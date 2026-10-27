using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Graph
{
    /// <summary>
    /// Draws one or more series as polylines with axes, a grid, shaded spans and a hover
    /// cursor, using the UI Toolkit vector painter and nothing else.
    /// </summary>
    /// <remarks>
    /// GenerateVisualContent only runs when the element is marked dirty, so callers must
    /// go through the setters rather than mutating arrays in place. Samples are decimated
    /// to the element's pixel width — drawing 200 stages into 400px does not need 200 line
    /// segments.
    /// </remarks>
    public sealed class CurveGraphElement : VisualElement
    {
        private const float PaddingLeft = 44f;
        private const float PaddingRight = 12f;
        private const float PaddingTop = 12f;
        private const float PaddingBottom = 24f;
        private const float MinPixelsPerSample = 2f;
        private const double LogFloor = 1e-6d;

        private readonly List<GraphSeries> _series = new List<GraphSeries>();
        private readonly List<GraphHighlight> _highlights = new List<GraphHighlight>();

        /// <summary>Axis positions carrying a manual override, drawn as markers.</summary>
        private readonly HashSet<int> _pins = new HashSet<int>();

        private double _min;
        private double _max = 1d;
        private int _axisFrom;
        private int _axisTo;
        private int _viewFrom;
        private int _viewTo;
        private int _hoverIndex = -1;
        private bool _hasNonFinite;
        private bool _logScale;
        private Vector2 _panStart;
        private int _panViewFrom;
        private bool _panning;

        /// <summary>Index of the sample under the pointer, or -1 when not hovering.</summary>
        public int HoverIndex => _hoverIndex;

        /// <summary>Raised when the hovered sample changes. Carries -1 when the pointer leaves.</summary>
        public event Action<int> HoverIndexChanged;

        /// <summary>
        /// Raised when a point on the curve is picked for editing, with its axis position.
        /// </summary>
        public event Action<int> PositionPicked;

        /// <summary>Raised when the visible axis range changes through zoom or pan.</summary>
        public event Action ViewChanged;

        public CurveGraphElement()
        {
            style.flexGrow = 1f;

            // painter2D geometry is not bounded by the element; without this a zoomed view
            // would paint over whatever sits next to the graph.
            style.overflow = Overflow.Hidden;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(_ => SetHoverIndex(-1));
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);

            // Double click rather than single: a single click on a graph is how you point at
            // something, and making that edit the data would fight the reading of it.
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount >= 2) PickAt(evt.localPosition.x);
            });
        }

        // --- data ---------------------------------------------------------------------

        /// <summary>Axis position the first sample of the primary series corresponds to.</summary>
        public int FirstAxisPosition => _axisFrom;

        /// <summary>Samples in the primary series.</summary>
        public int Count => _series.Count == 0 ? 0 : _series[0].Count;

        /// <summary>Number of plotted series.</summary>
        public int SeriesCount => _series.Count;

        /// <summary>Lowest plotted value, after non-finite samples are excluded.</summary>
        public double MinValue => _min;

        /// <inheritdoc cref="MinValue"/>
        public double MaxValue => _max;

        /// <summary>
        /// Whether any series contains NaN or an infinity.
        /// </summary>
        /// <remarks>
        /// Worth surfacing: a non-finite sample leaves a gap in the line, which reads as a
        /// rendering glitch rather than as a formula that has no value there.
        /// </remarks>
        public bool HasNonFiniteValues => _hasNonFinite;

        /// <summary>First axis position covered by any series.</summary>
        public int AxisFrom => _axisFrom;

        /// <summary>Last axis position covered by any series.</summary>
        public int AxisTo => _axisTo;

        /// <summary>First axis position currently visible.</summary>
        public int ViewFrom => _viewFrom;

        /// <summary>Last axis position currently visible.</summary>
        public int ViewTo => _viewTo;

        /// <summary>
        /// Whether the value axis is logarithmic.
        /// </summary>
        /// <remarks>
        /// An exponential track spans several orders of magnitude, and on a linear axis
        /// everything below the last few positions is pressed flat against zero — the part
        /// a designer most needs to see is the part that disappears.
        /// </remarks>
        public bool LogScale
        {
            get => _logScale;
            set
            {
                if (_logScale == value) return;
                _logScale = value;
                RecalculateRange();
                MarkDirtyRepaint();
            }
        }

        /// <summary>Replaces the plotted series with a single line.</summary>
        public void SetValues(double[] values, int firstAxisPosition = 0)
        {
            SetSeries(new[]
            {
                new GraphSeries(null, values, firstAxisPosition, GraphSeries.ColorAt(0)),
            });
        }

        /// <summary>Replaces the plotted series.</summary>
        public void SetSeries(IReadOnlyList<GraphSeries> series)
        {
            _series.Clear();
            if (series != null)
            {
                foreach (var entry in series)
                {
                    if (entry != null && entry.Count > 0) _series.Add(entry);
                }
            }

            _hoverIndex = -1;
            RecalculateAxis();
            RecalculateRange();
            ResetView();
            MarkDirtyRepaint();
        }

        /// <summary>Replaces the set of axis positions drawn as pinned.</summary>
        public void SetPins(IEnumerable<int> axisPositions)
        {
            _pins.Clear();
            if (axisPositions != null)
            {
                foreach (var position in axisPositions) _pins.Add(position);
            }

            MarkDirtyRepaint();
        }

        /// <summary>The sample index under a local x coordinate, or -1 outside the plot.</summary>
        public int IndexAtLocalX(float localX) => IndexAt(localX);

        /// <summary>Reports the axis position under a local x coordinate.</summary>
        public void PickAt(float localX)
        {
            var index = IndexAt(localX);
            if (index >= 0) PositionPicked?.Invoke(AxisPositionAt(index));
        }

        /// <summary>Replaces the shaded spans.</summary>
        public void SetHighlights(IReadOnlyList<GraphHighlight> highlights)
        {
            _highlights.Clear();
            if (highlights != null) _highlights.AddRange(highlights);
            MarkDirtyRepaint();
        }

        /// <summary>Value of the primary series at an offset, for tooltips.</summary>
        public bool TryGetValue(int index, out double value)
        {
            value = 0d;
            if (_series.Count == 0) return false;

            var values = _series[0].Values;
            if (index < 0 || index >= values.Length) return false;

            value = values[index];
            return true;
        }

        /// <summary>Axis position of a sample in the primary series.</summary>
        public int AxisPositionAt(int index) => _axisFrom + index;

        /// <summary>The series being plotted, for legends.</summary>
        public IReadOnlyList<GraphSeries> Series => _series;

        // --- view ---------------------------------------------------------------------

        /// <summary>Shows the whole axis again.</summary>
        public void ResetView()
        {
            _viewFrom = _axisFrom;
            _viewTo = _axisTo;
            RecalculateRange();
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
        }

        /// <summary>Restricts the visible axis range, clamped to what exists.</summary>
        public void SetView(int from, int to)
        {
            if (_axisTo < _axisFrom) return;

            // Two positions is the floor: a single-position view has no line to draw and no
            // way back out by zooming.
            if (to - from < 1) to = from + 1;

            var span = Math.Min(to - from, _axisTo - _axisFrom);
            from = Mathf.Clamp(from, _axisFrom, _axisTo - span);

            _viewFrom = from;
            _viewTo = from + span;
            RecalculateRange();
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (_series.Count == 0) return;

            var plot = PlotRect;
            if (plot.width <= 1f) return;

            // WheelEvent reports panel coordinates rather than local ones.
            var localX = this.WorldToLocal(evt.mousePosition).x;
            var t = Mathf.Clamp01((localX - plot.xMin) / plot.width);
            var anchor = _viewFrom + t * (_viewTo - _viewFrom);

            // Zoom about the pointer rather than the centre so the position being examined
            // stays put.
            var factor = evt.delta.y > 0f ? 1.2f : 1f / 1.2f;
            var span = Math.Max(1d, (_viewTo - _viewFrom) * factor);

            var from = (int)Math.Round(anchor - t * span);
            SetView(from, from + (int)Math.Round(span));
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 2 && !evt.altKey) return;

            _panning = true;
            _panStart = evt.localPosition;
            _panViewFrom = _viewFrom;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_panning) return;

            _panning = false;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        // --- geometry -----------------------------------------------------------------

        private Rect PlotRect
        {
            get
            {
                var r = contentRect;
                return new Rect(
                    r.x + PaddingLeft,
                    r.y + PaddingTop,
                    Mathf.Max(1f, r.width - PaddingLeft - PaddingRight),
                    Mathf.Max(1f, r.height - PaddingTop - PaddingBottom));
            }
        }

        private void RecalculateAxis()
        {
            if (_series.Count == 0)
            {
                _axisFrom = 0;
                _axisTo = -1;
                return;
            }

            _axisFrom = int.MaxValue;
            _axisTo = int.MinValue;

            foreach (var series in _series)
            {
                if (series.FirstAxisPosition < _axisFrom) _axisFrom = series.FirstAxisPosition;
                if (series.LastAxisPosition > _axisTo) _axisTo = series.LastAxisPosition;
            }
        }

        private void RecalculateRange()
        {
            _hasNonFinite = false;
            _min = double.MaxValue;
            _max = double.MinValue;

            var finite = 0;
            foreach (var series in _series)
            {
                for (var i = 0; i < series.Values.Length; i++)
                {
                    var position = series.FirstAxisPosition + i;

                    // Bounds follow the visible range, so zooming into a quiet stretch
                    // actually shows its shape instead of a flat line under the peak. This
                    // span has to match exactly what gets drawn, or a sample outside the
                    // computed minimum and maximum normalises past 0..1 and lands beyond
                    // the plot rectangle.
                    if (position < _viewFrom || position > _viewTo) continue;

                    var v = series.Values[i];
                    if (double.IsNaN(v) || double.IsInfinity(v))
                    {
                        _hasNonFinite = true;
                        continue;
                    }

                    if (_logScale && v <= 0d) continue;

                    if (v < _min) _min = v;
                    if (v > _max) _max = v;
                    finite++;
                }
            }

            if (finite == 0)
            {
                _min = 0d;
                _max = 1d;
                return;
            }

            if (Math.Abs(_max - _min) < double.Epsilon) _max = _min + 1d;
        }

        private bool IsPlottable(GraphSeries series, int index)
        {
            var v = series.Values[index];
            if (double.IsNaN(v) || double.IsInfinity(v)) return false;
            return !_logScale || v > 0d;
        }

        private Vector2 PointFor(GraphSeries series, int index, Rect plot)
        {
            var position = series.FirstAxisPosition + index;
            var span = Math.Max(1, _viewTo - _viewFrom);
            var t = (float)((position - _viewFrom) / (double)span);

            return new Vector2(plot.xMin + t * plot.width, plot.yMax - Normalize(series.Values[index]) * plot.height);
        }

        private float Normalize(double value)
        {
            if (!_logScale) return (float)((value - _min) / (_max - _min));

            var min = Math.Log10(Math.Max(_min, LogFloor));
            var max = Math.Log10(Math.Max(_max, LogFloor * 10d));
            if (Math.Abs(max - min) < double.Epsilon) return 0f;

            return (float)((Math.Log10(Math.Max(value, LogFloor)) - min) / (max - min));
        }

        private float XFor(int axisPosition, Rect plot)
        {
            var span = Math.Max(1, _viewTo - _viewFrom);
            var t = (float)((axisPosition - _viewFrom) / (double)span);
            return plot.xMin + t * plot.width;
        }

        // --- interaction --------------------------------------------------------------

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_panning)
            {
                var plot = PlotRect;
                var span = _viewTo - _viewFrom;
                var perPixel = span / Math.Max(1f, plot.width);
                var moved = (int)Math.Round((_panStart.x - evt.localPosition.x) * perPixel);
                SetView(_panViewFrom + moved, _panViewFrom + moved + span);
                return;
            }

            SetHoverIndex(IndexAt(evt.localPosition.x));
        }

        private int IndexAt(float localX)
        {
            if (_series.Count == 0) return -1;

            var plot = PlotRect;
            if (localX < plot.xMin || localX > plot.xMax) return -1;

            var t = (localX - plot.xMin) / plot.width;
            var position = (int)Math.Round(_viewFrom + t * (_viewTo - _viewFrom));

            var index = position - _series[0].FirstAxisPosition;
            return Mathf.Clamp(index, 0, Math.Max(0, _series[0].Count - 1));
        }

        private void SetHoverIndex(int index)
        {
            if (index == _hoverIndex) return;
            _hoverIndex = index;
            HoverIndexChanged?.Invoke(index);
            MarkDirtyRepaint();
        }

        // --- painting -----------------------------------------------------------------

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var plot = PlotRect;
            if (plot.width <= 1f || plot.height <= 1f) return;

            var painter = ctx.painter2D;

            DrawHighlights(painter, plot);
            DrawGrid(painter, plot);
            DrawAxes(painter, plot);

            if (_series.Count == 0) return;

            foreach (var series in _series) DrawSeries(painter, series, plot);
            DrawPins(painter, plot);
            DrawHoverMarker(painter, plot);
        }

        private void DrawHighlights(Painter2D painter, Rect plot)
        {
            foreach (var highlight in _highlights)
            {
                if (highlight.To < _viewFrom || highlight.From > _viewTo) continue;

                var from = Mathf.Max(plot.xMin, XFor(Math.Max(highlight.From, _viewFrom), plot));
                var to = Mathf.Min(plot.xMax, XFor(Math.Min(highlight.To, _viewTo), plot));

                // A one-position finding would otherwise be a zero-width rectangle.
                if (to - from < 2f) to = from + 2f;

                painter.fillColor = highlight.Color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(from, plot.yMin));
                painter.LineTo(new Vector2(to, plot.yMin));
                painter.LineTo(new Vector2(to, plot.yMax));
                painter.LineTo(new Vector2(from, plot.yMax));
                painter.ClosePath();
                painter.Fill();
            }
        }

        private static void DrawGrid(Painter2D painter, Rect plot)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.06f);
            painter.lineWidth = 1f;

            const int divisions = 4;
            painter.BeginPath();
            for (var i = 1; i < divisions; i++)
            {
                var y = plot.yMin + plot.height * i / divisions;
                painter.MoveTo(new Vector2(plot.xMin, y));
                painter.LineTo(new Vector2(plot.xMax, y));
            }
            painter.Stroke();
        }

        private static void DrawAxes(Painter2D painter, Rect plot)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(plot.xMin, plot.yMin));
            painter.LineTo(new Vector2(plot.xMin, plot.yMax));
            painter.LineTo(new Vector2(plot.xMax, plot.yMax));
            painter.Stroke();
        }

        /// <summary>
        /// The samples of a series that fall inside the visible range.
        /// </summary>
        /// <remarks>
        /// Plotting the whole series while zoomed would place off-view samples far outside
        /// the plot rectangle, and painter2D geometry is not bounded by the element.
        ///
        /// No padding beyond the range: the view bounds are axis positions and samples sit
        /// on them, so the first and last visible samples already land exactly on the left
        /// and right edges. Reaching one sample further would drag values outside the
        /// computed minimum and maximum into the picture, normalise them past 0..1, and
        /// draw them above or below the plot.
        /// </remarks>
        internal bool TryGetVisibleIndexRange(GraphSeries series, out int from, out int to)
        {
            from = 0;
            to = -1;
            if (series == null || series.Count == 0) return false;

            from = Math.Max(0, _viewFrom - series.FirstAxisPosition);
            to = Math.Min(series.Count - 1, _viewTo - series.FirstAxisPosition);
            return to >= from;
        }

        private void DrawSeries(Painter2D painter, GraphSeries series, Rect plot)
        {
            if (!TryGetVisibleIndexRange(series, out var first, out var last)) return;

            painter.strokeColor = series.Color;
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            var visible = Math.Max(1, _viewTo - _viewFrom);
            var step = Mathf.Max(1, Mathf.CeilToInt(visible * MinPixelsPerSample / plot.width));

            painter.BeginPath();

            // The path breaks wherever a sample has no value, so a gap reads as "undefined
            // here" instead of a straight line drawn through nothing.
            var penDown = false;
            var i = first;
            for (; i <= last; i += step) penDown = Plot(painter, series, i, plot, penDown);

            // The decimation stride can overshoot the last visible sample; the endpoint
            // matters visually.
            if (i - step != last) Plot(painter, series, last, plot, penDown);

            painter.Stroke();
        }

        private bool Plot(Painter2D painter, GraphSeries series, int index, Rect plot, bool penDown)
        {
            if (!IsPlottable(series, index)) return false;

            var point = PointFor(series, index, plot);
            if (penDown) painter.LineTo(point);
            else painter.MoveTo(point);

            return true;
        }

        /// <summary>
        /// Marks the positions whose value was set by hand rather than by the curve.
        /// </summary>
        /// <remarks>
        /// Without them a pinned point is invisible: the line passes through it and looks
        /// like every other point, so a curve that has been quietly overruled in four places
        /// reads as one the formula produced.
        /// </remarks>
        private void DrawPins(Painter2D painter, Rect plot)
        {
            if (_pins.Count == 0 || _series.Count == 0) return;

            var series = _series[0];

            painter.fillColor = new Color(1f, 0.78f, 0.32f, 0.95f);

            foreach (var axisPosition in _pins)
            {
                if (axisPosition < _viewFrom || axisPosition > _viewTo) continue;

                var index = axisPosition - _axisFrom;
                if (!IsPlottable(series, index)) continue;

                var point = PointFor(series, index, plot);

                // A diamond, so it stays distinguishable from the round hover marker even
                // when the two land on the same point.
                const float r = 4f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(point.x, point.y - r));
                painter.LineTo(new Vector2(point.x + r, point.y));
                painter.LineTo(new Vector2(point.x, point.y + r));
                painter.LineTo(new Vector2(point.x - r, point.y));
                painter.ClosePath();
                painter.Fill();
            }
        }

        private void DrawHoverMarker(Painter2D painter, Rect plot)
        {
            var primary = _series[0];
            if (_hoverIndex < 0 || _hoverIndex >= primary.Count) return;

            var hovered = primary.FirstAxisPosition + _hoverIndex;
            if (hovered < _viewFrom || hovered > _viewTo) return;

            var x = XFor(hovered, plot);

            painter.strokeColor = new Color(1f, 1f, 1f, 0.25f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, plot.yMin));
            painter.LineTo(new Vector2(x, plot.yMax));
            painter.Stroke();

            // A dot per series, so a hover reads every line at once rather than only the
            // one that happens to be first.
            foreach (var series in _series)
            {
                var index = primary.FirstAxisPosition + _hoverIndex - series.FirstAxisPosition;
                if (index < 0 || index >= series.Count) continue;
                if (!IsPlottable(series, index)) continue;

                painter.fillColor = series.Color;
                painter.BeginPath();
                painter.Arc(PointFor(series, index, plot), 3.5f, 0f, 360f);
                painter.Fill();
            }
        }
    }
}
