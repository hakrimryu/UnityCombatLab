using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Guarantees each step is larger than the one before it by at least some amount.
    /// </summary>
    /// <remarks>
    /// Rounding is what usually makes this necessary. A gentle curve rounded to three
    /// significant digits can produce runs of identical values, and a player who levels up
    /// and sees the same number learns that levelling did nothing.
    ///
    /// The pass only ever raises values, and a raise cascades forward — lifting one value
    /// can push every later one up too. That is the intent, but it does mean this belongs
    /// near the end of a chain: a rounding step placed after it can flatten the gaps it
    /// just created.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Always increase", "Guarantees each step is bigger than the last, so advancing is visible.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(MinDeltaProcessor))]
    public sealed class MinDeltaProcessor : PostProcessor
    {
        [SerializeField] private double _minDelta = 1d;

        public MinDeltaProcessor() { }

        public MinDeltaProcessor(double minDelta) => _minDelta = minDelta;

        /// <summary>Smallest permitted increase from one index to the next.</summary>
        public double MinDelta
        {
            get => _minDelta;
            set => _minDelta = value;
        }

        public override void Process(double[] values, EvalContext context)
        {
            if (values == null || values.Length < 2) return;

            for (var i = 1; i < values.Length; i++)
            {
                var floor = values[i - 1] + _minDelta;

                // NaN fails every comparison, so it would survive the pass and then poison
                // every later step through the running floor. Replace it with the floor.
                if (double.IsNaN(values[i]) || values[i] < floor)
                {
                    values[i] = floor;
                }
            }
        }

        public override string Describe() => $"min step {FormulaText.Number(_minDelta)}";
    }
}
