using System;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// Pins one position of a track to a value the formula did not produce.
    /// </summary>
    /// <remarks>
    /// Some numbers are decisions, not derivations — a boss stage that has to feel like a
    /// wall, a first upgrade priced to be affordable immediately. Rather than distorting a
    /// curve until it happens to pass through those points, they are stated outright.
    ///
    /// <see cref="Index"/> is a position on the axis, not an offset into the evaluated
    /// array. A track running stages 10 to 50 takes an override at 42, not at 32.
    ///
    /// <see cref="Note"/> exists for the same reason the profile has one: an unexplained
    /// hand-set number is indistinguishable from a mistake six months later.
    /// </remarks>
    [Serializable]
    public sealed class ManualOverride
    {
        [SerializeField] private int _index;
        [SerializeField] private double _value;
        [SerializeField] private string _note;

        public ManualOverride() { }

        public ManualOverride(int index, double value, string note = null)
        {
            _index = index;
            _value = value;
            _note = note;
        }

        /// <summary>Axis position this override applies to.</summary>
        public int Index
        {
            get => _index;
            set => _index = value;
        }

        /// <summary>Value used instead of the evaluated one.</summary>
        public double Value
        {
            get => _value;
            set => _value = value;
        }

        /// <summary>Why this position was pinned.</summary>
        public string Note
        {
            get => _note;
            set => _note = value;
        }
    }
}
