using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// A straight line: <c>slope · x + intercept</c>.
    /// </summary>
    /// <remarks>
    /// Suited to values that should grow by a fixed amount per step, where the designer
    /// wants the increment itself to be the tuning knob.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Straight line", "Grows by a fixed amount each step. The increment itself is what you tune.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(LinearModel))]
    public sealed class LinearModel : GrowthModel
    {
        [Tooltip("How much the value climbs per step along the axis.")]
        [FulcrumRange(-100d, 1000d)]
        [SerializeField] private double _slope = 1d;

        [Tooltip("Where the line starts, at position zero.")]
        [FulcrumRange(0d, 10000d)]
        [SerializeField] private double _intercept;

        public LinearModel() { }

        public LinearModel(double slope, double intercept = 0d)
        {
            _slope = slope;
            _intercept = intercept;
        }

        /// <summary>Change in value per unit of <c>x</c>.</summary>
        public double Slope
        {
            get => _slope;
            set => _slope = value;
        }

        /// <summary>Value at <c>x = 0</c>.</summary>
        public double Intercept
        {
            get => _intercept;
            set => _intercept = value;
        }

        public override double Evaluate(double x, EvalContext context)
            => _slope * x + _intercept;

        public override string Describe()
            => FormulaText.Scaled(_slope, "x") + FormulaText.Addend(_intercept);
    }
}
