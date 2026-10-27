using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Evaluation
{
    public sealed class DependencyGraphTests
    {
        private static BalanceProfile Profile(params BalanceTrack[] tracks)
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Tracks.AddRange(tracks);
            return profile;
        }

        private static BalanceTrack Track(string id, GrowthModel model)
            => new BalanceTrack(id, "stage", 1, 5, model);

        [Test]
        public void DependenciesPrecedeDependents()
        {
            // Authored in the wrong order on purpose: gold reads count, which is listed last.
            var profile = Profile(
                Track("gold", new TrackReferenceModel("count", 10d)),
                Track("count", new LinearModel(1d)));
            try
            {
                var order = DependencyGraph.Build(profile).EvaluationOrder;

                Assert.That(order.ToList().IndexOf("count"),
                    Is.LessThan(order.ToList().IndexOf("gold")));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void IndependentTracks_KeepAuthoringOrder()
        {
            var profile = Profile(
                Track("a", new LinearModel(1d)),
                Track("b", new LinearModel(2d)),
                Track("c", new LinearModel(3d)));
            try
            {
                Assert.That(DependencyGraph.Build(profile).EvaluationOrder,
                    Is.EqualTo(new[] { "a", "b", "c" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ACycle_IsReportedAsItsPath()
        {
            // "There is a cycle" is not actionable in a profile with forty tracks.
            var profile = Profile(
                Track("monster_hp", new TrackReferenceModel("stage_gold")),
                Track("stage_gold", new TrackReferenceModel("monster_hp")));
            try
            {
                var graph = DependencyGraph.Build(profile);

                Assert.That(graph.IsValid, Is.False);
                Assert.That(graph.Cycles.Count, Is.EqualTo(1));
                Assert.That(graph.DescribeCycles(),
                    Is.EqualTo("monster_hp → stage_gold → monster_hp"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ALongerCycle_IsReportedInFull()
        {
            var profile = Profile(
                Track("a", new TrackReferenceModel("c")),
                Track("b", new TrackReferenceModel("a")),
                Track("c", new TrackReferenceModel("b")));
            try
            {
                var graph = DependencyGraph.Build(profile);

                Assert.That(graph.Cycles.Count, Is.EqualTo(1));
                Assert.That(graph.Cycles[0].Count, Is.EqualTo(4), "three tracks plus the closing id");
                Assert.That(graph.DescribeCycles(), Does.Contain("a"));
                Assert.That(graph.DescribeCycles(), Does.Contain("b"));
                Assert.That(graph.DescribeCycles(), Does.Contain("c"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ATrackReadingItself_IsACycle()
        {
            var profile = Profile(Track("a", new TrackReferenceModel("a")));
            try
            {
                var graph = DependencyGraph.Build(profile);

                Assert.That(graph.IsValid, Is.False);
                Assert.That(graph.DescribeCycles(), Is.EqualTo("a → a"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void OneCycle_IsReportedOnceEvenWhenReachableFromSeveralEntryPoints()
        {
            // Two outside tracks both lead into the same loop.
            var profile = Profile(
                Track("in1", new TrackReferenceModel("a")),
                Track("in2", new TrackReferenceModel("b")),
                Track("a", new TrackReferenceModel("b")),
                Track("b", new TrackReferenceModel("a")));
            try
            {
                Assert.That(DependencyGraph.Build(profile).Cycles.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ReferencesToUnknownTracks_AreReportedSeparately()
        {
            // Distinct from a cycle: a typo, or a track deleted while something still reads it.
            var profile = Profile(Track("a", new TrackReferenceModel("does_not_exist")));
            try
            {
                var graph = DependencyGraph.Build(profile);

                Assert.That(graph.IsValid, Is.True, "A missing reference does not make the order invalid.");
                Assert.That(graph.MissingReferences.Count, Is.EqualTo(1));
                Assert.That(graph.MissingReferences[0].ToString(), Is.EqualTo("a → does_not_exist"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void NestedModels_ContributeTheirReferences()
        {
            var profile = Profile(
                Track("count", new LinearModel(1d)),
                Track("drop", new LinearModel(2d)),
                Track("gold", new CompositeModel(CompositeOperation.Product,
                    new TrackReferenceModel("count"),
                    new PiecewiseModel(
                        new PiecewiseSegment(1d, new TrackReferenceModel("drop"))))));
            try
            {
                var graph = DependencyGraph.Build(profile);

                Assert.That(graph.DependenciesOf("gold"),
                    Is.EquivalentTo(new[] { "count", "drop" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AffectedBy_ReturnsTheTransitiveDependentsInOrder()
        {
            // Changing count has to re-evaluate gold, and anything derived from gold.
            var profile = Profile(
                Track("count", new LinearModel(1d)),
                Track("gold", new TrackReferenceModel("count")),
                Track("total_gold", new TrackReferenceModel("gold")),
                Track("unrelated", new LinearModel(1d)));
            try
            {
                var affected = DependencyGraph.Build(profile).AffectedBy("count");

                Assert.That(affected, Is.EqualTo(new[] { "count", "gold", "total_gold" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Build_ToleratesNullsAndMissingIds()
        {
            var profile = Profile(
                null,
                Track(null, new LinearModel(1d)),
                Track("a", null));
            try
            {
                Assert.DoesNotThrow(() => DependencyGraph.Build(profile));
                Assert.That(DependencyGraph.Build(null).EvaluationOrder, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }

    public sealed class ProfileEvaluationTests
    {
        private static BalanceProfile DerivedProfile()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();

            // Authored so the derived track comes first, to prove ordering is not luck.
            profile.Tracks.Add(new BalanceTrack("gold", "stage", 1, 4,
                new CompositeModel(CompositeOperation.Product,
                    new TrackReferenceModel("monster_count"),
                    new TrackReferenceModel("gold_per_monster"))));
            profile.Tracks.Add(new BalanceTrack("monster_count", "stage", 1, 4, new LinearModel(1d, 2d)));
            profile.Tracks.Add(new BalanceTrack("gold_per_monster", "stage", 1, 4, new LinearModel(0d, 10d)));

            return profile;
        }

        [Test]
        public void DerivedTracks_ReadTheirInputs()
        {
            var profile = DerivedProfile();
            try
            {
                var evaluation = TrackEvaluator.EvaluateProfile(profile);

                // monster_count = x + 2 → 3,4,5,6; gold_per_monster = 10 → gold = 30,40,50,60
                Assert.That(evaluation["monster_count"], Is.EqualTo(new[] { 3d, 4d, 5d, 6d }));
                Assert.That(evaluation["gold"], Is.EqualTo(new[] { 30d, 40d, 50d, 60d }));
                Assert.That(evaluation.IsComplete, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AxisOffset_ReadsANeighbouringPosition()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("source", "stage", 1, 4, new LinearModel(10d)));
                profile.Tracks.Add(new BalanceTrack("previous", "stage", 1, 4,
                    new TrackReferenceModel("source", 1d, axisOffset: -1)));

                var evaluation = TrackEvaluator.EvaluateProfile(profile);

                // source = 10,20,30,40. Position 1 has no predecessor, so it reads nothing.
                Assert.That(evaluation["previous"], Is.EqualTo(new[] { 0d, 10d, 20d, 30d }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TracksInACycle_AreSkippedRatherThanGuessed()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("ok", "stage", 1, 3, new LinearModel(1d)));
                profile.Tracks.Add(new BalanceTrack("a", "stage", 1, 3, new TrackReferenceModel("b")));
                profile.Tracks.Add(new BalanceTrack("b", "stage", 1, 3, new TrackReferenceModel("a")));

                var evaluation = TrackEvaluator.EvaluateProfile(profile);

                Assert.That(evaluation.IsComplete, Is.False);
                Assert.That(evaluation["ok"], Is.Not.Null, "A cycle elsewhere must not stop the rest.");
                Assert.That(evaluation["a"], Is.Null);
                Assert.That(evaluation["b"], Is.Null);
                Assert.That(evaluation.Graph.DescribeCycles(), Does.Contain("→"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AMissingReference_ResolvesToZeroRatherThanThrowing()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("a", "stage", 1, 3,
                    new TrackReferenceModel("nope", 5d)));

                var evaluation = TrackEvaluator.EvaluateProfile(profile);

                Assert.That(evaluation["a"], Is.EqualTo(new[] { 0d, 0d, 0d }));
                Assert.That(evaluation.Graph.MissingReferences.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ASingleTrackEvaluatedAlone_StillWorks()
        {
            // Previews evaluate one track without the rest of the profile.
            var track = new BalanceTrack("a", "stage", 1, 3, new TrackReferenceModel("other"));

            Assert.That(TrackEvaluator.Evaluate(track, new EvalContext(0)),
                Is.EqualTo(new[] { 0d, 0d, 0d }));
        }
    }

    public sealed class CompositeModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void Operations_CombineTheParts()
        {
            var parts = new GrowthModel[] { new LinearModel(0d, 2d), new LinearModel(0d, 5d) };

            Assert.That(new CompositeModel(CompositeOperation.Sum, parts).Evaluate(1d, Ctx), Is.EqualTo(7d));
            Assert.That(new CompositeModel(CompositeOperation.Product, parts).Evaluate(1d, Ctx), Is.EqualTo(10d));
            Assert.That(new CompositeModel(CompositeOperation.Min, parts).Evaluate(1d, Ctx), Is.EqualTo(2d));
            Assert.That(new CompositeModel(CompositeOperation.Max, parts).Evaluate(1d, Ctx), Is.EqualTo(5d));
        }

        [Test]
        public void AnEmptyProduct_IsZeroNotOne()
        {
            // The identity would let a half-built track slip a 1 into a product unnoticed.
            Assert.That(new CompositeModel(CompositeOperation.Product).Evaluate(1d, Ctx), Is.EqualTo(0d));
            Assert.That(new CompositeModel(CompositeOperation.Sum).Evaluate(1d, Ctx), Is.EqualTo(0d));
        }

        [Test]
        public void NullParts_AreSkipped()
        {
            var model = new CompositeModel(CompositeOperation.Sum,
                null, new LinearModel(0d, 3d), null);

            Assert.That(model.Evaluate(1d, Ctx), Is.EqualTo(3d));
        }

        [Test]
        public void Describe_ReadsAsAnExpression()
        {
            Assert.That(new CompositeModel(CompositeOperation.Product,
                    new TrackReferenceModel("count"), new TrackReferenceModel("drop")).Describe(),
                Is.EqualTo("track(count) × track(drop)"));
            Assert.That(new CompositeModel(CompositeOperation.Sum).Describe(),
                Is.EqualTo("composite (empty)"));
        }
    }

    public sealed class TrackReferenceModelTests
    {
        [Test]
        public void Describe_ShowsTheOffsetAndScale()
        {
            Assert.That(new TrackReferenceModel("hp").Describe(), Is.EqualTo("track(hp)"));
            Assert.That(new TrackReferenceModel("hp", 2d).Describe(), Is.EqualTo("2 · track(hp)"));
            Assert.That(new TrackReferenceModel("hp", 1d, -1).Describe(), Is.EqualTo("track(hp, x - 1)"));
            Assert.That(new TrackReferenceModel(null).Describe(), Is.EqualTo("track (unset)"));
        }

        [Test]
        public void CollectTrackReferences_ReportsTheId()
        {
            var into = new List<string>();
            new TrackReferenceModel("hp").CollectTrackReferences(into);
            new TrackReferenceModel(null).CollectTrackReferences(into);

            Assert.That(into, Is.EqualTo(new[] { "hp" }));
        }
    }
}
