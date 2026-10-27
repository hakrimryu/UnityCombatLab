using System.Collections.Generic;

namespace Fulcrum.Simulation
{
    /// <summary>
    /// Runs the simulators over a profile and puts the rules to the result.
    /// </summary>
    /// <remarks>
    /// The single entry point the editor needs: numbers in, findings out. Keeping the
    /// orchestration here rather than in the window means the same analysis can run from a
    /// build script with no editor present.
    /// </remarks>
    public sealed class BalanceAnalyzer
    {
        private readonly ProgressionSimulator _progression = new ProgressionSimulator();
        private readonly EconomySimulator _economy = new EconomySimulator();

        /// <summary>Rules to apply. Defaults to the built-in set.</summary>
        public List<IValidationRule> Rules { get; } = new List<IValidationRule>
        {
            new DifficultyCliffRule(),
            new DifficultySpikeRule(),
            new TooEasyRule(),
            new EconomyDeflationRule(),
            new EconomyInflationRule(),
            new FlatProgressionRule(),
            new PlaytimeOutlierRule(),
        };

        /// <summary>Simulates every player and collects what the rules find.</summary>
        public BalanceAnalysis Analyze(
            BalanceSnapshot balance,
            TrackBinding binding,
            SimulationRun run,
            IReadOnlyList<PlayerProfile> players = null)
        {
            players = players ?? PlayerProfile.Presets();

            var progressions = new List<ProgressionResult>(players.Count);
            var economies = new List<EconomyResult>(players.Count);

            foreach (var player in players)
            {
                progressions.Add(_progression.Simulate(balance, binding, player, run));
                economies.Add(_economy.Simulate(balance, binding, player, run));
            }

            var report = new SimulationReport(balance, binding, run, players, progressions, economies);
            var issues = new List<ValidationIssue>();

            foreach (var rule in Rules)
            {
                if (rule == null) continue;

                var found = rule.Validate(report);
                if (found == null) continue;

                foreach (var issue in found)
                {
                    if (issue != null) issues.Add(issue);
                }
            }

            // Severity first so the thing that blocks progression is not buried under
            // pacing notes; position second so the list reads along the axis.
            issues.Sort((a, b) =>
            {
                var bySeverity = b.Severity.CompareTo(a.Severity);
                return bySeverity != 0 ? bySeverity : a.IndexFrom.CompareTo(b.IndexFrom);
            });

            return new BalanceAnalysis(report, issues);
        }
    }

    /// <summary>A simulation report together with what the rules made of it.</summary>
    public sealed class BalanceAnalysis
    {
        public BalanceAnalysis(SimulationReport report, IReadOnlyList<ValidationIssue> issues)
        {
            Report = report;
            Issues = issues;
        }

        public SimulationReport Report { get; }

        /// <summary>Findings, most severe first.</summary>
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public bool HasErrors => Count(Severity.Error) > 0;

        public int Count(Severity severity)
        {
            var total = 0;
            foreach (var issue in Issues)
            {
                if (issue.Severity == severity) total++;
            }

            return total;
        }

        /// <summary>Findings that cover an axis position, for highlighting on the graph.</summary>
        public IEnumerable<ValidationIssue> At(int axisPosition)
        {
            foreach (var issue in Issues)
            {
                if (issue.Covers(axisPosition)) yield return issue;
            }
        }
    }
}
