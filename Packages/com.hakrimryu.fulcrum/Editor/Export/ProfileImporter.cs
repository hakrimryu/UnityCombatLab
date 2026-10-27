using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fulcrum.Editor.Export
{
    /// <summary>
    /// The editor half of a CSV import: choosing a file, confirming, and saying what changed.
    /// </summary>
    /// <remarks>
    /// Import overwrites authored formulas, so every step in front of it is about making
    /// that visible before it happens rather than explaining it afterwards. The parse runs
    /// first and its findings go into the confirmation, so the choice is made knowing which
    /// columns matched and what will be lost.
    /// </remarks>
    public static class ProfileImporter
    {
        private const string LastCsvPathKey = "Fulcrum.Export.LastCsvPath";

        /// <summary>Prompts for a CSV and applies it. Returns true if the profile changed.</summary>
        public static bool PromptAndImportCsv(BalanceProfile profile)
        {
            if (profile == null) return false;

            var directory = EditorPrefs.GetString(LastCsvPathKey, Application.dataPath);
            var path = EditorUtility.OpenFilePanel("Import CSV", directory, "csv");
            if (string.IsNullOrEmpty(path)) return false;

            EditorPrefs.SetString(LastCsvPathKey, Path.GetDirectoryName(path));

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException exception)
            {
                EditorUtility.DisplayDialog("Import CSV", $"Could not read the file.\n\n{exception.Message}", "OK");
                return false;
            }

            var parsed = CsvImporter.Parse(text);
            if (parsed.HasInvalidNumbers)
            {
                EditorUtility.DisplayDialog("Import CSV", "The file contains invalid numbers. The profile was not changed.\n\n"
                    + Describe(parsed, 0), "OK");
                return false;
            }

            var matched = CountMatches(profile, parsed, out var unmatched);

            if (matched == 0)
            {
                EditorUtility.DisplayDialog(
                    "Import CSV",
                    "Nothing in this file matches a track in this profile.\n\n"
                    + Describe(parsed, unmatched)
                    + "\nColumn headers have to be track ids, exactly as they were exported.",
                    "OK");
                return false;
            }

            var message =
                $"{matched} column(s) will replace the curve on the matching track.\n\n"
                + Describe(parsed, unmatched)
                + "\nThe imported numbers are used as-is, so post-processors and pinned values "
                + "on those tracks are removed. This can be undone.";

            if (!EditorUtility.DisplayDialog("Import CSV", message, "Import", "Cancel")) return false;

            Undo.RecordObject(profile, "Import CSV");
            var report = CsvImporter.Apply(profile, parsed);
            EditorUtility.SetDirty(profile);

            // The report belongs where it can be re-read; a dialog would be gone the moment
            // someone wants to check which column overwrote which curve.
            Debug.Log($"Fulcrum: imported {Path.GetFileName(path)} into '{profile.ProfileName}'.\n"
                      + string.Join("\n", report.ToArray()));

            return true;
        }

        private static int CountMatches(BalanceProfile profile, CsvImporter.Result parsed, out int unmatched)
        {
            var matched = 0;
            unmatched = 0;

            foreach (var column in parsed.Columns)
            {
                if (profile.TryGetTrack(column.Id, out _)) matched++;
                else unmatched++;
            }

            return matched;
        }

        private static string Describe(CsvImporter.Result parsed, int unmatched)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("Columns read: ").Append(parsed.Columns.Count).Append('\n');
            if (unmatched > 0) sb.Append("Not matching any track: ").Append(unmatched).Append('\n');

            if (parsed.Problems.Count > 0)
            {
                sb.Append('\n');

                // Enough to recognise the shape of the problem without turning the dialog
                // into a log window.
                var shown = Mathf.Min(parsed.Problems.Count, 5);
                for (var i = 0; i < shown; i++) sb.Append("• ").Append(parsed.Problems[i]).Append('\n');
                if (parsed.Problems.Count > shown)
                {
                    sb.Append("• …and ").Append(parsed.Problems.Count - shown).Append(" more.\n");
                }
            }

            return sb.ToString();
        }
    }
}
