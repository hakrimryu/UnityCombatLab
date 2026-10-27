using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fulcrum
{
    /// <summary>
    /// Reads back the CSV shape that <see cref="CsvExporter"/> writes.
    /// </summary>
    /// <remarks>
    /// Export without import is a one-way door. A designer sends the numbers to someone who
    /// does not have Unity, gets a corrected sheet back, and then has to retype it — so in
    /// practice the spreadsheet becomes the master copy and the profile rots.
    ///
    /// Parsing is deliberately strict and reports rather than guesses. A file that has been
    /// through Excel can come back with a renamed column, a locale that writes decimals with
    /// a comma, or a row order that got sorted alphabetically; silently accepting any of
    /// those would write plausible wrong numbers into the profile.
    /// </remarks>
    public static class CsvImporter
    {
        /// <summary>One imported column: a contiguous run of values from an axis position.</summary>
        public sealed class Column
        {
            public Column(string id, int startPosition, double[] values)
            {
                Id = id;
                StartPosition = startPosition;
                Values = values;
            }

            /// <summary>The header cell, matched against track ids.</summary>
            public string Id { get; }

            /// <summary>Axis position of <see cref="Values"/>[0].</summary>
            public int StartPosition { get; }

            /// <summary>One value per axis position, with no gaps.</summary>
            public double[] Values { get; }

            public int EndPosition => StartPosition + Values.Length - 1;
        }

        public sealed class Result
        {
            public List<Column> Columns { get; } = new List<Column>();

            /// <summary>Everything that stopped a cell or column from being read.</summary>
            public List<string> Problems { get; } = new List<string>();

            public bool HasColumns => Columns.Count > 0;

            /// <summary>Whether an invalid numeric cell prevents the entire import from being applied.</summary>
            public bool HasInvalidNumbers { get; internal set; }
        }

        /// <summary>Parses CSV text. Never throws; anything unreadable becomes a problem.</summary>
        public static Result Parse(string text)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(text))
            {
                result.Problems.Add("The file is empty.");
                return result;
            }

            var rows = SplitRows(text);
            if (rows.Count < 2)
            {
                result.Problems.Add("The file has a header but no rows of numbers.");
                return result;
            }

            var header = rows[0];
            if (header.Count < 2)
            {
                result.Problems.Add(
                    "The header needs an axis column followed by at least one track column.");
                return result;
            }

            // Column 0 is the axis position. Its header is not checked, because a
            // spreadsheet round trip often renames it to something friendlier.
            var ids = new string[header.Count - 1];
            for (var i = 1; i < header.Count; i++) ids[i - 1] = header[i].Trim();

            // Values arrive keyed by position rather than by row, so a sheet that came back
            // sorted differently still lands in the right place.
            var cells = new Dictionary<int, double>[ids.Length];
            for (var i = 0; i < cells.Length; i++) cells[i] = new Dictionary<int, double>();

            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0) continue;

                var line = r + 1;
                if (!int.TryParse(row[0].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var position))
                {
                    result.Problems.Add($"Line {line}: '{row[0]}' is not an axis position.");
                    continue;
                }

                for (var c = 0; c < ids.Length; c++)
                {
                    var raw = c + 1 < row.Count ? row[c + 1].Trim() : string.Empty;
                    if (raw.Length == 0) continue;

                    if (!double.TryParse(raw, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var value))
                    {
                        result.HasInvalidNumbers = true;
                        result.Problems.Add(
                            $"Line {line}, column '{ids[c]}': '{raw}' is not a number. "
                            + "Numbers must use a dot for the decimal point and no thousands separator.");
                        continue;
                    }

                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        result.HasInvalidNumbers = true;
                        result.Problems.Add($"Line {line}, column '{ids[c]}': '{raw}' must be a finite number. Import rejected.");
                        continue;
                    }

                    cells[c][position] = value;
                }
            }

            for (var c = 0; c < ids.Length; c++) BuildColumn(result, ids[c], cells[c]);

            if (!result.HasColumns && result.Problems.Count == 0)
            {
                result.Problems.Add("No column held any numbers.");
            }

            return result;
        }

        /// <summary>
        /// Turns the cells of one column into a contiguous run.
        /// </summary>
        /// <remarks>
        /// A hole in the middle is rejected rather than interpolated. An exported track
        /// leaves cells empty only outside its own range, so a gap in between means the file
        /// was edited in a way the profile cannot represent — and filling it in would put a
        /// number nobody wrote into the asset.
        /// </remarks>
        private static void BuildColumn(Result result, string id, Dictionary<int, double> cells)
        {
            if (string.IsNullOrEmpty(id))
            {
                if (cells.Count > 0) result.Problems.Add("A column with no header was skipped.");
                return;
            }

            if (cells.Count == 0) return;

            var from = int.MaxValue;
            var to = int.MinValue;
            foreach (var position in cells.Keys)
            {
                if (position < from) from = position;
                if (position > to) to = position;
            }

            var values = new double[to - from + 1];
            for (var position = from; position <= to; position++)
            {
                if (!cells.TryGetValue(position, out var value))
                {
                    result.Problems.Add(
                        $"Column '{id}': no value at position {position}, "
                        + $"but the column runs from {from} to {to}. The column was skipped.");
                    return;
                }

                values[position - from] = value;
            }

            result.Columns.Add(new Column(id, from, values));
        }

        /// <summary>Splits CSV text into rows of fields, honouring quoted cells.</summary>
        private static List<List<string>> SplitRows(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            var quoted = false;
            var any = false;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                if (quoted)
                {
                    if (ch != '"') { field.Append(ch); continue; }

                    // A doubled quote inside a quoted field is a literal quote.
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; continue; }

                    quoted = false;
                    continue;
                }

                switch (ch)
                {
                    case '"':
                        quoted = true;
                        any = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        any = true;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        if (any || row.Count > 1) rows.Add(row);
                        row = new List<string>();
                        any = false;
                        break;
                    default:
                        field.Append(ch);
                        if (!char.IsWhiteSpace(ch)) any = true;
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (any || row.Count > 1) rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Writes parsed columns onto the matching tracks and reports what happened.
        /// </summary>
        /// <remarks>
        /// The imported numbers are final values, so the post-processor chain, the manual
        /// overrides and the running-total flag are all cleared as they are applied. Leaving
        /// any of them would round an already rounded column a second time, let an override
        /// outrank the numbers just imported, or accumulate a column that already is a
        /// running total — in every case the profile would show something other than the
        /// file that was chosen.
        ///
        /// This is destructive to the authored formula by design: the point of importing is
        /// that the spreadsheet is now the source of truth for that track.
        /// </remarks>
        public static List<string> Apply(BalanceProfile profile, Result parsed)
        {
            var report = new List<string>();
            if (profile == null || parsed == null) return report;

            // Validate every column before replacing any model, including caller-built results.
            var hasInvalidNumbers = parsed.HasInvalidNumbers;
            foreach (var column in parsed.Columns)
            {
                foreach (var value in column.Values)
                    hasInvalidNumbers |= double.IsNaN(value) || double.IsInfinity(value);
            }

            if (hasInvalidNumbers)
            {
                report.Add("Import rejected: every numeric cell must contain a finite number. The profile was not changed.");
                return report;
            }

            foreach (var column in parsed.Columns)
            {
                if (!profile.TryGetTrack(column.Id, out var track))
                {
                    report.Add($"'{column.Id}' — no track with that id. Skipped.");
                    continue;
                }

                var replaced = track.HasModel ? track.Model.Describe() : "nothing";
                var dropped = track.PostProcessors.Count;
                var pinned = track.Overrides.Count;
                var wasAccumulating = track.Accumulate;

                track.Model = new TableModel(column.StartPosition, column.Values);
                track.PostProcessors.Clear();
                track.Overrides.Clear();

                // The exported column was already a running total. Leaving the flag on would
                // accumulate it a second time, turning 0.006, 0.012, 0.018 into
                // 0.006, 0.018, 0.036 the moment the file came back in.
                track.Accumulate = false;

                track.RangeMin = column.StartPosition;
                track.RangeMax = column.EndPosition;

                var note = $"'{column.Id}' — {column.Values.Length} values "
                           + $"({column.StartPosition}–{column.EndPosition}), replacing {replaced}";

                if (dropped > 0) note += $"; {dropped} post-processor(s) removed";
                if (pinned > 0) note += $"; {pinned} pinned value(s) removed";
                if (wasAccumulating) note += "; running total turned off (the values already are one)";

                report.Add(note + ".");
            }

            return report;
        }
    }
}
