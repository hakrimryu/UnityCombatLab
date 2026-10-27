using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Export
{
    public sealed class CsvExporterTests
    {
        private static string[] Lines(string csv)
            => csv.Split('\n').Where(l => l.Length > 0).ToArray();

        [Test]
        public void Csv_HasOneRowPerAxisPositionAndOneColumnPerTrack()
        {
            var profile = ExportFixture.Build();
            try
            {
                var lines = Lines(CsvExporter.Export(profile));

                Assert.That(lines[0], Is.EqualTo("axis,monster_hp,gold"));
                Assert.That(lines.Length, Is.EqualTo(6), "header plus stages 1..5");
                Assert.That(lines[1], Is.EqualTo("1,200,"));
                Assert.That(lines[3], Is.EqualTo("3,200,30"));
                Assert.That(lines[5], Is.EqualTo("5,200,50"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CellsOutsideATracksRange_AreLeftEmpty()
        {
            // A zero would claim the track has a value there; an empty cell says it does not.
            var profile = ExportFixture.Build();
            try
            {
                var lines = Lines(CsvExporter.Export(profile));

                Assert.That(lines[1], Does.EndWith(","));
                Assert.That(lines[2], Does.EndWith(","));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Numbers_AreCultureInvariant()
        {
            var original = System.Threading.Thread.CurrentThread.CurrentCulture;
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                // A comma decimal separator would silently split one column into two.
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                profile.Tracks.Add(new BalanceTrack("t", "stage", 1, 1, new LinearModel(0d, 1.5d)));

                Assert.That(Lines(CsvExporter.Export(profile))[1], Is.EqualTo("1,1.5"));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = original;
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TrackIdsThatWouldBreakARow_AreQuoted()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("a,b", "stage", 1, 1, new LinearModel(1d)));
                profile.Tracks.Add(new BalanceTrack("say \"hi\"", "stage", 1, 1, new LinearModel(1d)));

                Assert.That(Lines(CsvExporter.Export(profile))[0],
                    Is.EqualTo("axis,\"a,b\",\"say \"\"hi\"\"\""));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EmptyProfile_StillProducesAHeader()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                Assert.That(CsvExporter.Export(profile), Is.EqualTo("axis\n"));
                Assert.That(CsvExporter.Export(null), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void InvalidProfileDoesNotOverwriteAnExistingCsvFile()
        {
            var profile = ExportFixture.Build();
            var path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllText(path, "previous valid export");
                profile.Tracks.Add(new BalanceTrack("cycle", "stage", 1, 5, new TrackReferenceModel("cycle")));
                Assert.Throws<System.InvalidOperationException>(() => Fulcrum.Editor.Export.ProfileExporter.ExportCsv(profile, path));
                Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo("previous valid export"));
            }
            finally
            {
                System.IO.File.Delete(path);
                Object.DestroyImmediate(profile);
            }
        }

    }
}
