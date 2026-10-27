using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fulcrum.Tests.Model
{
    /// <summary>
    /// Round trips a profile through a real <c>.asset</c> file rather than an in-memory
    /// copy. The JSON round trips elsewhere prove the serialiser understands the types;
    /// this proves the shape that actually ships — Unity's YAML on disk — carries the
    /// polymorphic references too.
    /// </summary>
    public sealed class BalanceProfileAssetTests
    {
        private const string Folder = "Assets/__FulcrumTests__";
        private const string AssetPath = Folder + "/RoundTripProfile.asset";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__FulcrumTests__");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
            AssetDatabase.Refresh();
        }

        private static BalanceProfile BuildProfile()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.ProfileName = "v1.2 hard mode pass";
            profile.Notes = "Stage 42 pinned so the chapter boundary reads as a wall.";

            profile.Axes.Add(new AxisDefinition("stage", 1, 200, "Stage"));
            profile.Globals.Add(new GlobalConstant("difficulty", 1.3d, "Hard mode multiplier"));
            profile.SimulationSettings = new SimulationSettings(500, 99);

            var track = new BalanceTrack("monster_hp", "stage", 1, 50, new PowerModel(100d, 1.35d));
            track.DisplayName = "Monster HP";
            track.Accumulate = false;
            track.PostProcessors.Add(new RoundProcessor(RoundingMode.SignificantDigits, 3));
            track.PostProcessors.Add(new MultiplierProcessor(1d, "difficulty"));
            track.PostProcessors.Add(ClampProcessor.AtLeast(1d));
            track.Overrides.Add(new ManualOverride(42, 99999d, "Chapter boundary spike"));
            profile.Tracks.Add(track);

            var exp = new BalanceTrack("exp_required", "stage", 1, 50, new ExponentialModel(100d, 1.12d));
            exp.Accumulate = true;
            profile.Tracks.Add(exp);

            return profile;
        }

        [Test]
        public void Profile_SurvivesSaveAndReload()
        {
            var created = BuildProfile();
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var loaded = AssetDatabase.LoadAssetAtPath<BalanceProfile>(AssetPath);
            Assert.That(loaded, Is.Not.Null, "The asset was not written to disk.");

            Assert.That(loaded.ProfileName, Is.EqualTo("v1.2 hard mode pass"));
            Assert.That(loaded.Notes, Does.Contain("chapter boundary"));

            Assert.That(loaded.Axes.Count, Is.EqualTo(1));
            Assert.That(loaded.Axes[0].Id, Is.EqualTo("stage"));
            Assert.That(loaded.Axes[0].Length, Is.EqualTo(200));

            Assert.That(loaded.SimulationSettings.Iterations, Is.EqualTo(500));
            Assert.That(loaded.SimulationSettings.Seed, Is.EqualTo(99));

            Assert.That(loaded.TryGetTrack("monster_hp", out var hp), Is.True);
            Assert.That(hp.AxisId, Is.EqualTo("stage"));
            Assert.That(hp.RangeMin, Is.EqualTo(1));
            Assert.That(hp.RangeMax, Is.EqualTo(50));
            Assert.That(hp.Length, Is.EqualTo(50));
            Assert.That(hp.DisplayName, Is.EqualTo("Monster HP"));

            Assert.That(hp.Model, Is.TypeOf<PowerModel>());
            Assert.That(hp.Model.Describe(), Is.EqualTo("100 · x^1.35"));

            Assert.That(PostProcessorChain.Describe(hp.PostProcessors),
                Is.EqualTo("round 3sf → × 1 × difficulty → clamp ≥ 1"));

            Assert.That(hp.TryGetOverride(42, out var pinned), Is.True);
            Assert.That(pinned.Value, Is.EqualTo(99999d));
            Assert.That(pinned.Note, Is.EqualTo("Chapter boundary spike"));

            Assert.That(loaded.TryGetTrack("exp_required", out var exp), Is.True);
            Assert.That(exp.Accumulate, Is.True);
            Assert.That(exp.Model, Is.TypeOf<ExponentialModel>());
        }

        [Test]
        public void SavedYaml_RecordsTheConcreteTypeNames()
        {
            // The type names written here are exactly what a rename would invalidate, and
            // what [MovedFrom] exists to redirect. Assert they reach the file.
            var created = BuildProfile();
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();

            var yaml = File.ReadAllText(AssetPath);

            Assert.That(yaml, Does.Contain(nameof(PowerModel)));
            Assert.That(yaml, Does.Contain(nameof(ExponentialModel)));
            Assert.That(yaml, Does.Contain(nameof(RoundProcessor)));
            Assert.That(yaml, Does.Contain(nameof(MultiplierProcessor)));
            Assert.That(yaml, Does.Contain(nameof(ClampProcessor)));
            Assert.That(yaml, Does.Contain("Fulcrum.Core"));
        }

        [Test]
        public void Globals_ReachTheEvaluationContext()
        {
            var created = BuildProfile();
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var loaded = AssetDatabase.LoadAssetAtPath<BalanceProfile>(AssetPath);
            var context = loaded.CreateContext();

            Assert.That(context.GetGlobal("difficulty"), Is.EqualTo(1.3d));

            // Closes the loop with MultiplierProcessor: a constant declared once on the
            // profile is what the step actually reads.
            var values = new[] { 100d };
            new MultiplierProcessor(1d, "difficulty").Process(values, context);
            Assert.That(values[0], Is.EqualTo(130d).Within(1e-9));
        }
    }

    public sealed class BalanceProfileLookupTests
    {
        [Test]
        public void BuildGlobals_KeepsTheLastEntryForARepeatedKey()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Globals.Add(new GlobalConstant("k", 1d));
                profile.Globals.Add(new GlobalConstant("k", 2d));
                profile.Globals.Add(new GlobalConstant(null, 3d));

                var globals = profile.BuildGlobals();

                Assert.That(globals["k"], Is.EqualTo(2d));
                Assert.That(globals.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FindDuplicateTrackIds_ReportsRepeats()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("a"));
                profile.Tracks.Add(new BalanceTrack("b"));
                profile.Tracks.Add(new BalanceTrack("a"));
                profile.Tracks.Add(new BalanceTrack("a"));

                Assert.That(profile.FindDuplicateTrackIds(), Is.EqualTo(new[] { "a" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Lookups_RejectMissingAndEmptyIds()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("a"));
                profile.Axes.Add(new AxisDefinition("stage", 1, 10));

                Assert.That(profile.TryGetTrack("missing", out _), Is.False);
                Assert.That(profile.TryGetTrack(null, out _), Is.False);
                Assert.That(profile.TryGetAxis("missing", out _), Is.False);
                Assert.That(profile.TryGetAxis("stage", out var axis), Is.True);
                Assert.That(axis.Length, Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ProfileName_FallsBackToTheAssetName()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.name = "MainBalance";
                Assert.That(profile.ProfileName, Is.EqualTo("MainBalance"));

                profile.ProfileName = "v2 draft";
                Assert.That(profile.ProfileName, Is.EqualTo("v2 draft"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }

    public sealed class BalanceTrackShapeTests
    {
        [Test]
        public void Length_IsZeroForAnInvertedRange()
        {
            var track = new BalanceTrack("t", "stage", 10, 1);

            Assert.That(track.Length, Is.Zero);
        }

        [Test]
        public void TryGetOverride_UsesAxisPositionsNotArrayOffsets()
        {
            var track = new BalanceTrack("t", "stage", 10, 50);
            track.Overrides.Add(new ManualOverride(42, 7d));

            Assert.That(track.TryGetOverride(42, out var found), Is.True);
            Assert.That(found.Value, Is.EqualTo(7d));
            Assert.That(track.TryGetOverride(32, out _), Is.False);
        }

        [Test]
        public void TryGetOverride_LastEntryWinsForARepeatedPosition()
        {
            var track = new BalanceTrack("t");
            track.Overrides.Add(new ManualOverride(5, 1d));
            track.Overrides.Add(new ManualOverride(5, 2d));

            Assert.That(track.TryGetOverride(5, out var found), Is.True);
            Assert.That(found.Value, Is.EqualTo(2d));
        }

        [Test]
        public void TryGetOverride_SkipsNullEntries()
        {
            var track = new BalanceTrack("t");
            track.Overrides.Add(null);
            track.Overrides.Add(new ManualOverride(3, 9d));

            Assert.DoesNotThrow(() => track.TryGetOverride(3, out _));
            Assert.That(track.TryGetOverride(3, out var found), Is.True);
            Assert.That(found.Value, Is.EqualTo(9d));
        }
    }
}
