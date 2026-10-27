using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Evaluation
{
    public sealed class ProfileEvaluationCacheTests
    {
        private BalanceProfile _profile;

        /// <summary>
        /// count → gold → total, plus one track that depends on nothing.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<BalanceProfile>();
            _profile.Tracks.Add(new BalanceTrack("count", "stage", 1, 4, new LinearModel(0d, 2d)));
            _profile.Tracks.Add(new BalanceTrack("gold", "stage", 1, 4,
                new TrackReferenceModel("count", 10d)));
            _profile.Tracks.Add(new BalanceTrack("total", "stage", 1, 4,
                new TrackReferenceModel("gold", 2d)));
            _profile.Tracks.Add(new BalanceTrack("unrelated", "stage", 1, 4, new LinearModel(1d)));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_profile);

        [Test]
        public void FirstAccess_EvaluatesEveryTrack()
        {
            var cache = new ProfileEvaluationCache(_profile);

            Assert.That(cache.GetSeries("gold"), Is.EqualTo(new[] { 20d, 20d, 20d, 20d }));
            Assert.That(cache.EvaluationCount, Is.EqualTo(4));
        }

        [Test]
        public void RepeatedAccess_DoesNoWork()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("gold");
            var after = cache.EvaluationCount;

            cache.GetSeries("gold");
            cache.GetSeries("total");

            Assert.That(cache.EvaluationCount, Is.EqualTo(after));
        }

        [Test]
        public void InvalidatingATrack_RedoesOnlyItAndItsDependents()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("total");
            var before = cache.EvaluationCount;

            cache.InvalidateTrack("gold");
            cache.GetSeries("total");

            // gold and total, not count and not unrelated.
            Assert.That(cache.EvaluationCount - before, Is.EqualTo(2));
        }

        [Test]
        public void InvalidatingALeaf_RedoesOnlyThatTrack()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("total");
            var before = cache.EvaluationCount;

            cache.InvalidateTrack("unrelated");
            cache.GetSeries("unrelated");

            Assert.That(cache.EvaluationCount - before, Is.EqualTo(1));
        }

        [Test]
        public void DependentsSeeTheNewValue()
        {
            var cache = new ProfileEvaluationCache(_profile);
            Assert.That(cache.GetSeries("total"), Is.EqualTo(new[] { 40d, 40d, 40d, 40d }));

            _profile.TryGetTrack("count", out var count);
            ((LinearModel)count.Model).Intercept = 3d;
            cache.InvalidateTrack("count");

            // count 3 → gold 30 → total 60. A stale read anywhere shows up here.
            Assert.That(cache.GetSeries("total"), Is.EqualTo(new[] { 60d, 60d, 60d, 60d }));
        }

        [Test]
        public void DependentsAreRedoneInDependencyOrder()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("total");

            _profile.TryGetTrack("count", out var count);
            ((LinearModel)count.Model).Intercept = 5d;
            cache.InvalidateTrack("count");

            // Redoing total before gold would leave it reading the previous gold.
            Assert.That(cache.GetSeries("total"), Is.EqualTo(new[] { 100d, 100d, 100d, 100d }));
        }

        [Test]
        public void InvalidateValues_RedoesEverythingWithoutRebuildingTheGraph()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("total");
            var graph = cache.Graph;
            var before = cache.EvaluationCount;

            cache.InvalidateValues();
            cache.GetSeries("total");

            Assert.That(cache.EvaluationCount - before, Is.EqualTo(4));
            Assert.That(cache.Graph, Is.SameAs(graph));
        }

        [Test]
        public void InvalidateStructure_RebuildsTheGraph()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("total");
            var graph = cache.Graph;

            _profile.Tracks.Add(new BalanceTrack("added", "stage", 1, 4, new LinearModel(1d)));
            cache.InvalidateStructure();

            Assert.That(cache.Graph, Is.Not.SameAs(graph));
            Assert.That(cache.GetSeries("added"), Is.Not.Null);
        }

        [Test]
        public void AStructuralChangeReportedAsAValueChange_StillRebuilds()
        {
            // Callers that cannot tell the two apart must not end up with a stale order.
            var cache = new ProfileEvaluationCache(_profile);
            cache.InvalidateStructure();

            Assert.DoesNotThrow(() => cache.InvalidateTrack("gold"));
            Assert.That(cache.GetSeries("gold"), Is.Not.Null);
        }

        [Test]
        public void ChangingProfile_Resets()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("gold");

            var other = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                other.Tracks.Add(new BalanceTrack("only", "stage", 1, 2, new LinearModel(7d)));
                cache.Profile = other;

                Assert.That(cache.GetSeries("gold"), Is.Null);
                Assert.That(cache.GetSeries("only"), Is.EqualTo(new[] { 7d, 14d }));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ANullProfile_IsUsable()
        {
            var cache = new ProfileEvaluationCache();

            Assert.That(cache.GetSeries("anything"), Is.Null);
            Assert.That(cache.Graph.EvaluationOrder, Is.Empty);
            Assert.That(cache.EvaluationCount, Is.Zero);
        }

        [Test]
        public void CachedValues_MatchAFullEvaluation()
        {
            var cache = new ProfileEvaluationCache(_profile);
            cache.GetSeries("total");

            _profile.TryGetTrack("count", out var count);
            ((LinearModel)count.Model).Intercept = 7d;
            cache.InvalidateTrack("count");
            cache.GetSeries("total");

            var fresh = TrackEvaluator.EvaluateProfile(_profile);
            foreach (var id in fresh.TrackIds)
            {
                Assert.That(cache.GetSeries(id), Is.EqualTo(fresh[id]), $"{id} drifted from a full evaluation.");
            }
        }
    }
}
