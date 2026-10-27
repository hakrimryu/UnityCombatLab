using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>How <see cref="RoundProcessor"/> reduces precision.</summary>
    public enum RoundingMode
    {
        /// <summary>Round to the nearest step, halves away from zero.</summary>
        Nearest = 0,

        /// <summary>Round toward negative infinity.</summary>
        Floor = 1,

        /// <summary>Round toward positive infinity.</summary>
        Ceiling = 2,

        /// <summary>Keep a fixed count of leading digits, e.g. <c>12847 → 12800</c>.</summary>
        SignificantDigits = 3,
    }

    /// <summary>
    /// Reduces precision so values read as deliberate rather than computed.
    /// </summary>
    /// <remarks>
    /// A monster with 12,847 HP looks like the output of a spreadsheet. One with 12,800
    /// looks like a decision. Significant-digit rounding is the mode that holds up across
    /// a whole track, because it keeps the same number of meaningful digits whether the
    /// value is in the hundreds or the millions.
    ///
    /// Halves round away from zero rather than to even. Banker's rounding is correct for
    /// accounting and surprising for game numbers.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Round", "Makes numbers read as decisions rather than output. 12,847 becomes 12,800.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(RoundProcessor))]
    public sealed class RoundProcessor : PointwisePostProcessor
    {
        [SerializeField] private RoundingMode _mode = RoundingMode.Nearest;
        [SerializeField] private int _decimals;
        [SerializeField] private int _significantDigits = 3;

        public RoundProcessor() { }

        public RoundProcessor(RoundingMode mode, int amount = 0)
        {
            _mode = mode;
            if (mode == RoundingMode.SignificantDigits) _significantDigits = amount;
            else _decimals = amount;
        }

        /// <summary>Which rounding rule to apply.</summary>
        public RoundingMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>
        /// Decimal places kept by <see cref="RoundingMode.Nearest"/>,
        /// <see cref="RoundingMode.Floor"/> and <see cref="RoundingMode.Ceiling"/>.
        /// Zero yields whole numbers; negative values round to tens, hundreds and so on.
        /// </summary>
        public int Decimals
        {
            get => _decimals;
            set => _decimals = value;
        }

        /// <summary>Leading digits kept by <see cref="RoundingMode.SignificantDigits"/>.</summary>
        public int SignificantDigits
        {
            get => _significantDigits;
            set => _significantDigits = value;
        }

        protected override double ProcessOne(double value, int index, EvalContext context)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value;

            switch (_mode)
            {
                case RoundingMode.SignificantDigits:
                    return ToSignificantDigits(value, _significantDigits);

                case RoundingMode.Floor:
                    return ApplyAtScale(value, _decimals, Math.Floor);

                case RoundingMode.Ceiling:
                    return ApplyAtScale(value, _decimals, Math.Ceiling);

                default:
                    return ApplyAtScale(value, _decimals,
                        v => Math.Round(v, MidpointRounding.AwayFromZero));
            }
        }

        private static double ApplyAtScale(double value, int decimals, Func<double, double> op)
        {
            // Math.Round only accepts 0..15 decimals and cannot round to tens or hundreds,
            // so the scaling is done here for every mode uniformly.
            var scale = Math.Pow(10d, decimals);
            return op(value * scale) / scale;
        }

        private static double ToSignificantDigits(double value, int digits)
        {
            if (value == 0d || digits <= 0) return value;

            var magnitude = Math.Floor(Math.Log10(Math.Abs(value))) + 1d - digits;
            var scale = Math.Pow(10d, magnitude);
            return scale * Math.Round(value / scale, MidpointRounding.AwayFromZero);
        }

        public override string Describe()
        {
            switch (_mode)
            {
                case RoundingMode.SignificantDigits: return $"round {_significantDigits}sf";
                case RoundingMode.Floor: return _decimals == 0 ? "floor" : $"floor {_decimals}dp";
                case RoundingMode.Ceiling: return _decimals == 0 ? "ceil" : $"ceil {_decimals}dp";
                default: return _decimals == 0 ? "round" : $"round {_decimals}dp";
            }
        }
    }
}
