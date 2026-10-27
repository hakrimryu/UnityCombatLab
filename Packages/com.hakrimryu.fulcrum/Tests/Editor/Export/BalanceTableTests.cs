using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Export
{
    internal static class ExportFixture
    {
        public static BalanceProfile Build()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.ProfileName = "Export fixture";
            profile.Globals.Add(new GlobalConstant("difficulty", 2d));

            var hp = new BalanceTrack("monster_hp", "stage", 1, 5, new LinearModel(0d, 100d));
            hp.PostProcessors.Add(new MultiplierProcessor(1d, "difficulty"));
            profile.Tracks.Add(hp);

            // Deliberately narrower, so partial coverage is exercised.
            profile.Tracks.Add(new BalanceTrack("gold", "stage", 3, 5, new LinearModel(10d)));

            return profile;
        }
    }

    public sealed class BalanceTableTests
    {
        [Test]
        public void BakedValues_MatchTheEvaluator()
        {
            var profile = ExportFixture.Build();
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                BalanceTableBaker.Bake(profile, table);

                profile.TryGetTrack("monster_hp", out var hp);
                var expected = TrackEvaluator.Evaluate(profile, hp);

                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.That(table.TryGet("monster_hp", hp.AxisPositionAt(i), out var value), Is.True);
                    Assert.That(value, Is.EqualTo(expected[i]));
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Lookups_UseAxisPositionsAndRespectTrackRanges()
        {
            var profile = ExportFixture.Build();
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                BalanceTableBaker.Bake(profile, table);

                // gold covers stages 3..5 only.
                Assert.That(table.TryGetRange("gold", out var min, out var max), Is.True);
                Assert.That(min, Is.EqualTo(3));
                Assert.That(max, Is.EqualTo(5));

                Assert.That(table.TryGet("gold", 3, out var atThree), Is.True);
                Assert.That(atThree, Is.EqualTo(30d));

                // Asking beyond a track is a normal condition, not an error.
                Assert.That(table.TryGet("gold", 2, out _), Is.False);
                Assert.That(table.TryGet("gold", 6, out _), Is.False);
                Assert.That(table.Get("gold", 99, fallback: -1d), Is.EqualTo(-1d));
                Assert.That(table.TryGet("unknown", 3, out _), Is.False);
                Assert.That(table.Contains("monster_hp"), Is.True);
                Assert.That(table.Contains("unknown"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Hash_ChangesWithParametersButNotWithLabels()
        {
            var profile = ExportFixture.Build();
            try
            {
                var before = BalanceTableBaker.ComputeHash(profile);

                profile.TryGetTrack("monster_hp", out var hp);
                hp.DisplayName = "Renamed for the designer's benefit";
                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.EqualTo(before),
                    "A cosmetic edit should not read as a balance change.");

                ((LinearModel)hp.Model).Intercept = 101d;
                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.Not.EqualTo(before),
                    "A parameter change must always change the fingerprint.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Hash_ReflectsChainOrder()
        {
            var profile = ExportFixture.Build();
            try
            {
                profile.TryGetTrack("monster_hp", out var hp);
                hp.PostProcessors.Add(ClampProcessor.AtMost(150d));
                var before = BalanceTableBaker.ComputeHash(profile);

                var moved = hp.PostProcessors[0];
                hp.PostProcessors.RemoveAt(0);
                hp.PostProcessors.Add(moved);

                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.Not.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Bake_ToleratesANullProfile()
        {
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                Assert.DoesNotThrow(() => BalanceTableBaker.Bake(null, table));
                Assert.That(table.TrackCount, Is.Zero);
                Assert.That(table.TryGet("anything", 0, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Bake_KeepsAuthoringOrder()
        {
            var profile = ExportFixture.Build();
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                BalanceTableBaker.Bake(profile, table);

                Assert.That(table.TrackIds, Is.EqualTo(new[] { "monster_hp", "gold" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void InvalidExportPreservesTheSavedTableAndItsReference()
        {
            var profile = ExportFixture.Build();
            var path = "Assets/FulcrumExportGuard_" + System.Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                var table = Fulcrum.Editor.Export.ProfileExporter.ExportTable(profile, path);
                var before = UnityEditor.EditorJsonUtility.ToJson(table);
                var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                profile.Tracks.Add(new BalanceTrack("cycle_a", "stage", 1, 5, new TrackReferenceModel("cycle_b")));
                profile.Tracks.Add(new BalanceTrack("cycle_b", "stage", 1, 5, new TrackReferenceModel("cycle_a")));

                Assert.Throws<System.InvalidOperationException>(() => Fulcrum.Editor.Export.ProfileExporter.ExportTable(profile, path));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(table), Is.EqualTo(before));
                Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<BalanceTable>(path), Is.SameAs(table));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(path);
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void NonFiniteExportPreservesThePreviousTable(double invalid)
        {
            var profile = ExportFixture.Build();
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                BalanceTableBaker.Bake(profile, table, "before");
                var before = UnityEditor.EditorJsonUtility.ToJson(table);
                profile.Tracks[0].Model = new TableModel(1d, invalid);
                Assert.Throws<System.InvalidOperationException>(() => BalanceTableBaker.Bake(profile, table, "after"));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(table), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void MissingReferenceAndDuplicateIdsAreRejectedBeforeReplacement()
        {
            var profile = ExportFixture.Build();
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                BalanceTableBaker.Bake(profile, table);
                var before = UnityEditor.EditorJsonUtility.ToJson(table);
                var track = profile.Tracks[0];
                var model = track.Model;
                track.Model = new TrackReferenceModel("missing");
                Assert.Throws<System.InvalidOperationException>(() => BalanceTableBaker.Bake(profile, table));
                track.Model = model;
                profile.Tracks.Add(new BalanceTrack(track.Id, "stage", 1, 5, new LinearModel(5d)));
                Assert.Throws<System.InvalidOperationException>(() => BalanceTableBaker.Bake(profile, table));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(table), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(table);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HashDetectsValuesInsideNestedModels(bool piecewise)
        {
            var profile = ExportFixture.Build();
            try
            {
                var model = new TableModel(1d, 10d, 20d, 30d, 40d, 50d);
                profile.Tracks[0].Model = piecewise
                    ? (GrowthModel)new PiecewiseModel(new PiecewiseSegment(1d, model))
                    : new CompositeModel(CompositeOperation.Sum, model, new LinearModel(1d));
                var before = BalanceTableBaker.ComputeHash(profile);
                model.Values[1] = 21d;
                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.Not.EqualTo(before));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void HashDetectsTableAndCurveEditsAndIsCultureIndependent()
        {
            var profile = ExportFixture.Build();
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                var model = new TableModel(1d, 10d, 20d, 30d, 40d, 50d);
                profile.Tracks[0].Model = model;
                var before = BalanceTableBaker.ComputeHash(profile);
                model.Values[1] = 20.000000000001d;
                var changed = BalanceTableBaker.ComputeHash(profile);
                Assert.That(changed, Is.Not.EqualTo(before));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.EqualTo(changed));
                var curve = new CurveModel(AnimationCurve.Linear(1f, 10f, 5f, 50f));
                profile.Tracks[0].Model = curve;
                before = BalanceTableBaker.ComputeHash(profile);
                curve.Curve.MoveKey(1, new Keyframe(5f, 60f));
                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.Not.EqualTo(before));
                before = BalanceTableBaker.ComputeHash(profile);
                curve.TimeScale = 2d;
                Assert.That(BalanceTableBaker.ComputeHash(profile), Is.Not.EqualTo(before));
                var table = ScriptableObject.CreateInstance<BalanceTable>();
                try
                {
                    BalanceTableBaker.Bake(profile, table, "first");
                    before = table.SourceProfileHash;
                    BalanceTableBaker.Bake(profile, table, "second");
                    Assert.That(table.SourceProfileHash, Is.EqualTo(before));
                }
                finally { Object.DestroyImmediate(table); }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                Object.DestroyImmediate(profile);
            }
        }

    }
}
