using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// One span of a <see cref="PiecewiseModel"/>: where it starts and which curve it uses.
    /// </summary>
    [Serializable]
    [FulcrumModel("Segment", "One span of a piecewise curve.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(PiecewiseSegment))]
    public sealed class PiecewiseSegment
    {
        [SerializeField] private double _from;
        [SerializeReference] private GrowthModel _model;

        public PiecewiseSegment() { }

        public PiecewiseSegment(double from, GrowthModel model)
        {
            _from = from;
            _model = model;
        }

        /// <summary>Axis position where this segment takes over, inclusive.</summary>
        public double From
        {
            get => _from;
            set => _from = value;
        }

        /// <summary>Curve used within this segment. Null evaluates to zero.</summary>
        public GrowthModel Model
        {
            get => _model;
            set => _model = value;
        }
    }

    /// <summary>
    /// Switches between curves at fixed breakpoints.
    /// </summary>
    /// <remarks>
    /// Progression rarely follows one formula end to end. A chapter break, a difficulty
    /// tier, or a level cap all change the shape, and forcing a single curve through those
    /// transitions distorts both sides of the boundary.
    ///
    /// The segment whose <see cref="PiecewiseSegment.From"/> is the greatest value not
    /// exceeding <c>x</c> wins, so segments need not be stored in order. Below the earliest
    /// breakpoint the earliest segment applies — a gap at the start reflects an unfinished
    /// edit rather than an intent to return nothing.
    ///
    /// Segments hold their own <c>[SerializeReference]</c> models, so a piecewise model can
    /// nest arbitrarily. That works, but it also means a rename breaks references at any
    /// depth, not just the top level.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Changes at a boundary", "A different curve after each breakpoint. For chapter boundaries and difficulty tiers.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(PiecewiseModel))]
    public sealed class PiecewiseModel : GrowthModel
    {
        private const int MaxDescribedBreakpoints = 4;

        [SerializeField] private List<PiecewiseSegment> _segments = new List<PiecewiseSegment>();

        public PiecewiseModel() { }

        public PiecewiseModel(params PiecewiseSegment[] segments)
        {
            _segments = new List<PiecewiseSegment>(segments ?? Array.Empty<PiecewiseSegment>());
        }

        /// <summary>The spans making up this curve. Order is not significant.</summary>
        public List<PiecewiseSegment> Segments
        {
            get => _segments ?? (_segments = new List<PiecewiseSegment>());
            set => _segments = value ?? new List<PiecewiseSegment>();
        }

        public override double Evaluate(double x, EvalContext context)
        {
            var chosen = SegmentFor(x);
            return chosen?.Model?.Evaluate(x, context) ?? 0d;
        }

        public override void CollectTrackReferences(ICollection<string> into)
        {
            var segments = _segments;
            if (segments == null) return;

            // Every segment counts, not just the one that happens to govern a given x:
            // the track depends on whatever any part of its range might read.
            foreach (var segment in segments)
            {
                segment?.Model?.CollectTrackReferences(into);
            }
        }

        /// <summary>
        /// Resolves which segment governs <paramref name="x"/>, or null when there are none.
        /// Exposed so the editor can highlight the active span.
        /// </summary>
        public PiecewiseSegment SegmentFor(double x)
        {
            var segments = _segments;
            if (segments == null || segments.Count == 0) return null;

            PiecewiseSegment chosen = null;
            PiecewiseSegment earliest = null;

            foreach (var segment in segments)
            {
                if (segment == null) continue;

                if (earliest == null || segment.From < earliest.From)
                {
                    earliest = segment;
                }

                if (segment.From <= x && (chosen == null || segment.From > chosen.From))
                {
                    chosen = segment;
                }
            }

            return chosen ?? earliest;
        }

        public override string Describe()
        {
            var segments = _segments;
            var count = 0;
            if (segments != null)
            {
                foreach (var segment in segments)
                {
                    if (segment != null) count++;
                }
            }

            if (count == 0) return "piecewise (empty)";

            var starts = new List<double>(count);
            foreach (var segment in segments)
            {
                if (segment != null) starts.Add(segment.From);
            }
            starts.Sort();

            var sb = new StringBuilder();
            sb.Append("piecewise (").Append(count).Append(" segments from ");
            var shown = Math.Min(starts.Count, MaxDescribedBreakpoints);
            for (var i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(FormulaText.Number(starts[i]));
            }
            if (starts.Count > shown) sb.Append(", …");
            sb.Append(')');
            return sb.ToString();
        }
    }
}
