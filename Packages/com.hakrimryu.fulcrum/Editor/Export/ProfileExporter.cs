using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fulcrum.Editor.Export
{
    /// <summary>
    /// Editor-side plumbing for writing a profile out: file dialogs, asset creation and
    /// the timestamp.
    /// </summary>
    /// <remarks>
    /// The evaluation and packing live in the runtime assembly; only the parts that need
    /// the editor are here, so the same export can be driven from a build script later.
    /// </remarks>
    public static class ProfileExporter
    {
        private const string LastTablePathKey = "Fulcrum.Export.LastTablePath";
        private const string LastCsvPathKey = "Fulcrum.Export.LastCsvPath";

        /// <summary>
        /// Bakes a profile into a <see cref="BalanceTable"/> asset, reusing the asset at
        /// <paramref name="assetPath"/> when one is already there.
        /// </summary>
        /// <remarks>
        /// Reusing rather than replacing matters: prefabs and scenes referencing the table
        /// keep their link, so re-exporting does not quietly empty every reference.
        /// </remarks>
        public static BalanceTable ExportTable(BalanceProfile profile, string assetPath)
        {
            if (profile == null || string.IsNullOrEmpty(assetPath)) return null;

            var table = AssetDatabase.LoadAssetAtPath<BalanceTable>(assetPath);
            var isNew = table == null;
            if (isNew) table = ScriptableObject.CreateInstance<BalanceTable>();

            try
            {
                BalanceTableBaker.Bake(profile, table, DateTime.UtcNow.ToString("O"));
            }
            catch
            {
                if (isNew) UnityEngine.Object.DestroyImmediate(table);
                throw;
            }

            if (isNew) AssetDatabase.CreateAsset(table, assetPath);
            else EditorUtility.SetDirty(table);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<BalanceTable>(assetPath);
        }

        /// <summary>Writes a profile as CSV to an absolute path.</summary>
        public static void ExportCsv(BalanceProfile profile, string absolutePath)
        {
            if (profile == null || string.IsNullOrEmpty(absolutePath)) return;

            File.WriteAllText(absolutePath, CsvExporter.Export(profile));

            // Only refresh when the file landed inside the project.
            if (absolutePath.Replace('\\', '/').StartsWith(
                Application.dataPath.Replace('\\', '/'), StringComparison.Ordinal))
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>Prompts for a location and bakes the table there.</summary>
        public static BalanceTable PromptAndExportTable(BalanceProfile profile)
        {
            if (profile == null) return null;

            var directory = EditorPrefs.GetString(LastTablePathKey, "Assets");
            var path = EditorUtility.SaveFilePanelInProject(
                "Export balance table", profile.ProfileName + "Table", "asset",
                "Choose where to write the baked table.", directory);

            if (string.IsNullOrEmpty(path)) return null;

            EditorPrefs.SetString(LastTablePathKey, Path.GetDirectoryName(path));
            try
            {
                return ExportTable(profile, path);
            }
            catch (InvalidOperationException exception)
            {
                EditorUtility.DisplayDialog("Export balance table", exception.Message, "OK");
                return null;
            }
        }

        /// <summary>Prompts for a location and writes the CSV there.</summary>
        public static void PromptAndExportCsv(BalanceProfile profile)
        {
            if (profile == null) return;

            var directory = EditorPrefs.GetString(LastCsvPathKey, Application.dataPath);
            var path = EditorUtility.SaveFilePanel(
                "Export CSV", directory, profile.ProfileName + ".csv", "csv");

            if (string.IsNullOrEmpty(path)) return;

            EditorPrefs.SetString(LastCsvPathKey, Path.GetDirectoryName(path));
            try
            {
                ExportCsv(profile, path);
            }
            catch (InvalidOperationException exception)
            {
                EditorUtility.DisplayDialog("Export CSV", exception.Message, "OK");
            }
        }
    }
}
