using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Scales the whole track by a constant, optionally one defined on the profile.
    /// </summary>
    /// <remarks>
    /// The point of the global key is that a difficulty knob should live in one place. A
    /// hard mode that multiplies every enemy stat by 1.3 is one number a designer edits,
    /// not thirty tracks they have to remember to keep in sync.
    ///
    /// A missing global resolves to 1 rather than 0. Half-configured profiles are normal
    /// while authoring, and zeroing an entire track would look like a bug in the formula
    /// rather than an unset constant.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Scale everything", "One multiplier over the whole track, optionally a profile-wide one such as difficulty.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(MultiplierProcessor))]
    public sealed class MultiplierProcessor : PointwisePostProcessor
    {
        [SerializeField] private double _factor = 1d;
        [SerializeField] private string _globalKey;

        public MultiplierProcessor() { }

        public MultiplierProcessor(double factor, string globalKey = null)
        {
            _factor = factor;
            _globalKey = globalKey;
        }

        /// <summary>Literal multiplier, applied before any global.</summary>
        public double Factor
        {
            get => _factor;
            set => _factor = value;
        }

        /// <summary>
        /// Optional profile global multiplied in on top of <see cref="Factor"/>.
        /// Empty means "literal only".
        /// </summary>
        public string GlobalKey
        {
            get => _globalKey;
            set => _globalKey = value;
        }

        protected override double ProcessOne(double value, int index, EvalContext context)
        {
            var factor = _factor;
            if (!string.IsNullOrEmpty(_globalKey))
            {
                factor *= context.GetGlobal(_globalKey, 1d);
            }

            return value * factor;
        }

        public override string Describe()
        {
            var literal = "× " + FormulaText.Number(_factor);
            return string.IsNullOrEmpty(_globalKey) ? literal : $"{literal} × {_globalKey}";
        }
    }
}
