using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests
{
    /// <summary>
    /// The round trip through a spreadsheet, and the ways a file comes back wrong.
    /// </summary>
    public sealed class CsvImporterTests
    {
        private static BalanceProfile Profile(params BalanceTrack[] tracks)
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Axes.Add(new AxisDefinition("stage", 1, 10, "Stage"));
            foreach (var track in tracks) profile.Tracks.Add(track);
            return profile;
        }

        [Test]
        public void Parse_ReadsColumnsAndPositions()
        {
            var result = CsvImporter.Parse("axis,hp,atk\n1,10,2\n2,20,4\n3,30,6\n");

            Assert.That(result.Problems, Is.Empty);
            Assert.That(result.Columns.Count, Is.EqualTo(2));

            var hp = result.Columns[0];
            Assert.That(hp.Id, Is.EqualTo("hp"));
            Assert.That(hp.StartPosition, Is.EqualTo(1));
            Assert.That(hp.EndPosition, Is.EqualTo(3));
            Assert.That(hp.Values, Is.EqualTo(new[] { 10d, 20d, 30d }));
        }

        [Test]
        public void Parse_EmptyCellsTrimTheColumnRange()
        {
            // Exactly what the exporter writes for a track that covers part of the axis.
            var result = CsvImporter.Parse("axis,hp,late\n1,10,\n2,20,\n3,30,7\n4,40,8\n");

            Assert.That(result.Problems, Is.Empty);

            var late = result.Columns.Find(c => c.Id == "late");
            Assert.That(late.StartPosition, Is.EqualTo(3));
            Assert.That(late.Values, Is.EqualTo(new[] { 7d, 8d }));
        }

        [Test]
        public void Parse_HoleInTheMiddleSkipsTheColumn()
        {
            var result = CsvImporter.Parse("axis,hp\n1,10\n2,\n3,30\n");

            Assert.That(result.Columns, Is.Empty);
            Assert.That(result.Problems.Count, Is.EqualTo(1));
            Assert.That(result.Problems[0], Does.Contain("position 2"));
        }

        [Test]
        public void Parse_RowsMayArriveOutOfOrder()
        {
            // A sheet that came back sorted by value rather than by stage.
            var result = CsvImporter.Parse("axis,hp\n3,30\n1,10\n2,20\n");

            Assert.That(result.Problems, Is.Empty);
            Assert.That(result.Columns[0].Values, Is.EqualTo(new[] { 10d, 20d, 30d }));
        }

        [Test]
        public void Parse_NonNumericCellIsReportedNotGuessed()
        {
            var result = CsvImporter.Parse("axis,hp\n1,10\n2,1 200\n3,30\n");

            Assert.That(result.Problems.Count, Is.EqualTo(2), "the bad cell and the hole it leaves");
            Assert.That(result.Problems[0], Does.Contain("not a number"));
            Assert.That(result.Columns, Is.Empty);
        }

        [Test]
        public void Parse_QuotedHeaderIsUnwrapped()
        {
            var result = CsvImporter.Parse("axis,\"hp,total\"\n1,10\n2,20\n");

            Assert.That(result.Columns[0].Id, Is.EqualTo("hp,total"));
        }

        [Test]
        public void Parse_EmptyFileIsReported()
        {
            Assert.That(CsvImporter.Parse(string.Empty).Problems, Is.Not.Empty);
            Assert.That(CsvImporter.Parse("axis,hp\n").Problems, Is.Not.Empty);
        }

        [Test]
        public void Apply_ReplacesTheModelWithTheImportedValues()
        {
            var track = new BalanceTrack("hp", "stage", 1, 10, new LinearModel(1d));
            var profile = Profile(track);

            CsvImporter.Apply(profile, CsvImporter.Parse("axis,hp\n1,10\n2,25\n3,90\n"));

            Assert.That(track.Model, Is.TypeOf<TableModel>());
            Assert.That(track.RangeMin, Is.EqualTo(1));
            Assert.That(track.RangeMax, Is.EqualTo(3));

            var values = TrackEvaluator.EvaluateAll(profile)["hp"];
            Assert.That(values, Is.EqualTo(new[] { 10d, 25d, 90d }));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Apply_ClearsPostProcessorsSoValuesAreNotProcessedTwice()
        {
            // The exported column already had the rounding applied. Keeping the chain would
            // round it again, and the profile would not show the file that was imported.
            var track = new BalanceTrack("hp", "stage", 1, 3, new LinearModel(1d));
            track.PostProcessors.Add(new RoundProcessor(RoundingMode.SignificantDigits, 1));
            track.Overrides.Add(new ManualOverride(2, 999d, "pinned"));

            var profile = Profile(track);
            var report = CsvImporter.Apply(profile, CsvImporter.Parse("axis,hp\n1,17\n2,34\n3,51\n"));

            Assert.That(track.PostProcessors, Is.Empty);
            Assert.That(track.Overrides, Is.Empty);
            Assert.That(TrackEvaluator.EvaluateAll(profile)["hp"], Is.EqualTo(new[] { 17d, 34d, 51d }));
            Assert.That(report[0], Does.Contain("post-processor"));
            Assert.That(report[0], Does.Contain("pinned value"));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Apply_UnknownColumnIsReportedAndLeavesTheProfileAlone()
        {
            var track = new BalanceTrack("hp", "stage", 1, 3, new LinearModel(1d));
            var profile = Profile(track);

            var report = CsvImporter.Apply(profile, CsvImporter.Parse("axis,mana\n1,5\n2,6\n"));

            Assert.That(report.Count, Is.EqualTo(1));
            Assert.That(report[0], Does.Contain("no track with that id"));
            Assert.That(track.Model, Is.TypeOf<LinearModel>());

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ExportThenImport_ReproducesTheSameNumbers()
        {
            var profile = Profile(
                new BalanceTrack("hp", "stage", 1, 10, new PowerModel(70d, 1.05d)),
                new BalanceTrack("atk", "stage", 1, 10, new PowerModel(6d, 0.9d)));

            var before = TrackEvaluator.EvaluateAll(profile);
            var expected = new Dictionary<string, double[]>(before);

            CsvImporter.Apply(profile, CsvImporter.Parse(CsvExporter.Export(profile)));
            var after = TrackEvaluator.EvaluateAll(profile);

            foreach (var pair in expected)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value).Within(1e-9),
                    $"track '{pair.Key}' changed across a round trip");
            }

            Object.DestroyImmediate(profile);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("1e999")]
        public void NonFiniteCellRejectsTheWholeImportWithoutChangingAnyTrack(string invalid)
        {
            var first = new BalanceTrack("hp", "stage", 1, 3, new LinearModel(1d));
            var second = new BalanceTrack("atk", "stage", 1, 3, new LinearModel(2d));
            var profile = Profile(first, second);
            try
            {
                var before = UnityEditor.EditorJsonUtility.ToJson(profile);
                var parsed = CsvImporter.Parse("axis,hp,atk\n1,100,10\n2,200," + invalid + "\n3,300,30\n");
                Assert.That(parsed.Problems, Is.Not.Empty);
                Assert.That(CsvImporter.Apply(profile, parsed), Has.Some.Contains("rejected"));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(profile), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void CallerBuiltInvalidColumnCannotPartiallyReplaceTheProfile()
        {
            var profile = Profile(new BalanceTrack("hp", "stage", 1, 3, new LinearModel(1d)),
                new BalanceTrack("atk", "stage", 1, 3, new LinearModel(2d)));
            try
            {
                var before = UnityEditor.EditorJsonUtility.ToJson(profile);
                var parsed = new CsvImporter.Result();
                parsed.Columns.Add(new CsvImporter.Column("hp", 1, new[] { 100d }));
                parsed.Columns.Add(new CsvImporter.Column("atk", 1, new[] { double.NaN }));
                Assert.That(CsvImporter.Apply(profile, parsed), Has.Some.Contains("rejected"));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(profile), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(profile); }
        }

    }
}
