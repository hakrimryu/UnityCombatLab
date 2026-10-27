using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// A power curve: <c>scale · x^exponent + offset</c>.
    /// </summary>
    /// <remarks>
    /// The usual choice for monster stats. An exponent slightly above 1 gives growth that
    /// accelerates gently, which reads as "getting harder" without the runaway of a true
    /// exponential.
    ///
    /// <c>Math.Pow</c> is undefined for a negative base with a fractional exponent and
    /// returns NaN. Rather than hide that behind a fallback, the value is passed through:
    /// tracks normally start at 1, and clamping belongs in the post-processing chain where
    /// the designer can see it.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Gets steeper", "Climbs faster and faster, but gently. The usual choice for monster stats.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(PowerModel))]
    public sealed class PowerModel : GrowthModel
    {
        [Tooltip("Overall size. Roughly the value at position 1.")]
        [FulcrumRange(1d, 1000d)]
        [SerializeField] private double _scale = 1d;

        [Tooltip("How quickly it steepens. 1 is a straight line; 1.3 to 1.5 climbs noticeably; above 2 runs away fast.")]
        [FulcrumRange(0.5d, 3d)]
        [SerializeField] private double _exponent = 1d;

        [Tooltip("Added afterwards. Useful as a floor for the earliest positions.")]
        [FulcrumRange(0d, 1000d)]
        [SerializeField] private double _offset;

        public PowerModel() { }

        public PowerModel(double scale, double exponent, double offset = 0d)
        {
            _scale = scale;
            _exponent = exponent;
            _offset = offset;
        }

        /// <summary>Multiplier applied to the powered term.</summary>
        public double Scale
        {
            get => _scale;
            set => _scale = value;
        }

        /// <summary>Exponent applied to <c>x</c>. Values just above 1 curve gently upward.</summary>
        public double Exponent
        {
            get => _exponent;
            set => _exponent = value;
        }

        /// <summary>Constant added after scaling, useful as a floor for early stages.</summary>
        public double Offset
        {
            get => _offset;
            set => _offset = value;
        }

        public override double Evaluate(double x, EvalContext context)
            => _scale * Math.Pow(x, _exponent) + _offset;

        public override string Describe()
            => FormulaText.Scaled(_scale, "x^" + FormulaText.Number(_exponent))
             + FormulaText.Addend(_offset);
    }
}
