using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Holds values within a floor and a ceiling.
    /// </summary>
    /// <remarks>
    /// Two jobs, usually. Keeping a quantity meaningful — HP below 1 is not a weaker
    /// monster, it is a broken one — and keeping a probability inside 0..1 where the rest
    /// of the game assumes it lives.
    ///
    /// Each bound is opted into separately rather than inferred from a sentinel value,
    /// because "no ceiling" and "ceiling of zero" are both things a designer means.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Keep within bounds", "A floor, a ceiling, or both. Health below 1 is broken, not weak.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(ClampProcessor))]
    public sealed class ClampProcessor : PointwisePostProcessor
    {
        [SerializeField] private bool _hasMin = true;
        [SerializeField] private double _min = 1d;
        [SerializeField] private bool _hasMax;
        [SerializeField] private double _max = 1d;

        public ClampProcessor() { }

        private ClampProcessor(bool hasMin, double min, bool hasMax, double max)
        {
            _hasMin = hasMin;
            _min = min;
            _hasMax = hasMax;
            _max = max;
        }

        /// <summary>A floor only, such as "HP is at least 1".</summary>
        public static ClampProcessor AtLeast(double min) => new ClampProcessor(true, min, false, 0d);

        /// <summary>A ceiling only.</summary>
        public static ClampProcessor AtMost(double max) => new ClampProcessor(false, 0d, true, max);

        /// <summary>Both bounds, such as a probability held inside 0..1.</summary>
        public static ClampProcessor Between(double min, double max)
            => new ClampProcessor(true, min, true, max);

        /// <summary>Whether the floor applies.</summary>
        public bool HasMin
        {
            get => _hasMin;
            set => _hasMin = value;
        }

        /// <summary>Lowest permitted value, when <see cref="HasMin"/>.</summary>
        public double Min
        {
            get => _min;
            set => _min = value;
        }

        /// <summary>Whether the ceiling applies.</summary>
        public bool HasMax
        {
            get => _hasMax;
            set => _hasMax = value;
        }

        /// <summary>Highest permitted value, when <see cref="HasMax"/>.</summary>
        public double Max
        {
            get => _max;
            set => _max = value;
        }

        protected override double ProcessOne(double value, int index, EvalContext context)
        {
            // NaN compares false against everything, so it would slip through an ordinary
            // clamp untouched. A bounded track has no use for it; snap it to whichever
            // bound exists so a broken formula shows up as a flat line rather than a gap.
            if (double.IsNaN(value))
            {
                if (_hasMin) return _min;
                if (_hasMax) return _max;
                return value;
            }

            if (_hasMin && value < _min) return _min;
            if (_hasMax && value > _max) return _max;
            return value;
        }

        public override string Describe()
        {
            if (_hasMin && _hasMax) return $"clamp {FormulaText.Number(_min)}..{FormulaText.Number(_max)}";
            if (_hasMin) return $"clamp ≥ {FormulaText.Number(_min)}";
            if (_hasMax) return $"clamp ≤ {FormulaText.Number(_max)}";
            return "clamp (unbounded)";
        }
    }
}
