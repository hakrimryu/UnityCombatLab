using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fulcrum
{
    /// <summary>
    /// Evaluates a profile and packs the result into a <see cref="BalanceTable"/>.
    /// </summary>
    /// <remarks>
    /// Kept out of the editor assembly so a project can bake at runtime — a live-tuning
    /// build or a test harness needs the same operation, and duplicating it would let the
    /// two drift.
    /// </remarks>
    public static class BalanceTableBaker
    {
        /// <summary>Fills <paramref name="table"/> with the evaluated contents of a profile.</summary>
        public static void Bake(BalanceProfile profile, BalanceTable table, string exportedAtUtc = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            if (profile == null)
            {
                table.SetContents(null, null, exportedAtUtc, null, null, null, null, null);
                return;
            }

            var evaluated = EvaluateForExport(profile);

            var ids = new List<string>(evaluated.TrackIds.Count);
            var rangeMins = new List<int>(evaluated.TrackIds.Count);
            var lengths = new List<int>(evaluated.TrackIds.Count);
            var offsets = new List<int>(evaluated.TrackIds.Count);
            var values = new List<double>();

            // Iterating the profile rather than the dictionary keeps storage in authoring
            // order, so a diff between two exports lines up.
            foreach (var track in profile.Tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                if (!evaluated.TryGetSeries(track.Id, out var series)) continue;

                ids.Add(track.Id);
                rangeMins.Add(track.RangeMin);
                lengths.Add(series.Length);
                offsets.Add(values.Count);
                values.AddRange(series);
            }

            table.SetContents(
                profile.ProfileName,
                ComputeHash(profile, evaluated),
                exportedAtUtc,
                ids.ToArray(), rangeMins.ToArray(), lengths.ToArray(), offsets.ToArray(), values.ToArray());
        }

        /// <summary>
        /// A fingerprint of everything that affects the exported numbers.
        /// </summary>
        /// <remarks>
        /// Includes the formulas, ranges, constants and sampled output, so edits inside
        /// table or curve models are detected without relying on their display descriptions.
        /// Track labels and the export timestamp do not affect the fingerprint.
        /// </remarks>
        public static string ComputeHash(BalanceProfile profile)
        {
            if (profile == null) return string.Empty;
            return ComputeHash(profile, EvaluateForExport(profile));
        }

        /// <summary>Validates a complete export before any existing asset or file is changed.</summary>
        internal static ProfileEvaluation EvaluateForExport(BalanceProfile profile)
        {
            var evaluated = TrackEvaluator.EvaluateProfile(profile);
            if (!evaluated.IsComplete)
            {
                var cycles = new List<string>();
                foreach (var cycle in evaluated.Graph.Cycles)
                    cycles.Add(string.Join(" → ", cycle));
                throw new InvalidOperationException("Cannot export cyclic tracks: " + string.Join("; ", cycles));
            }

            if (evaluated.Graph.MissingReferences.Count > 0)
                throw new InvalidOperationException("Cannot export a missing track reference: " + evaluated.Graph.MissingReferences[0]);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var track in profile.Tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                if (!ids.Add(track.Id))
                    throw new InvalidOperationException("Cannot export duplicate track id: " + track.Id);

                if (!evaluated.TryGetSeries(track.Id, out var values)) continue;
                for (var index = 0; index < values.Length; index++)
                {
                    if (double.IsNaN(values[index]) || double.IsInfinity(values[index]))
                        throw new InvalidOperationException($"Cannot export non-finite value in '{track.Id}' at {track.AxisPositionAt(index)}.");
                }
            }

            return evaluated;
        }

        private static string ComputeHash(BalanceProfile profile, ProfileEvaluation evaluated)
        {
            var sb = new StringBuilder();

            foreach (var global in profile.Globals)
            {
                if (global == null) continue;
                sb.Append(global.Key).Append('=')
                  .Append(global.Value.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }

            foreach (var track in profile.Tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;

                sb.Append(track.Id).Append('|')
                  .Append(track.AxisId).Append('|')
                  .Append(track.RangeMin).Append("..").Append(track.RangeMax).Append('|')
                  .Append(track.Accumulate ? 'c' : '-').Append('|')
                  .Append(track.HasModel ? track.Model.Describe() : "(none)").Append('|')
                  .Append(PostProcessorChain.Describe(track.PostProcessors)).Append('|');

                foreach (var pinned in track.Overrides)
                {
                    if (pinned == null) continue;
                    sb.Append(pinned.Index).Append(':')
                      .Append(pinned.Value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                }

                sb.Append("|values:");
                if (evaluated.TryGetSeries(track.Id, out var values))
                {
                    foreach (var value in values)
                        sb.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                }

                sb.Append(';');
            }

            return ToHex(sb.ToString());
        }

        /// <summary>
        /// FNV-1a over the descriptor. A cryptographic digest would be overkill for
        /// telling two authoring states apart, and this has no external dependency.
        /// </summary>
        private static string ToHex(string text)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            var hash = offsetBasis;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
