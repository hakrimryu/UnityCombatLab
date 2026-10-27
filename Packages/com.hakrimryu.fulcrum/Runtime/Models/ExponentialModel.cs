using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Compound growth: <c>scale · ratio^x</c>.
    /// </summary>
    /// <remarks>
    /// Fits upgrade costs and late-game curves, where each step should cost a fixed
    /// percentage more than the last rather than a fixed amount.
    ///
    /// Growth is unbounded by construction — a ratio of 1.15 over 200 stages is roughly
    /// 10^12 — so an exponential track almost always wants a clamp or a rounding step
    /// after it. That is the post-processing chain's job, not this model's.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Compounds", "Each step costs a fixed percentage more than the last. Fits upgrade costs.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(ExponentialModel))]
    public sealed class ExponentialModel : GrowthModel
    {
        [Tooltip("Starting value, at position zero.")]
        [FulcrumRange(1d, 10000d)]
        [SerializeField] private double _scale = 1d;

        [Tooltip("Multiplier per step. 1.15 means 15% more each time. Small changes here matter enormously over a long axis.")]
        [FulcrumRange(1.01d, 1.5d)]
        [SerializeField] private double _ratio = 1.1d;

        public ExponentialModel() { }

        public ExponentialModel(double scale, double ratio)
        {
            _scale = scale;
            _ratio = ratio;
        }

        /// <summary>Value at <c>x = 0</c>.</summary>
        public double Scale
        {
            get => _scale;
            set => _scale = value;
        }

        /// <summary>Multiplier applied per unit of <c>x</c>. 1.15 means "15% more each step".</summary>
        public double Ratio
        {
            get => _ratio;
            set => _ratio = value;
        }

        public override double Evaluate(double x, EvalContext context)
            => _scale * Math.Pow(_ratio, x);

        public override string Describe()
            => FormulaText.Scaled(_scale, FormulaText.Number(_ratio) + "^x");
    }
}
