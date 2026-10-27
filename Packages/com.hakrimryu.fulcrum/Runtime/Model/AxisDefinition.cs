using System;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// A named input dimension that tracks are defined over, such as stage or level.
    /// </summary>
    /// <remarks>
    /// Axes are data rather than a fixed enumeration. Every project counts progression
    /// differently — equipment tiers, guild ranks, ascension steps — and a built-in list
    /// would force anyone with a different vocabulary to fork the package.
    ///
    /// The range here is the axis's canonical domain. It seeds new tracks and gives
    /// validation something to compare against, but each track carries its own range and
    /// that range is what gets evaluated.
    /// </remarks>
    [Serializable]
    public sealed class AxisDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private int _min = 1;
        [SerializeField] private int _max = 100;

        public AxisDefinition() { }

        public AxisDefinition(string id, int min, int max, string displayName = null)
        {
            _id = id;
            _min = min;
            _max = max;
            _displayName = displayName;
        }

        /// <summary>Stable key tracks refer to, such as <c>stage</c>.</summary>
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>Label shown in the editor. Falls back to <see cref="Id"/> when unset.</summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
            set => _displayName = value;
        }

        /// <summary>First position on the axis.</summary>
        public int Min
        {
            get => _min;
            set => _min = value;
        }

        /// <summary>Last position on the axis, inclusive.</summary>
        public int Max
        {
            get => _max;
            set => _max = value;
        }

        /// <summary>Number of positions, or zero when the range is inverted.</summary>
        public int Length => _max < _min ? 0 : _max - _min + 1;
    }
}
