using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fulcrum.Tests.Fixtures
{
    /// <summary>
    /// Guards against the failure mode that <c>[SerializeReference]</c> makes possible:
    /// renaming a class silently empties every asset that referenced it.
    /// </summary>
    /// <remarks>
    /// Unity logs a "missing types" warning when it loads such an asset, but nothing
    /// fails: the field becomes null, the profile loads, and the curve quietly evaluates to
    /// zeros. A console warning in a busy console is not a safety net, and it only appears
    /// for assets that happen to be loaded.
    ///
    /// Round-tripping objects built in code cannot catch this either, because the code and
    /// the data change together. Only a frozen asset can — which is why the fixture is
    /// committed rather than generated.
    /// </remarks>
    public sealed class SerializeReferenceMigrationTests
    {
        private const string ValidFixture = MigrationFixtureBuilder.FixturePath;

        /// <summary>
        /// The deliberately broken copy is stored with a <c>.txt</c> suffix so Unity does
        /// not import it. As a real asset it would log "missing types" on every domain
        /// reload — a permanent warning that trains everyone to ignore exactly the message
        /// this fixture exists to demonstrate.
        /// </summary>
        private const string RenamedTemplate =
            MigrationFixtureBuilder.FixtureFolder + "/RenamedTypeFixture.asset.txt";

        private const string ProbeFolder = "Assets/__FulcrumMigrationProbe__";
        private const string ProbeAsset = ProbeFolder + "/RenamedTypeFixture.asset";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(ProbeFolder)) AssetDatabase.DeleteAsset(ProbeFolder);
        }

        /// <summary>Materialises the broken copy as a real asset for the duration of a test.</summary>
        private static BalanceProfile LoadRenamedFixture()
        {
            if (!AssetDatabase.IsValidFolder(ProbeFolder))
            {
                AssetDatabase.CreateFolder("Assets", "__FulcrumMigrationProbe__");
            }

            System.IO.File.WriteAllText(ProbeAsset, System.IO.File.ReadAllText(RenamedTemplate));
            AssetDatabase.ImportAsset(ProbeAsset, ImportAssetOptions.ForceSynchronousImport);

            var profile = AssetDatabase.LoadAssetAtPath<BalanceProfile>(ProbeAsset);
            Assert.That(profile, Is.Not.Null, "The broken fixture did not import.");
            return profile;
        }

        /// <summary>
        /// Names the types an asset refers to but Unity could not resolve.
        /// </summary>
        /// <remarks>
        /// Walking <see cref="SerializedProperty"/> does not work for this. When a stored
        /// type cannot be found, <c>managedReferenceFullTypename</c> comes back empty and
        /// the value is null — indistinguishable from a reference that was simply never
        /// assigned, which is a normal authoring state.
        ///
        /// <see cref="SerializationUtility"/> keeps the unresolved coordinates instead, so
        /// the failure can be reported with the name that needs restoring.
        /// </remarks>
        private static string[] FindMissingTypes(Object target)
        {
            return SerializationUtility.GetManagedReferencesWithMissingTypes(target)
                .Select(m => $"{m.namespaceName}.{m.className} ({m.assemblyName})")
                .ToArray();
        }

        private static BalanceProfile Load(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BalanceProfile>(path);
            Assert.That(profile, Is.Not.Null, $"Fixture missing at {path}");
            return profile;
        }

        [Test]
        public void Fixture_HasNoBrokenReferences()
        {
            var profile = Load(ValidFixture);
            var missing = FindMissingTypes(profile);

            Assert.That(missing, Is.Empty,
                "A shipped type was renamed or moved, and every asset referring to it has "
                + "silently lost that field. Restore the name, or add the old coordinates "
                + "to its [MovedFrom]. Unresolved: " + string.Join(", ", missing));
        }

        [Test]
        public void Detector_FindsARenamedType()
        {
            // Proves the check above can actually fail. This fixture is a copy of the valid
            // one with a single type name altered to something that does not exist, which
            // is exactly what a rename leaves behind in already-saved assets.
            var missing = FindMissingTypes(LoadRenamedFixture());

            Assert.That(missing.Length, Is.EqualTo(1));
            Assert.That(missing[0], Does.Contain("TableModelThatNoLongerExists"));
            Assert.That(missing[0], Does.Contain("Fulcrum.Core"));
        }

        [Test]
        public void ARenamedType_LeavesTheFieldNullWithoutAnyError()
        {
            // The reason this whole file exists: nothing fails. The profile loads, the
            // track survives, and its curve is simply gone.
            var profile = LoadRenamedFixture();

            Assert.That(profile.TryGetTrack("table", out var track), Is.True);
            Assert.That(track.Model, Is.Null);
            Assert.That(track.HasModel, Is.False);

            // And it evaluates to a flat zero line rather than reporting anything.
            Assert.That(TrackEvaluator.Evaluate(track, profile.CreateContext()),
                Is.All.EqualTo(0d));
        }

        [Test]
        public void Fixture_CoversEveryShippedSerializedType()
        {
            // A model added later must reach the fixture, or the guard above stops
            // covering it.
            var expected = typeof(GrowthModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => typeof(GrowthModel).IsAssignableFrom(t)
                         || typeof(PostProcessor).IsAssignableFrom(t))
                .Select(t => t.FullName)
                .ToArray();

            var present = new HashSet<string>();
            var serialized = new SerializedObject(Load(ValidFixture));
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                var value = iterator.managedReferenceValue;
                if (value != null) present.Add(value.GetType().FullName);
            }

            var missing = expected.Where(t => !present.Contains(t)).ToArray();

            Assert.That(missing, Is.Empty,
                "These types are not represented in the fixture. Regenerate it from "
                + "Window/Fulcrum/Regenerate Migration Fixture and commit the result. "
                + "Missing: " + string.Join(", ", missing));
        }

        [Test]
        public void Fixture_StillEvaluates()
        {
            // A reference can resolve and still have lost its parameters, so the fixture
            // is also exercised rather than merely inspected.
            var profile = Load(ValidFixture);
            var results = TrackEvaluator.EvaluateAll(profile);

            Assert.That(results.Count, Is.EqualTo(profile.Tracks.Count));

            foreach (var pair in results)
            {
                Assert.That(pair.Value.Length, Is.EqualTo(60), $"{pair.Key} evaluated to the wrong length.");
                Assert.That(pair.Value.Any(v => v != 0d), Is.True,
                    $"{pair.Key} evaluated to all zeros, which is what a lost model looks like.");
            }
        }

        [Test]
        public void Fixture_PreservesNestedModelsAndChainOrder()
        {
            var profile = Load(ValidFixture);

            Assert.That(profile.TryGetTrack("piecewise", out var piecewise), Is.True);
            var outer = piecewise.Model as PiecewiseModel;
            Assert.That(outer, Is.Not.Null);
            Assert.That(outer.Segments[1].Model, Is.TypeOf<PiecewiseModel>(),
                "The nested model was lost.");

            Assert.That(profile.TryGetTrack("full_chain", out var chained), Is.True);
            Assert.That(PostProcessorChain.Describe(chained.PostProcessors),
                Is.EqualTo("round 3sf → × 1.1 × difficulty → min step 5 → smooth 5 → clamp ≥ 1"));
            Assert.That(chained.Accumulate, Is.True);
            Assert.That(chained.TryGetOverride(42, out var pinned), Is.True);
            Assert.That(pinned.Value, Is.EqualTo(99999d));
        }
    }
}
