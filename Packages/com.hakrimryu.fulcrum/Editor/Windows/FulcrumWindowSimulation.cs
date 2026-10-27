using System.Collections.Generic;
using Fulcrum.Editor.Graph;
using Fulcrum.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// The simulation half of the window: driving runs, showing findings, overlaying them.
    /// </summary>
    public sealed partial class FulcrumWindow
    {
        // Findings overlap, and overlapping fills stack. At the previous strengths a
        // stretch covered by three of them turned into an opaque smear that read as a
        // rendering fault rather than as marked-up regions.
        private static readonly Color ErrorTint = new Color(0.95f, 0.35f, 0.35f, 0.085f);
        private static readonly Color WarningTint = new Color(1f, 0.72f, 0.30f, 0.065f);
        private static readonly Color InfoTint = new Color(0.5f, 0.7f, 1f, 0.045f);

        private SimulationDriver _driver;
        private BalanceAnalysis _analysis;
        private ScrollView _issuesList;
        private Label _issuesTitle;
        private VisualElement _legend;
        private Toggle _logScaleToggle;
        private Toggle _overlayToggle;
        private bool _dragging;

        /// <summary>Binding slots that are blank or point at nothing. Empty means runnable.</summary>
        private List<string> _missingBindings = new List<string>();

        private void BuildSimulation(VisualElement root)
        {
            _issuesList = root.Q<ScrollView>("issues-list");
            _issuesTitle = root.Q<Label>("issues-title");
            _legend = root.Q<VisualElement>("graph-legend");

            _driver = new SimulationDriver(rootVisualElement);
            _driver.Completed += OnAnalysisReady;
            _driver.Failed += exception => SetIssuesTitle($"Simulation failed: {exception.Message}");
        }

        private void BuildViewToggles(VisualElement toolbarRight)
        {
            _logScaleToggle = new Toggle("Log") { value = false };
            _logScaleToggle.RegisterValueChangedCallback(evt =>
            {
                if (_graph != null) _graph.LogScale = evt.newValue;
            });

            // Comparing presets and reading one track are different questions, and one
            // graph cannot answer both at once without becoming unreadable.
            _overlayToggle = new Toggle("Compare") { value = false };
            _overlayToggle.RegisterValueChangedCallback(_ => RefreshSelection());

            var reset = new Button(() => _graph?.ResetView()) { text = "Fit" };

            BuildTableToggle(toolbarRight);

            toolbarRight.Add(_logScaleToggle);
            toolbarRight.Add(_overlayToggle);
            toolbarRight.Add(reset);
        }

        /// <summary>Queues an analysis for the current profile.</summary>
        private void RequestSimulation()
        {
            _driver?.Cancel();
            _analysis = null;
            _missingBindings.Clear();
            RefreshGraph(SelectedTrack);
            if (_profile == null || _driver == null)
            {
                _analysis = null;
                RefreshIssues();
                return;
            }

            var binding = _profile.SimulationBinding;
            var settings = _profile.SimulationSettings;

            // Running against an unbound profile does not fail, it fabricates: the simulators
            // read a missing track as zeroes, and zero-health monsters produce a confident
            // report about a game nobody described.
            _missingBindings = binding.MissingBindings(_profile);
            if (_missingBindings.Count > 0)
            {
                _analysis = null;
                RefreshIssues();
                return;
            }

            var run = SimulationRun.Default(1, ResolveAxisLength(binding));
            run.Iterations = settings.Iterations;
            run.Seed = settings.Seed;

            // While a value is being dragged the shape matters more than the precision, so
            // the run is coarsened rather than skipped.
            if (_dragging) run = run.Coarse();

            RefreshIssues();
            _driver.Request(BalanceSnapshot.From(_profile, _cache.Evaluation), binding, run);
        }

        private int ResolveAxisLength(TrackBinding binding)
        {
            if (_profile.TryGetAxis(binding.AxisId, out var axis) && axis.Max > axis.Min)
            {
                return axis.Max;
            }

            var max = 1;
            foreach (var track in _profile.Tracks)
            {
                if (track != null && track.RangeMax > max) max = track.RangeMax;
            }

            return max;
        }

        private void OnAnalysisReady(BalanceAnalysis analysis)
        {
            _analysis = analysis;
            RefreshIssues();
            RefreshGraph(SelectedTrack);
        }

        private void SetIssuesTitle(string text)
        {
            if (_issuesTitle != null) _issuesTitle.text = text;
        }

        private void RefreshIssues()
        {
            if (_issuesList == null) return;

            _issuesList.Clear();

            if (_missingBindings.Count > 0)
            {
                ShowUnconfigured();
                return;
            }

            if (_analysis == null)
            {
                SetIssuesTitle("Issues");
                return;
            }

            var issues = _analysis.Issues;
            SetIssuesTitle(issues.Count == 0
                ? "Issues — none"
                : $"Issues ({_analysis.Count(Severity.Error)} errors, "
                  + $"{_analysis.Count(Severity.Warning)} warnings, {_analysis.Count(Severity.Info)} info)");

            if (issues.Count == 0)
            {
                _issuesList.Add(new Label("Nothing flagged.") { });
                return;
            }

            foreach (var issue in issues)
            {
                _issuesList.Add(BuildIssueRow(issue));
            }

            var progression = SelectedProgression();
            if (progression.HasValue && progression.Value.CumulativePlayTimeSec != null)
            {
                _issuesList.Add(new Label(
                    $"Predicted playtime to {progression.Value.ReachedStage}: "
                    + $"{progression.Value.TotalHours:F1} h")
                { style = { paddingLeft = 8f, paddingTop = 4f, opacity = 0.75f } });
            }
        }

        /// <summary>
        /// Explains why there is no analysis, instead of leaving the pane blank.
        /// </summary>
        /// <remarks>
        /// A profile that describes something other than a stage-clearing game — a pity
        /// curve, a price table — will never fill these slots, and that is a legitimate use
        /// of the tool. Saying so is different from reporting nothing, and very different
        /// from reporting a win rate.
        /// </remarks>
        private void ShowUnconfigured()
        {
            SetIssuesTitle("Simulation not configured");

            var lead = new Label(
                "The curves are still evaluated, graphed and exported. Simulation needs to "
                + "know which track is which before it can say anything about play.");
            lead.style.paddingLeft = 8f;
            lead.style.paddingTop = 4f;
            lead.style.whiteSpace = WhiteSpace.Normal;
            _issuesList.Add(lead);

            var missing = new Label("Not yet bound: " + string.Join(", ", _missingBindings.ToArray()));
            missing.style.paddingLeft = 8f;
            missing.style.paddingTop = 4f;
            missing.style.opacity = 0.75f;
            missing.style.whiteSpace = WhiteSpace.Normal;
            _issuesList.Add(missing);

            var how = new Label("Set them under Profile → Simulation binding.");
            how.style.paddingLeft = 8f;
            how.style.paddingTop = 4f;
            how.style.opacity = 0.75f;
            _issuesList.Add(how);
        }

        private ProgressionResult? SelectedProgression()
        {
            var report = _analysis?.Report;
            if (report == null || report.BaselineIndex < 0) return null;
            return report.Progressions[report.BaselineIndex];
        }

        private VisualElement BuildIssueRow(ValidationIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList("fulcrum-issue");
            row.AddToClassList(issue.Severity == Severity.Error ? "fulcrum-issue--error"
                : issue.Severity == Severity.Warning ? "fulcrum-issue--warning"
                : "fulcrum-issue--info");

            var message = new Label(issue.Message);
            message.AddToClassList("fulcrum-issue__message");
            row.Add(message);

            if (!string.IsNullOrEmpty(issue.Suggestion))
            {
                var suggestion = new Label(issue.Suggestion);
                suggestion.AddToClassList("fulcrum-issue__suggestion");
                row.Add(suggestion);
            }

            // Clicking a finding frames it, so a report about position 42 is one click away
            // from showing position 42.
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var pad = Mathf.Max(5, (issue.IndexTo - issue.IndexFrom) / 2);
                _graph?.SetView(issue.IndexFrom - pad, issue.IndexTo + pad);
            });

            return row;
        }

        /// <summary>Shaded spans for the findings that apply to the graph.</summary>
        private List<GraphHighlight> BuildHighlights()
        {
            var highlights = new List<GraphHighlight>();
            if (_analysis == null) return highlights;

            foreach (var issue in _analysis.Issues)
            {
                var color = issue.Severity == Severity.Error ? ErrorTint
                    : issue.Severity == Severity.Warning ? WarningTint
                    : InfoTint;

                highlights.Add(new GraphHighlight(issue.IndexFrom, issue.IndexTo, color, issue.Message));
            }

            return highlights;
        }

        /// <summary>Win-rate curves for every simulated preset, for the comparison view.</summary>
        private List<GraphSeries> BuildComparisonSeries()
        {
            var series = new List<GraphSeries>();
            var report = _analysis?.Report;
            if (report?.Progressions == null) return series;

            for (var i = 0; i < report.Progressions.Count; i++)
            {
                var progression = report.Progressions[i];
                if (progression.WinRateAtStage == null) continue;

                var name = report.Players != null && i < report.Players.Count
                    ? report.Players[i].Name
                    : $"player {i}";

                series.Add(new GraphSeries(
                    name, progression.WinRateAtStage, progression.RangeMin, GraphSeries.ColorAt(i)));
            }

            return series;
        }

        private void RefreshLegend(IReadOnlyList<GraphSeries> series)
        {
            if (_legend == null) return;

            _legend.Clear();
            if (series == null || series.Count <= 1) return;

            foreach (var entry in series)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var row = new VisualElement();
                row.AddToClassList("fulcrum-legend__entry");

                var swatch = new VisualElement();
                swatch.AddToClassList("fulcrum-legend__swatch");
                swatch.style.backgroundColor = entry.Color;
                row.Add(swatch);
                row.Add(new Label(entry.Name) { style = { fontSize = 10f } });

                _legend.Add(row);
            }
        }
    }
}
