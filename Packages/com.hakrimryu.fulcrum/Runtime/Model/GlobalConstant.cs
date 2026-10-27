using System;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// A named constant shared by every track in a profile.
    /// </summary>
    /// <remarks>
    /// A difficulty knob that multiplies every enemy stat should be one number a designer
    /// edits, not the same figure copied into thirty tracks and kept in sync by memory.
    /// Steps such as <see cref="MultiplierProcessor"/> read these by key.
    ///
    /// Stored as a list rather than a dictionary because Unity does not serialise
    /// dictionaries; <see cref="BalanceProfile.BuildGlobals"/> turns it into one.
    /// </remarks>
    [Serializable]
    public sealed class GlobalConstant
    {
        [SerializeField] private string _key;
        [SerializeField] private double _value;
        [SerializeField] private string _note;

        public GlobalConstant() { }

        public GlobalConstant(string key, double value, string note = null)
        {
            _key = key;
            _value = value;
            _note = note;
        }

        /// <summary>Name referenced from processors and expressions.</summary>
        public string Key
        {
            get => _key;
            set => _key = value;
        }

        public double Value
        {
            get => _value;
            set => _value = value;
        }

        /// <summary>What this constant represents.</summary>
        public string Note
        {
            get => _note;
            set => _note = value;
        }
    }
}
