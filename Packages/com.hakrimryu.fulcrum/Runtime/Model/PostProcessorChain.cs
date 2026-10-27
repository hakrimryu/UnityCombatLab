using System.Collections.Generic;
using System.Text;

namespace Fulcrum
{
    /// <summary>
    /// Applies a track's post-processing steps in the order the designer arranged them.
    /// </summary>
    /// <remarks>
    /// Order is part of the configuration, not an implementation detail. Rounding and then
    /// enforcing a minimum step produces different numbers than doing it the other way
    /// round, and both are legitimate choices — so the chain is applied exactly as stored
    /// and never reordered for convenience.
    /// </remarks>
    public static class PostProcessorChain
    {
        /// <summary>
        /// Runs every step over <paramref name="values"/> in place. Null entries are
        /// skipped: a chain slot can be empty while its step is being chosen, and a broken
        /// <c>[SerializeReference]</c> looks the same from here.
        /// </summary>
        public static void Apply(IReadOnlyList<PostProcessor> processors, double[] values, EvalContext context)
        {
            if (processors == null || values == null) return;

            for (var i = 0; i < processors.Count; i++)
            {
                processors[i]?.Process(values, context);
            }
        }

        /// <summary>
        /// Renders the chain for display, e.g. <c>round 3sf → clamp ≥ 1</c>.
        /// Returns an empty string when there is nothing to show.
        /// </summary>
        public static string Describe(IReadOnlyList<PostProcessor> processors)
        {
            if (processors == null || processors.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            for (var i = 0; i < processors.Count; i++)
            {
                var processor = processors[i];
                if (processor == null) continue;

                if (sb.Length > 0) sb.Append(" → ");
                sb.Append(processor.Describe());
            }

            return sb.ToString();
        }
    }
}
