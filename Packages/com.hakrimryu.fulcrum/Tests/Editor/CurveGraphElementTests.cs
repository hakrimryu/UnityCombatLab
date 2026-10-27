using Fulcrum.Editor.Graph;
using NUnit.Framework;

namespace Fulcrum.Tests
{
    /// <summary>
    /// Covers the parts of the graph element that do not need a laid-out panel.
    /// Hit testing and painting depend on resolved layout, so they are exercised
    /// against a live editor window rather than here.
    /// </summary>
    public sealed class CurveGraphElementTests
    {
        [Test]
        public void TryGetValue_ReturnsStoredSample()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 10d, 20d, 30d });

            Assert.That(graph.TryGetValue(1, out var value), Is.True);
            Assert.That(value, Is.EqualTo(20d));
        }

        [Test]
        public void TryGetValue_RejectsOutOfRangeIndex()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 10d, 20d });

            Assert.That(graph.TryGetValue(-1, out _), Is.False);
            Assert.That(graph.TryGetValue(2, out _), Is.False);
        }

        [Test]
        public void SetValues_AcceptsNull()
        {
            var graph = new CurveGraphElement();

            Assert.DoesNotThrow(() => graph.SetValues(null));
            Assert.That(graph.TryGetValue(0, out _), Is.False);
        }

        [Test]
        public void SetValues_AcceptsFlatSeries()
        {
            // A flat series has a zero span; normalising it must not divide by zero.
            var graph = new CurveGraphElement();

            Assert.DoesNotThrow(() => graph.SetValues(new[] { 5d, 5d, 5d }));
            Assert.That(graph.TryGetValue(2, out var value), Is.True);
            Assert.That(value, Is.EqualTo(5d));
        }

        [Test]
        public void HoverIndex_DefaultsToNone()
        {
            var graph = new CurveGraphElement();

            Assert.That(graph.HoverIndex, Is.EqualTo(-1));
        }
    }
}
