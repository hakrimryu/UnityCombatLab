using Fulcrum.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Graph
{
    public sealed class GraphViewTests
    {
        private static GraphSeries Series(string name, int first, params double[] values)
            => new GraphSeries(name, values, first, Color.white);

        [Test]
        public void SeveralSeries_ShareOneAxisRange()
        {
            var graph = new CurveGraphElement();
            graph.SetSeries(new[]
            {
                Series("a", 1, 1d, 2d, 3d),
                Series("b", 5, 4d, 5d),
            });

            Assert.That(graph.SeriesCount, Is.EqualTo(2));
            Assert.That(graph.AxisFrom, Is.EqualTo(1));
            Assert.That(graph.AxisTo, Is.EqualTo(6));
            Assert.That(graph.MaxValue, Is.EqualTo(5d));
        }

        [Test]
        public void EmptySeries_AreDropped()
        {
            var graph = new CurveGraphElement();
            graph.SetSeries(new[] { Series("empty", 1), Series("a", 1, 1d) });

            Assert.That(graph.SeriesCount, Is.EqualTo(1));
        }

        [Test]
        public void ZoomingRecomputesBoundsFromTheVisibleRange()
        {
            // Zooming into a quiet stretch has to show its shape, not a flat line pressed
            // under a peak elsewhere.
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d, 1000d }, firstAxisPosition: 1);
            Assert.That(graph.MaxValue, Is.EqualTo(1000d));

            graph.SetView(1, 3);

            Assert.That(graph.MaxValue, Is.EqualTo(3d));
            Assert.That(graph.ViewFrom, Is.EqualTo(1));
            Assert.That(graph.ViewTo, Is.EqualTo(3));
        }

        [Test]
        public void TheViewIsClampedToWhatExists()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d, 4d }, firstAxisPosition: 1);

            graph.SetView(-50, 500);

            Assert.That(graph.ViewFrom, Is.EqualTo(1));
            Assert.That(graph.ViewTo, Is.EqualTo(4));
        }

        [Test]
        public void TheViewNeverCollapsesToASinglePosition()
        {
            // There would be no line to draw, and no way back out by zooming.
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d, 4d }, firstAxisPosition: 1);

            graph.SetView(2, 2);

            Assert.That(graph.ViewTo, Is.GreaterThan(graph.ViewFrom));
        }

        [Test]
        public void ResetView_ShowsEverythingAgain()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d, 1000d }, firstAxisPosition: 1);
            graph.SetView(1, 2);

            graph.ResetView();

            Assert.That(graph.ViewFrom, Is.EqualTo(1));
            Assert.That(graph.ViewTo, Is.EqualTo(4));
            Assert.That(graph.MaxValue, Is.EqualTo(1000d));
        }

        [Test]
        public void NewDataResetsTheView()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d, 4d }, firstAxisPosition: 1);
            graph.SetView(1, 2);

            graph.SetValues(new[] { 10d, 20d, 30d }, firstAxisPosition: 1);

            Assert.That(graph.ViewTo, Is.EqualTo(3), "A stale view would crop the new track.");
        }

        [Test]
        public void LogScale_ExcludesNonPositiveValuesFromTheBounds()
        {
            // Zero has no position on a logarithmic axis.
            var graph = new CurveGraphElement { LogScale = true };
            graph.SetValues(new[] { 0d, 10d, 1000d }, firstAxisPosition: 1);

            Assert.That(graph.MinValue, Is.EqualTo(10d));
            Assert.That(graph.MaxValue, Is.EqualTo(1000d));
        }

        [Test]
        public void LogScale_TogglesWithoutLosingTheData()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 10d, 100d }, firstAxisPosition: 1);

            graph.LogScale = true;
            graph.LogScale = false;

            Assert.That(graph.Count, Is.EqualTo(3));
            Assert.That(graph.MaxValue, Is.EqualTo(100d));
        }

        [Test]
        public void ZoomingRestrictsWhichSamplesAreDrawn()
        {
            // Plotting the whole series while zoomed puts off-view samples outside the plot
            // rectangle, and painter2D geometry is not bounded by the element — the line
            // ends up drawn over whatever sits beside the graph.
            var graph = new CurveGraphElement();
            var series = Series("a", 1, new double[100]);
            graph.SetSeries(new[] { series });

            Assert.That(graph.TryGetVisibleIndexRange(series, out var from, out var to), Is.True);
            Assert.That(from, Is.EqualTo(0));
            Assert.That(to, Is.EqualTo(99));

            graph.SetView(40, 50);

            Assert.That(graph.TryGetVisibleIndexRange(series, out from, out to), Is.True);
            Assert.That(from, Is.EqualTo(39), "Position 40 is the first visible sample.");
            Assert.That(to, Is.EqualTo(49), "Position 50 is the last.");
        }

        [Test]
        public void ASeriesEntirelyOutsideTheViewIsNotDrawn()
        {
            var graph = new CurveGraphElement();
            var near = Series("near", 1, 1d, 2d, 3d);
            var far = Series("far", 500, 1d, 2d, 3d);
            graph.SetSeries(new[] { near, far });

            graph.SetView(1, 3);

            Assert.That(graph.TryGetVisibleIndexRange(far, out _, out _), Is.False);
        }

        [Test]
        public void TheVisibleRangeStaysInsideTheSeries()
        {
            var graph = new CurveGraphElement();
            var series = Series("a", 1, 1d, 2d, 3d, 4d);
            graph.SetSeries(new[] { series });

            Assert.That(graph.TryGetVisibleIndexRange(series, out var from, out var to), Is.True);
            Assert.That(from, Is.GreaterThanOrEqualTo(0));
            Assert.That(to, Is.LessThan(series.Count));
        }

        [Test]
        public void HighlightsAreAccepted()
        {
            var graph = new CurveGraphElement();
            graph.SetValues(new[] { 1d, 2d, 3d }, firstAxisPosition: 1);

            Assert.DoesNotThrow(() => graph.SetHighlights(new[]
            {
                new GraphHighlight(1, 2, Color.red, "wall"),
            }));
            Assert.DoesNotThrow(() => graph.SetHighlights(null));
        }

        [Test]
        public void PaletteEntriesAreDistinct()
        {
            // Presets confused for one another defeat the comparison they exist for.
            for (var i = 0; i < GraphSeries.Palette.Length; i++)
            {
                for (var j = i + 1; j < GraphSeries.Palette.Length; j++)
                {
                    Assert.That(GraphSeries.Palette[i], Is.Not.EqualTo(GraphSeries.Palette[j]));
                }
            }

            Assert.That(GraphSeries.ColorAt(0), Is.EqualTo(GraphSeries.ColorAt(GraphSeries.Palette.Length)));
        }

        [Test]
        public void SeriesReadsSamplesByAxisPosition()
        {
            var series = Series("a", 10, 1d, 2d, 3d);

            Assert.That(series.LastAxisPosition, Is.EqualTo(12));
            Assert.That(series.TryGetAt(11, out var value), Is.True);
            Assert.That(value, Is.EqualTo(2d));
            Assert.That(series.TryGetAt(9, out _), Is.False);
            Assert.That(series.TryGetAt(13, out _), Is.False);
        }
    }
}
