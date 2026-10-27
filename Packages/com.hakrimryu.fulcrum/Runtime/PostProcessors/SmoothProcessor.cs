using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum
{
    /// <summary>
    /// Averages each value with its neighbours to soften abrupt steps.
    /// </summary>
    /// <remarks>
    /// Useful where a piecewise model meets at a chapter boundary and the seam reads as a
    /// difficulty wall, or where hand-entered table values wobble.
    ///
    /// The average is centred and computed from a snapshot of the input, so a smoothed
    /// value never feeds into its own neighbours' averages. Smoothing in place would make
    /// the result depend on iteration direction and quietly drag the whole series.
    ///
    /// Near the ends the window shrinks to what exists rather than repeating the edge
    /// value, which would pull the first and last entries toward themselves and defeat the
    /// smoothing exactly where curves are most often deliberate.
    /// </remarks>
    [Serializable]
    [FulcrumModel("Smooth out", "Averages neighbours to soften a seam where two curves meet.")]
    [MovedFrom(false, sourceNamespace: "Fulcrum", sourceAssembly: "Fulcrum.Core", sourceClassName: nameof(SmoothProcessor))]
    public sealed class SmoothProcessor : PostProcessor
    {
        [SerializeField] private int _windowSize = 3;

        public SmoothProcessor() { }

        public SmoothProcessor(int windowSize) => _windowSize = windowSize;

        /// <summary>
        /// Total width of the averaging window. Values below 3 disable smoothing; even
        /// values are treated as the next odd one up so the window stays centred.
        /// </summary>
        public int WindowSize
        {
            get => _windowSize;
            set => _windowSize = value;
        }

        public override void Process(double[] values, EvalContext context)
        {
            if (values == null || values.Length < 3 || _windowSize < 3) return;

            var radius = _windowSize / 2;
            var source = (double[])values.Clone();

            for (var i = 0; i < values.Length; i++)
            {
                var from = Math.Max(0, i - radius);
                var to = Math.Min(source.Length - 1, i + radius);

                var sum = 0d;
                var count = 0;
                for (var j = from; j <= to; j++)
                {
                    sum += source[j];
                    count++;
                }

                values[i] = sum / count;
            }
        }

        public override string Describe()
            => _windowSize < 3 ? "smooth (off)" : $"smooth {_windowSize / 2 * 2 + 1}";
    }
}
