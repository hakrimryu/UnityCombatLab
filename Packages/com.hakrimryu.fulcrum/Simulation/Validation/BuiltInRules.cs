using System;
using System.Collections.Generic;

namespace Fulcrum.Simulation
{
    /// <summary>
    /// A jump in time to kill from one position to the next.
    /// </summary>
    /// <remarks>
    /// Difficulty is expected to rise; what players notice is a step, not a slope. The
    /// threshold is a ratio rather than an absolute because the same extra thirty seconds
    /// is unremarkable late and jarring early.
    /// </remarks>
    public sealed class DifficultySpikeRule : IValidationRule
    {
        public string Id => "difficulty_spike";

        /// <summary>Ratio against the previous position that counts as a spike.</summary>
        public double Threshold { get; set; } = 1.5d;

        /// <summary>
        /// Shortest fight the rule will compare against.
        /// </summary>
        /// <remarks>
        /// A ratio computed on very short fights is noise: going from one turn to two is a
        /// doubling that nobody notices, and reporting it buries the spikes that matter
        /// under a run of findings at the start of every profile.
        /// </remarks>
        public double MinimumTurns { get; set; } = 4d;

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            var index = report.BaselineIndex;
            if (index < 0) yield break;

            var progression = report.Progressions[index];
            var turns = progression.TurnsAtStage;
            if (turns == null) yield break;

            for (var i = 1; i < turns.Length; i++)
            {
                if (turns[i - 1] < MinimumTurns) continue;
                if (turns[i - 1] <= 0d || turns[i] <= 0d) continue;

                var ratio = turns[i] / turns[i - 1];
                if (ratio < Threshold) continue;

                var stage = progression.RangeMin + i;
                yield return new ValidationIssue(
                    Severity.Warning, Id,
                    $"Time to kill jumps {ratio:F1}× at {stage}.",
                    stage, stage,
                    suggestion: "Flatten the monster stat curve around here, or move the "
                              + "increase earlier so it arrives gradually.");
            }
        }
    }

    /// <summary>A position most players simply cannot clear.</summary>
    public sealed class DifficultyCliffRule : IValidationRule
    {
        public string Id => "difficulty_cliff";

        public double Threshold { get; set; } = 0.15d;

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            var index = report.BaselineIndex;
            if (index < 0) yield break;

            var progression = report.Progressions[index];
            var rates = progression.WinRateAtStage;
            if (rates == null) yield break;

            var from = -1;
            for (var i = 0; i <= rates.Length; i++)
            {
                var below = i < rates.Length && rates[i] < Threshold;

                if (below && from < 0) from = i;
                else if (!below && from >= 0)
                {
                    // Reported as one range rather than per position: a wall twenty stages
                    // wide is one problem, not twenty.
                    yield return new ValidationIssue(
                        Severity.Error, Id,
                        $"Win rate below {Threshold:P0} from {progression.RangeMin + from} "
                        + $"to {progression.RangeMin + i - 1}.",
                        progression.RangeMin + from, progression.RangeMin + i - 1,
                        suggestion: "Progression stops here for the average player. Lower the "
                                  + "monster curve, or give more experience or currency before it.");
                    from = -1;
                }
            }
        }
    }

    /// <summary>A stretch with no meaningful chance of losing.</summary>
    public sealed class TooEasyRule : IValidationRule
    {
        public string Id => "too_easy";

        public double Threshold { get; set; } = 0.95d;

        /// <summary>Consecutive positions before a stretch is reported.</summary>
        public int RunLength { get; set; } = 5;

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            var index = report.BaselineIndex;
            if (index < 0) yield break;

            var progression = report.Progressions[index];
            var rates = progression.WinRateAtStage;
            if (rates == null) yield break;

            var from = -1;
            for (var i = 0; i <= rates.Length; i++)
            {
                var easy = i < rates.Length && rates[i] > Threshold;

                if (easy && from < 0) from = i;
                else if (!easy && from >= 0)
                {
                    if (i - from >= RunLength)
                    {
                        yield return new ValidationIssue(
                            Severity.Warning, Id,
                            $"Win rate above {Threshold:P0} for {i - from} positions "
                            + $"from {progression.RangeMin + from}.",
                            progression.RangeMin + from, progression.RangeMin + i - 1,
                            suggestion: "Nothing is at stake through this stretch. Raise the "
                                      + "monster curve or shorten the run.");
                    }

                    from = -1;
                }
            }
        }
    }

    /// <summary>Currency accumulating with nothing to spend it on.</summary>
    public sealed class EconomyInflationRule : IValidationRule
    {
        public string Id => "economy_inflation";

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            if (report.Economies == null) yield break;

            for (var i = 0; i < report.Economies.Count; i++)
            {
                var economy = report.Economies[i];
                if (economy.InflationStartStage < 0) continue;

                yield return new ValidationIssue(
                    Severity.Warning, Id,
                    $"Currency stops being spent from {economy.InflationStartStage} "
                    + $"({PlayerName(report, i)}).",
                    economy.InflationStartStage,
                    economy.RangeMin + (economy.NetBalance?.Length ?? 1) - 1,
                    suggestion: "Add a sink, or raise upgrade costs so the surplus has "
                              + "somewhere to go.");
            }
        }

        internal static string PlayerName(SimulationReport report, int index)
            => report.Players != null && index < report.Players.Count
                ? report.Players[index].Name
                : "player";
    }

    /// <summary>Upgrades drifting out of reach of what a position pays.</summary>
    public sealed class EconomyDeflationRule : IValidationRule
    {
        public string Id => "economy_deflation";

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            if (report.Economies == null) yield break;

            for (var i = 0; i < report.Economies.Count; i++)
            {
                var economy = report.Economies[i];
                if (economy.DeflationStartStage < 0) continue;

                yield return new ValidationIssue(
                    Severity.Warning, Id,
                    $"The next upgrade costs more than 20 positions of income "
                    + $"from {economy.DeflationStartStage} ({EconomyInflationRule.PlayerName(report, i)}).",
                    economy.DeflationStartStage,
                    economy.RangeMin + (economy.NetBalance?.Length ?? 1) - 1,
                    suggestion: "Flatten the cost curve or raise income; progression has "
                              + "stopped depending on play here.");
            }
        }
    }

    /// <summary>A stretch where a track barely moves.</summary>
    /// <remarks>
    /// Reads the numbers rather than the simulation: a stat that grows by under a few
    /// percent per step is one the player cannot feel changing, whatever the win rate does.
    /// </remarks>
    public sealed class FlatProgressionRule : IValidationRule
    {
        public string Id => "flat_progression";

        /// <summary>Relative growth per position below which a stretch is flat.</summary>
        public double Threshold { get; set; } = 0.03d;

        /// <summary>Consecutive positions before a stretch is reported.</summary>
        public int RunLength { get; set; } = 8;

        /// <summary>Tracks to examine. Empty means the bound monster stats.</summary>
        public IReadOnlyList<string> TrackIds { get; set; }

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            if (report.Balance == null) yield break;

            var ids = TrackIds;
            if (ids == null || ids.Count == 0)
            {
                ids = new[] { report.Binding?.MonsterHp, report.Binding?.MonsterAttack };
            }

            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!report.Balance.TryGetRange(id, out var from, out var to)) continue;

                var runStart = -1;
                for (var position = from; position <= to + 1; position++)
                {
                    var flat = position <= to && position > from && IsFlat(report.Balance, id, position);

                    if (flat && runStart < 0) runStart = position;
                    else if (!flat && runStart >= 0)
                    {
                        if (position - runStart >= RunLength)
                        {
                            yield return new ValidationIssue(
                                Severity.Info, Id,
                                $"'{id}' grows under {Threshold:P0} per step "
                                + $"from {runStart} to {position - 1}.",
                                runStart, position - 1, id,
                                suggestion: "Players cannot feel a change this small. Steepen "
                                          + "the curve here or shorten the stretch.");
                        }

                        runStart = -1;
                    }
                }
            }
        }

        private bool IsFlat(BalanceSnapshot balance, string id, int position)
        {
            if (!balance.TryGet(id, position - 1, out var previous)) return false;
            if (!balance.TryGet(id, position, out var current)) return false;
            if (previous <= 0d) return false;

            return (current - previous) / previous < Threshold;
        }
    }

    /// <summary>Uneven pacing across equal slices of the axis.</summary>
    public sealed class PlaytimeOutlierRule : IValidationRule
    {
        public string Id => "playtime_outlier";

        /// <summary>Positions per slice.</summary>
        public int ChapterLength { get; set; } = 10;

        /// <summary>Ratio against the mean slice that counts as an outlier.</summary>
        public double Threshold { get; set; } = 2d;

        public IEnumerable<ValidationIssue> Validate(SimulationReport report)
        {
            var index = report.BaselineIndex;
            if (index < 0) yield break;

            var progression = report.Progressions[index];
            var cumulative = progression.CumulativePlayTimeSec;
            if (cumulative == null || cumulative.Length < ChapterLength * 2) yield break;

            var chapters = new List<double>();
            var starts = new List<int>();

            for (var i = 0; i < cumulative.Length; i += ChapterLength)
            {
                var end = Math.Min(i + ChapterLength, cumulative.Length) - 1;
                var before = i == 0 ? 0d : cumulative[i - 1];
                chapters.Add(cumulative[end] - before);
                starts.Add(progression.RangeMin + i);
            }

            var total = 0d;
            foreach (var chapter in chapters) total += chapter;
            var mean = total / chapters.Count;
            if (mean <= 0d) yield break;

            for (var i = 0; i < chapters.Count; i++)
            {
                var ratio = chapters[i] / mean;
                if (ratio < Threshold) continue;

                var last = Math.Min(starts[i] + ChapterLength - 1,
                    progression.RangeMin + cumulative.Length - 1);

                yield return new ValidationIssue(
                    Severity.Info, Id,
                    $"Positions {starts[i]}–{last} take {ratio:F1}× the average chapter "
                    + $"({chapters[i] / 3600d:F1} h).",
                    starts[i], last,
                    suggestion: "Pacing is uneven here. Check whether the stretch is meant "
                              + "to be a milestone or is simply grindy.");
            }
        }
    }
}
