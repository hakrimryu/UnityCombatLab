using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Explicit values, one per step, starting at <see cref="StartX"/>.
    /// </summary>
    /// <remarks>
    /// Early stages are usually hand-tuned rather than derived — a tutorial wants the
    /// numbers a designer picked, not whatever a formula produces. This model exists so
    /// those values stay first-class instead of being smuggled in as overrides.
    ///
    /// Between entries the value is interpolated linearly, and outside the table it holds
    /// at the nearest end. Holding rather than extrapolating is deliberate: a table is a
    /// statement about a specific range, and extending it by extrapolation would invent
    /// numbers nobody chose.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Hand-written values", "Numbers you type in, one per step. For the early positions you want to decide yourself.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(TableModel))]
    public sealed class TableModel : GrowthModel
    {
        [Tooltip("Axis position the first entry corresponds to.")]
        [SerializeField] private double _startX = 1d;

        [Tooltip("One value per step. Between entries the value is interpolated; outside them it holds at the nearest end.")]
        [SerializeField] private double[] _values = Array.Empty<double>();

        public TableModel() { }

        public TableModel(double startX, params double[] values)
        {
            _startX = startX;
            _values = values ?? Array.Empty<double>();
        }

        /// <summary>Axis position that the first entry corresponds to.</summary>
        public double StartX
        {
            get => _startX;
            set => _startX = value;
        }

        /// <summary>The tabulated values, one per unit of <c>x</c>.</summary>
        public double[] Values
        {
            get => _values;
            set => _values = value ?? Array.Empty<double>();
        }

        /// <summary>Number of entries. Zero is a valid authoring state.</summary>
        public int Count => _values?.Length ?? 0;

        public override double Evaluate(double x, EvalContext context)
        {
            var values = _values;
            if (values == null || values.Length == 0) return 0d;
            if (values.Length == 1) return values[0];

            var position = x - _startX;
            if (position <= 0d) return values[0];
            if (position >= values.Length - 1) return values[values.Length - 1];

            var lower = (int)Math.Floor(position);
            var t = position - lower;
            return values[lower] + (values[lower + 1] - values[lower]) * t;
        }

        public override string Describe()
        {
            var count = Count;
            if (count == 0) return "table (empty)";
            return $"table ({count} {(count == 1 ? "entry" : "entries")} from x={FormulaText.Number(_startX)})";
        }
    }
}
