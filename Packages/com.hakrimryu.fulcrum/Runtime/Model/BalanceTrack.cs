using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// One named curve within a profile — an axis, a range, the model that fills it, and
    /// the steps that turn its output into shippable numbers.
    /// </summary>
    /// <remarks>
    /// The range stored here is what gets evaluated. An axis supplies a canonical domain
    /// that seeds new tracks and gives validation something to compare against, but a
    /// track is free to cover only part of it.
    /// </remarks>
    [Serializable]
    public sealed class BalanceTrack
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _axisId;
        [SerializeField] private int _rangeMin = 1;
        [SerializeField] private int _rangeMax = 100;
        [SerializeField] private bool _accumulate;
        [SerializeReference] private GrowthModel _model;
        [SerializeReference] private List<PostProcessor> _postProcessors = new List<PostProcessor>();
        [SerializeField] private List<ManualOverride> _overrides = new List<ManualOverride>();

        public BalanceTrack() { }

        public BalanceTrack(string id, GrowthModel model = null)
        {
            _id = id;
            _model = model;
        }

        public BalanceTrack(string id, string axisId, int rangeMin, int rangeMax, GrowthModel model = null)
        {
            _id = id;
            _axisId = axisId;
            _rangeMin = rangeMin;
            _rangeMax = rangeMax;
            _model = model;
        }

        /// <summary>Stable key other tracks and exports refer to, such as <c>monster_hp</c>.</summary>
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>Label shown in the track list. Falls back to <see cref="Id"/> when unset.</summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
            set => _displayName = value;
        }

        /// <summary>
        /// The curve backing this track. Null is a valid authoring state — a track can exist
        /// before its formula is chosen — but it also means a broken
        /// <c>[SerializeReference]</c>, so callers should surface it rather than skip it.
        /// </summary>
        public GrowthModel Model
        {
            get => _model;
            set => _model = value;
        }

        /// <summary>Whether this track can currently produce values.</summary>
        public bool HasModel => _model != null;

        /// <summary>
        /// Steps applied to the evaluated series, in order. Order is configuration:
        /// rounding before a minimum-step rule gives different numbers than after it.
        /// </summary>
        public List<PostProcessor> PostProcessors
        {
            get => _postProcessors ?? (_postProcessors = new List<PostProcessor>());
            set => _postProcessors = value ?? new List<PostProcessor>();
        }

        /// <summary>Which <see cref="AxisDefinition"/> this track is defined over.</summary>
        public string AxisId
        {
            get => _axisId;
            set => _axisId = value;
        }

        /// <summary>First axis position evaluated.</summary>
        public int RangeMin
        {
            get => _rangeMin;
            set => _rangeMin = value;
        }

        /// <summary>Last axis position evaluated, inclusive.</summary>
        public int RangeMax
        {
            get => _rangeMax;
            set => _rangeMax = value;
        }

        /// <summary>Number of positions evaluated, or zero when the range is inverted.</summary>
        public int Length => _rangeMax < _rangeMin ? 0 : _rangeMax - _rangeMin + 1;

        /// <summary>
        /// Converts an axis position to an offset into an evaluated series,
        /// or -1 when the position falls outside this track.
        /// </summary>
        public int IndexOfAxisPosition(int axisPosition)
        {
            if (axisPosition < _rangeMin || axisPosition > _rangeMax) return -1;
            return axisPosition - _rangeMin;
        }

        /// <summary>Converts an offset into an evaluated series back to an axis position.</summary>
        public int AxisPositionAt(int index) => _rangeMin + index;

        /// <summary>
        /// Whether each value is the running total of everything up to it.
        /// </summary>
        /// <remarks>
        /// Cumulative required experience is the usual case: the designer tunes the cost of
        /// each single level, and the total to reach a level is what falls out of that.
        /// Keeping it a flag rather than a separate model type avoids multiplying every
        /// curve shape by two.
        /// </remarks>
        public bool Accumulate
        {
            get => _accumulate;
            set => _accumulate = value;
        }

        /// <summary>Positions pinned to hand-chosen values, keyed by axis position.</summary>
        public List<ManualOverride> Overrides
        {
            get => _overrides ?? (_overrides = new List<ManualOverride>());
            set => _overrides = value ?? new List<ManualOverride>();
        }

        /// <summary>
        /// Finds the override for an axis position, if one is set. When several share a
        /// position the last one wins, matching how the list reads top to bottom.
        /// </summary>
        public bool TryGetOverride(int axisPosition, out ManualOverride result)
        {
            result = null;
            var overrides = _overrides;
            if (overrides == null) return false;

            for (var i = 0; i < overrides.Count; i++)
            {
                var candidate = overrides[i];
                if (candidate != null && candidate.Index == axisPosition) result = candidate;
            }

            return result != null;
        }
    }
}
