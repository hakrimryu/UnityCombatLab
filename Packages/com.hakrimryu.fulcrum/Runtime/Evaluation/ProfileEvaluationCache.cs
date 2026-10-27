using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// Keeps a profile's evaluated series up to date, redoing only what a change affects.
    /// </summary>
    /// <remarks>
    /// Editing is continuous — a slider drag produces a change every frame — while most of
    /// a profile is unaffected by any single edit. Re-evaluating everything would scale
    /// with profile size instead of with the size of the change, and a designer would feel
    /// it exactly when they are iterating hardest.
    ///
    /// What to redo comes from the dependency graph: a track and everything downstream of
    /// it, and nothing else.
    ///
    /// Structural edits are separate. Adding or removing a track, or changing which tracks
    /// a model reads, changes the graph itself, so the shape has to be rebuilt before the
    /// values are. Callers that cannot tell the difference should report a structural
    /// change; the cost is one rebuild, and the alternative is a stale order.
    /// </remarks>
    public sealed class ProfileEvaluationCache
    {
        private readonly HashSet<string> _dirty = new HashSet<string>();
        private BalanceProfile _profile;
        private DependencyGraph _graph;
        private ProfileEvaluation _evaluation;
        private bool _structureDirty = true;

        public ProfileEvaluationCache(BalanceProfile profile = null) => _profile = profile;

        /// <summary>
        /// How many individual track evaluations have run since the cache was created.
        /// </summary>
        /// <remarks>
        /// Exposed so the saving can be asserted rather than assumed — a cache that
        /// silently stopped skipping work would still return correct values.
        /// </remarks>
        public int EvaluationCount { get; private set; }

        /// <summary>The profile being tracked. Assigning a different one resets everything.</summary>
        public BalanceProfile Profile
        {
            get => _profile;
            set
            {
                if (ReferenceEquals(_profile, value)) return;
                _profile = value;
                InvalidateStructure();
            }
        }

        /// <summary>Dependency information for the current structure.</summary>
        public DependencyGraph Graph
        {
            get
            {
                EnsureUpToDate();
                return _graph;
            }
        }

        /// <summary>The evaluated profile, brought up to date on access.</summary>
        public ProfileEvaluation Evaluation
        {
            get
            {
                EnsureUpToDate();
                return _evaluation;
            }
        }

        /// <summary>The series for a track, or null when it has none.</summary>
        public double[] GetSeries(string trackId)
        {
            EnsureUpToDate();
            return _evaluation[trackId];
        }

        /// <summary>
        /// Marks a track's values stale, along with everything that reads it.
        /// </summary>
        /// <remarks>
        /// Use this for parameter edits. If the edit changed which tracks the model reads,
        /// call <see cref="InvalidateStructure"/> instead.
        /// </remarks>
        public void InvalidateTrack(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return;

            if (_structureDirty || _graph == null)
            {
                InvalidateStructure();
                return;
            }

            foreach (var affected in _graph.AffectedBy(trackId)) _dirty.Add(affected);
        }

        /// <summary>Marks every track's values stale, keeping the current graph.</summary>
        /// <remarks>For edits that reach everything at once, such as a profile-wide constant.</remarks>
        public void InvalidateValues()
        {
            if (_graph == null)
            {
                InvalidateStructure();
                return;
            }

            foreach (var id in _graph.EvaluationOrder) _dirty.Add(id);
        }

        /// <summary>Discards the dependency graph as well as the values.</summary>
        public void InvalidateStructure()
        {
            _structureDirty = true;
            _dirty.Clear();
        }

        private void EnsureUpToDate()
        {
            if (_structureDirty)
            {
                _graph = DependencyGraph.Build(_profile);
                _evaluation = new ProfileEvaluation(_graph);
                _structureDirty = false;

                _dirty.Clear();
                foreach (var id in _graph.EvaluationOrder) _dirty.Add(id);
            }

            if (_dirty.Count == 0) return;

            var globals = _profile != null
                ? _profile.BuildGlobals()
                : new Dictionary<string, double>();

            // The resolver is the evaluation itself, so a dirty track reads the freshly
            // stored values of its dependencies rather than a copy taken before the edit.
            var context = new EvalContext(0, globals, _evaluation);

            // Walking the full order rather than the dirty set keeps dependencies ahead of
            // dependents without sorting the subset again.
            foreach (var id in _graph.EvaluationOrder)
            {
                if (!_dirty.Contains(id)) continue;
                if (_profile == null || !_profile.TryGetTrack(id, out var track)) continue;

                _evaluation.Store(track, TrackEvaluator.Evaluate(track, context));
                EvaluationCount++;
            }

            _dirty.Clear();
        }
    }
}
