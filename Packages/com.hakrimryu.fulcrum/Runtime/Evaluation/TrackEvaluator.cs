using System;
using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// Turns a track into the series of numbers it describes.
    /// </summary>
    /// <remarks>
    /// The pipeline runs in a fixed order, and the order is a design decision rather than
    /// an implementation detail:
    ///
    /// <list type="number">
    /// <item>the model produces a raw value for each axis position,</item>
    /// <item>the post-processing chain shapes those values,</item>
    /// <item>accumulation turns per-step values into running totals,</item>
    /// <item>manual overrides pin positions to stated values.</item>
    /// </list>
    ///
    /// Post-processing sits before accumulation because a designer tunes what one step
    /// costs, not what the total happens to round to. Rounding each step and summing gives
    /// a total built from numbers someone chose; summing and then rounding gives a total
    /// whose individual steps are whatever the arithmetic left over.
    ///
    /// Overrides come last because they are statements about the finished series, which is
    /// also why they behave differently on a cumulative track — see
    /// <see cref="ApplyOverrides"/>.
    /// </remarks>
    public static class TrackEvaluator
    {
        /// <summary>
        /// Evaluates <paramref name="track"/> across its range.
        /// </summary>
        /// <returns>
        /// A freshly allocated array the caller owns, one entry per axis position from
        /// <see cref="BalanceTrack.RangeMin"/> upward. Empty when the range is inverted.
        /// </returns>
        public static double[] Evaluate(BalanceTrack track, EvalContext context)
        {
            if (track == null) return Array.Empty<double>();

            var length = track.Length;
            if (length == 0) return Array.Empty<double>();

            context = context ?? new EvalContext(0);

            var values = new double[length];

            EvaluateModel(track, context, values);
            PostProcessorChain.Apply(track.PostProcessors, values, context);

            if (track.Accumulate) Accumulate(values);
            ApplyOverrides(track, values, track.Accumulate);

            return values;
        }

        /// <summary>
        /// Evaluates a track using the profile's constants.
        /// </summary>
        public static double[] Evaluate(BalanceProfile profile, BalanceTrack track)
            => Evaluate(track, profile != null ? profile.CreateContext() : null);

        /// <summary>
        /// Evaluates every track in a profile, in dependency order.
        /// </summary>
        /// <remarks>
        /// Tracks caught in a cycle are skipped rather than guessed at — see
        /// <see cref="DependencyGraph.Cycles"/> for what to report. Tracks without an id are
        /// skipped too; nothing can reference them and nothing can export them.
        /// </remarks>
        public static ProfileEvaluation EvaluateProfile(BalanceProfile profile)
        {
            var graph = DependencyGraph.Build(profile);
            var evaluation = new ProfileEvaluation(graph);
            if (profile == null) return evaluation;

            var globals = profile.BuildGlobals();

            // Each track is evaluated with a resolver over the results so far, which is
            // exactly why the order matters: a dependency is already stored by the time
            // anything reads it.
            var context = new EvalContext(0, globals, evaluation);

            foreach (var id in graph.EvaluationOrder)
            {
                if (!profile.TryGetTrack(id, out var track)) continue;
                evaluation.Store(track, Evaluate(track, context));
            }

            return evaluation;
        }

        /// <summary>
        /// Evaluates every track in a profile, keyed by track id.
        /// </summary>
        public static Dictionary<string, double[]> EvaluateAll(BalanceProfile profile)
        {
            var result = new Dictionary<string, double[]>();
            if (profile == null) return result;

            var evaluation = EvaluateProfile(profile);
            foreach (var id in evaluation.TrackIds)
            {
                result[id] = evaluation[id];
            }

            return result;
        }

        private static void EvaluateModel(BalanceTrack track, EvalContext context, double[] values)
        {
            var model = track.Model;

            // A track without a model is a normal authoring state — the curve has not been
            // chosen yet. Zeros keep the rest of the pipeline meaningful; the editor is
            // responsible for showing that the model is missing.
            if (model == null) return;

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = model.Evaluate(track.AxisPositionAt(i), context.AtIndex(i));
            }
        }

        private static void Accumulate(double[] values)
        {
            for (var i = 1; i < values.Length; i++)
            {
                values[i] += values[i - 1];
            }
        }

        /// <summary>
        /// Pins overrides onto the finished series.
        /// </summary>
        /// <remarks>
        /// On an ordinary track an override replaces one value and nothing else moves.
        ///
        /// On a cumulative track it also shifts everything after it by the same amount.
        /// A running total is a statement about everything up to a point, so pinning the
        /// total at stage 42 without carrying the change forward would leave stage 43 back
        /// on the original line — turning the step between them into whatever number
        /// happens to close the gap, sometimes a negative one.
        /// </remarks>
        private static void ApplyOverrides(BalanceTrack track, double[] values, bool cumulative)
        {
            var overrides = track.Overrides;
            if (overrides == null || overrides.Count == 0) return;

            var byIndex = BuildOverrideLookup(track, overrides, values.Length);
            if (byIndex == null) return;

            if (!cumulative)
            {
                foreach (var pair in byIndex)
                {
                    values[pair.Key] = pair.Value;
                }
                return;
            }

            var shift = 0d;
            for (var i = 0; i < values.Length; i++)
            {
                values[i] += shift;

                if (byIndex.TryGetValue(i, out var pinned))
                {
                    shift += pinned - values[i];
                    values[i] = pinned;
                }
            }
        }

        private static Dictionary<int, double> BuildOverrideLookup(
            BalanceTrack track, List<ManualOverride> overrides, int length)
        {
            Dictionary<int, double> byIndex = null;

            for (var i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry == null) continue;

                // Positions outside the track are ignored rather than rejected: narrowing a
                // range should not destroy the overrides that sit beyond it, in case the
                // range is widened again.
                var index = track.IndexOfAxisPosition(entry.Index);
                if (index < 0 || index >= length) continue;

                byIndex = byIndex ?? new Dictionary<int, double>();
                byIndex[index] = entry.Value;
            }

            return byIndex;
        }
    }
}
