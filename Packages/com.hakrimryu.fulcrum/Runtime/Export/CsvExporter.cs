using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fulcrum
{
    /// <summary>
    /// Writes an evaluated profile as CSV for spreadsheets and external tooling.
    /// </summary>
    /// <remarks>
    /// Tracks become columns and axis positions become rows, because that is the shape a
    /// designer already reads: one row per stage, every number for that stage beside it.
    ///
    /// Formatting is culture invariant. A locale that writes decimals with a comma would
    /// silently turn one column into two, and the file would open looking almost right.
    /// </remarks>
    public static class CsvExporter
    {
        /// <summary>
        /// Fifteen significant digits: everything a double reliably carries, and nothing
        /// past it.
        /// </summary>
        /// <remarks>
        /// Round-trip formatting ("R") preserves the exact bits, which means it also prints
        /// the artefacts of binary arithmetic: three sixths of a percent added together came
        /// out of the exporter as 0.018000000000000002. That lands in a spreadsheet in front
        /// of a designer, who is entitled to read it as the tool being broken.
        ///
        /// The lost precision is below the fourteenth digit and cannot matter to a balance
        /// number; the import round trip is verified to survive it.
        /// </remarks>
        private const string NumberFormat = "G15";

        /// <summary>Renders the whole profile as CSV text.</summary>
        public static string Export(BalanceProfile profile)
        {
            if (profile == null) return string.Empty;

            var tracks = new List<BalanceTrack>();
            var series = new List<double[]>();
            var evaluated = BalanceTableBaker.EvaluateForExport(profile);

            foreach (var track in profile.Tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                if (!evaluated.TryGetSeries(track.Id, out var values)) continue;

                tracks.Add(track);
                series.Add(values);
            }

            var sb = new StringBuilder();
            AppendHeader(sb, tracks);

            if (tracks.Count == 0) return sb.ToString();

            FindAxisBounds(tracks, out var from, out var to);

            for (var position = from; position <= to; position++)
            {
                sb.Append(position.ToString(CultureInfo.InvariantCulture));

                for (var i = 0; i < tracks.Count; i++)
                {
                    sb.Append(',');

                    // A track can cover only part of the axis. An empty cell says "this
                    // track does not reach here", which a zero would misrepresent.
                    var index = tracks[i].IndexOfAxisPosition(position);
                    if (index >= 0 && index < series[i].Length)
                    {
                        sb.Append(series[i][index].ToString(NumberFormat, CultureInfo.InvariantCulture));
                    }
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static void AppendHeader(StringBuilder sb, List<BalanceTrack> tracks)
        {
            sb.Append("axis");
            foreach (var track in tracks)
            {
                sb.Append(',').Append(Escape(track.Id));
            }
            sb.Append('\n');
        }

        private static void FindAxisBounds(List<BalanceTrack> tracks, out int from, out int to)
        {
            from = int.MaxValue;
            to = int.MinValue;

            foreach (var track in tracks)
            {
                if (track.Length == 0) continue;
                if (track.RangeMin < from) from = track.RangeMin;
                if (track.RangeMax > to) to = track.RangeMax;
            }

            if (from > to)
            {
                from = 0;
                to = -1;
            }
        }

        /// <summary>Quotes a field only when it would otherwise break the row.</summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var needsQuotes = value.IndexOf(',') >= 0
                           || value.IndexOf('"') >= 0
                           || value.IndexOf('\n') >= 0
                           || value.IndexOf('\r') >= 0;

            if (!needsQuotes) return value;

            return '"' + value.Replace("\"", "\"\"") + '"';
        }
    }
}
