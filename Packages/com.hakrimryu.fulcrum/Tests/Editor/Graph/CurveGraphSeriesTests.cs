using Fulcrum.Editor.Graph;
using NUnit.Framework;

namespace Fulcrum.Tests.Graph
{
    /// <summary>
    /// Covers how the graph element interprets a series. Painting needs a laid-out panel,
    /// so it is verified against a live window instead.
    /// </summary>
    public sealed class CurveGraphSeriesTests
    {
        [Test]
        public void AxisPositions_FollowTheTrackRange()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d }, firstAxisPosition: 10);

            Assert.That(graph.FirstAxisPosition, Is.EqualTo(10));
            Assert.That(graph.AxisPositionAt(0), Is.EqualTo(10));
            Assert.That(graph.AxisPositionAt(2), Is.EqualTo(12));
        }

        [Test]
        public void Bounds_ExcludeNonFiniteSamples()
        {
            // One NaN in the bounds would collapse every real value into a single pixel.
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 10d, double.NaN, 30d });

            Assert.That(graph.MinValue, Is.EqualTo(10d));
            Assert.That(graph.MaxValue, Is.EqualTo(30d));
            Assert.That(graph.HasNonFiniteValues, Is.True);
        }

        [Test]
        public void Bounds_SurviveASeriesThatIsEntirelyNonFinite()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { double.NaN, double.PositiveInfinity });

            Assert.That(graph.MinValue, Is.EqualTo(0d));
            Assert.That(graph.MaxValue, Is.EqualTo(1d));
            Assert.That(graph.HasNonFiniteValues, Is.True);
        }

        [Test]
        public void NonFiniteFlag_ClearsForACleanSeries()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { double.NaN });
            graph.SetValues(new[] { 1d, 2d });

            Assert.That(graph.HasNonFiniteValues, Is.False);
            Assert.That(graph.Count, Is.EqualTo(2));
        }

        [Test]
        public void HoverResets_WhenTheSeriesChanges()
        {
            // Otherwise a stale index would report a value from the previous track.
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d });

            Assert.That(graph.HoverIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EvaluatedTrack_FeedsTheGraphDirectly()
        {
            var track = new BalanceTrack("monster_hp", "stage", 10, 14, new LinearModel(100d));
            var values = TrackEvaluator.Evaluate(track, new EvalContext(0));

            var graph = new CurveGraphElement();
            graph.SetValues(values, track.RangeMin);

            Assert.That(graph.Count, Is.EqualTo(5));
            Assert.That(graph.AxisPositionAt(0), Is.EqualTo(10));
            Assert.That(graph.TryGetValue(0, out var first), Is.True);
            Assert.That(first, Is.EqualTo(1000d));
            Assert.That(graph.MaxValue, Is.EqualTo(1400d));
        }
    }
}
