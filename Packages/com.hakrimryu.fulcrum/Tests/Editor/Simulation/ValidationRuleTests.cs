using System.Linq;
using Fulcrum.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Simulation
{
    public sealed class ValidationRuleTests
    {
        private readonly BalanceAnalyzer _analyzer = new BalanceAnalyzer();

        private BalanceAnalysis Analyze(BalanceProfile profile, int to = 60, int iterations = 100)
        {
            var run = SimulationRun.Default(1, to);
            run.Iterations = iterations;
            return _analyzer.Analyze(BalanceSnapshot.From(profile), SimFixture.Binding(), run);
        }

        [Test]
        public void ABalancedProfile_ProducesNoErrors()
        {
            var profile = SimFixture.Profile();
            try
            {
                Assert.That(Analyze(profile).HasErrors, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AnUnclearableStretch_IsAnError()
        {
            var profile = SimFixture.Profile();
            try
            {
                // A wall from stage 30 on.
                profile.TryGetTrack("monster_hp", out var hp);
                hp.Model = new PiecewiseModel(
                    new PiecewiseSegment(1d, new PowerModel(70d, 1.05d)),
                    new PiecewiseSegment(30d, new LinearModel(0d, 1e9d)));

                var analysis = Analyze(profile);
                var cliff = analysis.Issues.FirstOrDefault(i => i.RuleId == "difficulty_cliff");

                Assert.That(cliff, Is.Not.Null);
                Assert.That(cliff.Severity, Is.EqualTo(Severity.Error));
                Assert.That(cliff.IndexFrom, Is.GreaterThanOrEqualTo(29));
                Assert.That(cliff.Suggestion, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AWideWall_IsOneFindingNotOnePerPosition()
        {
            // Driven directly rather than through a simulation: a real run stops at the
            // wall, so the only way to exercise a wide stretch is to state one.
            var report = SyntheticReport(winRates: new[]
            {
                0.9d, 0.9d, 0.05d, 0.02d, 0.01d, 0.03d, 0.9d, 0.9d,
            });

            var issues = new DifficultyCliffRule().Validate(report).ToArray();

            Assert.That(issues.Length, Is.EqualTo(1), "A wall four positions wide is one problem.");
            Assert.That(issues[0].IndexFrom, Is.EqualTo(3));
            Assert.That(issues[0].IndexTo, Is.EqualTo(6));
        }

        [Test]
        public void TwoSeparateWalls_AreTwoFindings()
        {
            var report = SyntheticReport(winRates: new[] { 0.9d, 0.01d, 0.9d, 0.01d, 0.9d });

            Assert.That(new DifficultyCliffRule().Validate(report).Count(), Is.EqualTo(2));
        }

        [Test]
        public void ShortFightsDoNotCountAsSpikes()
        {
            // One turn to two is a doubling nobody notices; reporting it would bury the
            // spikes that matter under findings at the start of every profile.
            var report = SyntheticReport(
                winRates: new[] { 1d, 1d, 1d, 1d },
                turns: new[] { 1d, 2d, 3d, 4d });

            Assert.That(new DifficultySpikeRule().Validate(report), Is.Empty);
        }

        [Test]
        public void ALongFightThatSuddenlyDoubles_IsASpike()
        {
            var report = SyntheticReport(
                winRates: new[] { 1d, 1d, 1d, 1d },
                turns: new[] { 10d, 11d, 30d, 31d });

            var issues = new DifficultySpikeRule().Validate(report).ToArray();

            Assert.That(issues.Length, Is.EqualTo(1));
            Assert.That(issues[0].IndexFrom, Is.EqualTo(3));
        }

        /// <summary>A report with stated numbers, so rules can be tested on their own.</summary>
        private static SimulationReport SyntheticReport(double[] winRates, double[] turns = null)
        {
            var progression = new ProgressionResult
            {
                RangeMin = 1,
                WinRateAtStage = winRates,
                TurnsAtStage = turns ?? Enumerable.Repeat(10d, winRates.Length).ToArray(),
                CumulativePlayTimeSec = new double[winRates.Length],
                LevelAtStage = new int[winRates.Length],
                CumulativeExp = new double[winRates.Length],
                UpgradesAtStage = new int[winRates.Length],
                FirstWallStage = -1,
            };

            return new SimulationReport(
                new BalanceSnapshot(), SimFixture.Binding(), SimulationRun.Default(1, winRates.Length),
                new[] { PlayerProfile.Standard() },
                new[] { progression },
                new EconomyResult[0]);
        }

        [Test]
        public void ATrivialStretch_IsAWarning()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("monster_hp", "stage", 1, 40, new LinearModel(0d, 5d)));
                profile.Tracks.Add(new BalanceTrack("monster_atk", "stage", 1, 40, new LinearModel(0d, 1d)));

                var easy = Analyze(profile, 40).Issues.FirstOrDefault(i => i.RuleId == "too_easy");

                Assert.That(easy, Is.Not.Null);
                Assert.That(easy.Severity, Is.EqualTo(Severity.Warning));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AStatThatBarelyMoves_IsReportedAsFlat()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                // 1% per step, well under what a player can feel.
                profile.Tracks.Add(new BalanceTrack("monster_hp", "stage", 1, 40,
                    new ExponentialModel(1000d, 1.01d)));
                profile.Tracks.Add(new BalanceTrack("monster_atk", "stage", 1, 40, new LinearModel(0d, 30d)));

                var flat = Analyze(profile, 40).Issues.FirstOrDefault(i => i.RuleId == "flat_progression");

                Assert.That(flat, Is.Not.Null);
                Assert.That(flat.Severity, Is.EqualTo(Severity.Info));
                Assert.That(flat.TrackId, Is.EqualTo("monster_hp"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AStepInTheCurve_SurfacesAtThePositionItHappens()
        {
            // End to end, the claim is that a designer does not have to read the numbers to
            // find the step. Whether it lands as a spike or as a cliff depends on how far
            // the step goes, and both are the right answer; the position is what matters.
            var profile = SimFixture.Profile();
            try
            {
                profile.TryGetTrack("monster_hp", out var hp);
                hp.Model = new PiecewiseModel(
                    new PiecewiseSegment(1d, new PowerModel(70d, 1.05d)),
                    new PiecewiseSegment(25d, new PowerModel(230d, 1.05d)));

                var atStep = Analyze(profile, 45).Issues
                    .Where(i => i.Severity >= Severity.Warning && i.Covers(25))
                    .ToArray();

                Assert.That(atStep, Is.Not.Empty, "The step at 25 went unreported.");
                Assert.That(atStep.Any(i => i.RuleId == "difficulty_cliff"
                                         || i.RuleId == "difficulty_spike"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CurrencyWithNoSink_IsReportedAsInflation()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("monster_hp", "stage", 1, 40, new PowerModel(70d, 1.05d)));
                profile.Tracks.Add(new BalanceTrack("monster_atk", "stage", 1, 40, new PowerModel(6d, 0.9d)));
                profile.Tracks.Add(new BalanceTrack("stage_gold", "stage", 1, 40, new LinearModel(0d, 100d)));

                var issue = Analyze(profile, 40).Issues.FirstOrDefault(i => i.RuleId == "economy_inflation");

                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Severity, Is.EqualTo(Severity.Warning));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RunawayUpgradeCosts_AreReportedAsDeflation()
        {
            var profile = SimFixture.Profile();
            try
            {
                profile.TryGetTrack("upgrade_cost", out var cost);
                cost.Model = new ExponentialModel(100d, 2.5d);

                var issue = Analyze(profile).Issues.FirstOrDefault(i => i.RuleId == "economy_deflation");

                Assert.That(issue, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void IssuesAreOrderedBySeverityThenPosition()
        {
            var profile = SimFixture.Profile();
            try
            {
                profile.TryGetTrack("monster_hp", out var hp);
                hp.Model = new PiecewiseModel(
                    new PiecewiseSegment(1d, new LinearModel(0d, 5d)),
                    new PiecewiseSegment(40d, new LinearModel(0d, 1e9d)));

                var issues = Analyze(profile).Issues;

                for (var i = 1; i < issues.Count; i++)
                {
                    Assert.That((int)issues[i].Severity,
                        Is.LessThanOrEqualTo((int)issues[i - 1].Severity));

                    if (issues[i].Severity == issues[i - 1].Severity)
                    {
                        Assert.That(issues[i].IndexFrom,
                            Is.GreaterThanOrEqualTo(issues[i - 1].IndexFrom));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FindingsCanBeLookedUpByPosition()
        {
            var profile = SimFixture.Profile();
            try
            {
                profile.TryGetTrack("monster_hp", out var hp);
                hp.Model = new PiecewiseModel(
                    new PiecewiseSegment(1d, new PowerModel(70d, 1.05d)),
                    new PiecewiseSegment(30d, new LinearModel(0d, 1e9d)));

                var analysis = Analyze(profile);
                var atWall = analysis.At(45).ToArray();

                Assert.That(atWall, Is.Not.Empty, "The graph overlay needs findings by position.");
                Assert.That(atWall.All(i => i.Covers(45)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EveryIssueCarriesASuggestion()
        {
            // The difference between a report and an assistant.
            var profile = SimFixture.Profile();
            try
            {
                profile.TryGetTrack("monster_hp", out var hp);
                hp.Model = new PiecewiseModel(
                    new PiecewiseSegment(1d, new LinearModel(0d, 5d)),
                    new PiecewiseSegment(40d, new LinearModel(0d, 1e9d)));

                foreach (var issue in Analyze(profile).Issues)
                {
                    Assert.That(issue.Suggestion, Is.Not.Null.And.Not.Empty, issue.RuleId);
                    Assert.That(issue.Message, Is.Not.Null.And.Not.Empty, issue.RuleId);
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AllSevenRulesAreRegistered()
        {
            var ids = _analyzer.Rules.Select(r => r.Id).ToArray();

            Assert.That(ids, Is.EquivalentTo(new[]
            {
                "difficulty_spike", "difficulty_cliff", "too_easy",
                "economy_deflation", "economy_inflation",
                "flat_progression", "playtime_outlier",
            }));
        }
    }
}
