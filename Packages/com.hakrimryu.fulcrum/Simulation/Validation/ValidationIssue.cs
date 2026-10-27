using System.Collections.Generic;

namespace Fulcrum.Simulation
{
    /// <summary>How much a finding should interrupt.</summary>
    public enum Severity
    {
        /// <summary>Worth knowing; not necessarily wrong.</summary>
        Info = 0,

        /// <summary>Probably unintended.</summary>
        Warning = 1,

        /// <summary>Broken as it stands.</summary>
        Error = 2,
    }

    /// <summary>
    /// One thing a rule noticed, tied to where on the axis it happens.
    /// </summary>
    /// <remarks>
    /// <see cref="Suggestion"/> is what turns this from a report into help. A message
    /// saying difficulty spikes at stage 42 states a fact; one that also says which curve
    /// to flatten is the difference between a tool and an assistant.
    /// </remarks>
    public sealed class ValidationIssue
    {
        public Severity Severity { get; }

        /// <summary>Rule that produced this, for filtering and suppression.</summary>
        public string RuleId { get; }

        /// <summary>Track the finding concerns, if any.</summary>
        public string TrackId { get; }

        /// <summary>Axis range the finding covers, inclusive.</summary>
        public int IndexFrom { get; }

        /// <inheritdoc cref="IndexFrom" />
        public int IndexTo { get; }

        public string Message { get; }

        /// <summary>What to do about it.</summary>
        public string Suggestion { get; }

        public ValidationIssue(
            Severity severity, string ruleId, string message,
            int indexFrom, int indexTo, string trackId = null, string suggestion = null)
        {
            Severity = severity;
            RuleId = ruleId;
            Message = message;
            IndexFrom = indexFrom;
            IndexTo = indexTo;
            TrackId = trackId;
            Suggestion = suggestion;
        }

        /// <summary>Whether an axis position falls inside this finding.</summary>
        public bool Covers(int axisPosition) => axisPosition >= IndexFrom && axisPosition <= IndexTo;

        public override string ToString()
            => IndexFrom == IndexTo
                ? $"[{Severity}] {Message} (at {IndexFrom})"
                : $"[{Severity}] {Message} ({IndexFrom}–{IndexTo})";
    }

    /// <summary>Everything the simulators produced, for the rules to read.</summary>
    public sealed class SimulationReport
    {
        public SimulationReport(
            BalanceSnapshot balance,
            TrackBinding binding,
            SimulationRun run,
            IReadOnlyList<PlayerProfile> players,
            IReadOnlyList<ProgressionResult> progressions,
            IReadOnlyList<EconomyResult> economies)
        {
            Balance = balance;
            Binding = binding;
            Run = run;
            Players = players;
            Progressions = progressions;
            Economies = economies;
        }

        public BalanceSnapshot Balance { get; }
        public TrackBinding Binding { get; }
        public SimulationRun Run { get; }

        /// <summary>Players simulated, aligned with <see cref="Progressions"/>.</summary>
        public IReadOnlyList<PlayerProfile> Players { get; }

        public IReadOnlyList<ProgressionResult> Progressions { get; }
        public IReadOnlyList<EconomyResult> Economies { get; }

        /// <summary>
        /// The run used as the baseline for rules that need a single answer.
        /// </summary>
        /// <remarks>
        /// The middle preset, not the best or worst: a rule that fires only for the
        /// strongest player reports something almost nobody experiences.
        /// </remarks>
        public int BaselineIndex => Progressions == null || Progressions.Count == 0
            ? -1
            : Progressions.Count / 2;
    }

    /// <summary>A check run against simulation output.</summary>
    /// <remarks>
    /// Implement this in your own project and the editor picks it up automatically; rules
    /// are as much an extension point as models are.
    /// </remarks>
    public interface IValidationRule
    {
        /// <summary>Stable id, used for display and suppression.</summary>
        string Id { get; }

        /// <summary>Findings, or nothing.</summary>
        IEnumerable<ValidationIssue> Validate(SimulationReport report);
    }
}
