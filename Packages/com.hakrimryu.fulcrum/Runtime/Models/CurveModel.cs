using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Wraps an <see cref="AnimationCurve"/> so a curve drawn by hand can drive a track.
    /// </summary>
    /// <remarks>
    /// Some shapes are easier to feel than to write. This model exists for the cases where
    /// a designer knows what the curve should look like but not which formula produces it.
    ///
    /// Two consequences worth knowing. <see cref="AnimationCurve"/> is single precision, so
    /// values round-trip through <c>float</c> and lose precision well before the ranges an
    /// exponential track reaches. And it clamps to its end keys rather than extrapolating,
    /// so a curve authored over 0..1 returns its last key for every larger <c>x</c> — which
    /// is why <see cref="TimeScale"/> exists to map the track's range onto the curve's.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Drawn by hand", "You draw the shape. For when you know how it should feel but not how to write it.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(CurveModel))]
    public sealed class CurveModel : GrowthModel
    {
        [Tooltip("The shape. Click to open the curve editor.")]
        [SerializeField] private AnimationCurve _curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Divides the axis position before sampling. Set it to the track length to draw over 0..1.")]
        [SerializeField] private double _timeScale = 1d;

        [Tooltip("Multiplies the sampled value, so the curve can describe shape alone.")]
        [FulcrumRange(1d, 10000d)]
        [SerializeField] private double _valueScale = 1d;

        public CurveModel() { }

        public CurveModel(AnimationCurve curve, double timeScale = 1d, double valueScale = 1d)
        {
            _curve = curve;
            _timeScale = timeScale;
            _valueScale = valueScale;
        }

        /// <summary>The authored shape. Null is treated as a flat zero.</summary>
        public AnimationCurve Curve
        {
            get => _curve;
            set => _curve = value;
        }

        /// <summary>
        /// Divides <c>x</c> before sampling, mapping a track range onto the curve's time
        /// range. Set it to the track's length to author over 0..1.
        /// </summary>
        public double TimeScale
        {
            get => _timeScale;
            set => _timeScale = value;
        }

        /// <summary>Multiplies the sampled value, so the curve can describe shape alone.</summary>
        public double ValueScale
        {
            get => _valueScale;
            set => _valueScale = value;
        }

        /// <summary>Number of keyframes. Zero means the curve evaluates to zero everywhere.</summary>
        public int KeyCount => _curve?.length ?? 0;

        public override double Evaluate(double x, EvalContext context)
        {
            if (_curve == null || _curve.length == 0) return 0d;

            var time = _timeScale == 0d ? x : x / _timeScale;
            return _curve.Evaluate((float)time) * _valueScale;
        }

        public override string Describe()
        {
            var keys = KeyCount;
            if (keys == 0) return "curve (empty)";

            var shape = $"curve ({keys} {(keys == 1 ? "key" : "keys")})";
            return FormulaText.Scaled(_valueScale, shape);
        }
    }
}
