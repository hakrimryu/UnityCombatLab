using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// Works out what order a profile's tracks have to be evaluated in.
    /// </summary>
    /// <remarks>
    /// Once one track can read another, evaluating them in authoring order is wrong: a
    /// derived value would be computed before its inputs and silently read stale or
    /// missing numbers. Sorting by dependency fixes that, and the same pass is what
    /// detects a cycle.
    ///
    /// Cycles are reported as the path that forms them rather than as a bare "there is a
    /// cycle". A designer told <c>monster_hp → stage_gold → monster_hp</c> can fix it;
    /// one told a cycle exists somewhere in forty tracks cannot.
    /// </remarks>
    public sealed class DependencyGraph
    {
        private readonly List<string> _order = new List<string>();
        private readonly List<IReadOnlyList<string>> _cycles = new List<IReadOnlyList<string>>();
        private readonly Dictionary<string, List<string>> _dependencies =
            new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> _dependents =
            new Dictionary<string, List<string>>();
        private readonly List<MissingReference> _missing = new List<MissingReference>();

        /// <summary>A track that refers to an id no track in the profile provides.</summary>
        public readonly struct MissingReference
        {
            public readonly string TrackId;
            public readonly string MissingTrackId;

            public MissingReference(string trackId, string missingTrackId)
            {
                TrackId = trackId;
                MissingTrackId = missingTrackId;
            }

            public override string ToString() => $"{TrackId} → {MissingTrackId}";
        }

        /// <summary>Track ids in an order where every dependency precedes its dependents.</summary>
        /// <remarks>Tracks caught in a cycle are excluded; there is no valid position for them.</remarks>
        public IReadOnlyList<string> EvaluationOrder => _order;

        /// <summary>Every cycle found, each as the path that closes it.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Cycles => _cycles;

        /// <summary>References to tracks the profile does not define.</summary>
        public IReadOnlyList<MissingReference> MissingReferences => _missing;

        /// <summary>Whether the profile can be evaluated as it stands.</summary>
        public bool IsValid => _cycles.Count == 0;

        /// <summary>Ids a track reads directly.</summary>
        public IReadOnlyList<string> DependenciesOf(string trackId)
            => _dependencies.TryGetValue(trackId, out var list) ? list : System.Array.Empty<string>();

        /// <summary>Ids that read a track directly.</summary>
        public IReadOnlyList<string> DependentsOf(string trackId)
            => _dependents.TryGetValue(trackId, out var list) ? list : System.Array.Empty<string>();

        /// <summary>
        /// Every track that would need re-evaluating when <paramref name="trackId"/> changes,
        /// including the track itself, in evaluation order.
        /// </summary>
        public IReadOnlyList<string> AffectedBy(string trackId)
        {
            var affected = new HashSet<string>();
            var pending = new Stack<string>();
            pending.Push(trackId);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!affected.Add(current)) continue;

                foreach (var dependent in DependentsOf(current)) pending.Push(dependent);
            }

            // Returning them in evaluation order means a caller can just walk the result.
            var result = new List<string>(affected.Count);
            foreach (var id in _order)
            {
                if (affected.Contains(id)) result.Add(id);
            }

            return result;
        }

        /// <summary>Builds the graph for a profile.</summary>
        public static DependencyGraph Build(BalanceProfile profile)
        {
            var graph = new DependencyGraph();
            if (profile == null) return graph;

            var known = new HashSet<string>();
            var tracks = profile.Tracks;

            foreach (var track in tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                known.Add(track.Id);
            }

            foreach (var track in tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                if (graph._dependencies.ContainsKey(track.Id)) continue;

                var references = new HashSet<string>();
                track.Model?.CollectTrackReferences(references);

                var edges = new List<string>();
                foreach (var reference in references)
                {
                    if (string.IsNullOrEmpty(reference)) continue;

                    if (!known.Contains(reference))
                    {
                        graph._missing.Add(new MissingReference(track.Id, reference));
                        continue;
                    }

                    // A track reading itself is a cycle of length one; the sort reports it.
                    edges.Add(reference);
                }

                graph._dependencies[track.Id] = edges;
            }

            foreach (var pair in graph._dependencies)
            {
                foreach (var dependency in pair.Value)
                {
                    if (!graph._dependents.TryGetValue(dependency, out var list))
                    {
                        list = new List<string>();
                        graph._dependents[dependency] = list;
                    }

                    if (!list.Contains(pair.Key)) list.Add(pair.Key);
                }
            }

            graph.Sort(tracks);
            return graph;
        }

        /// <summary>
        /// Depth-first topological sort that records the path whenever it re-enters a node
        /// still on the stack.
        /// </summary>
        private void Sort(IReadOnlyList<BalanceTrack> tracks)
        {
            var state = new Dictionary<string, VisitState>();
            var path = new List<string>();

            foreach (var track in tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                Visit(track.Id, state, path);
            }

            if (_cycles.Count == 0) return;

            // Detecting a back edge stops that branch, but the nodes above it on the stack
            // still unwind normally and land in the order. There is no valid position for a
            // track whose own value depends on itself, so they are removed afterwards.
            //
            // Tracks merely downstream of a cycle stay: their input is unavailable, which
            // reads as zero exactly like a reference to a track that does not exist.
            var inCycle = new HashSet<string>();
            foreach (var cycle in _cycles)
            {
                foreach (var id in cycle) inCycle.Add(id);
            }

            _order.RemoveAll(inCycle.Contains);
        }

        private enum VisitState { Visiting, Done }

        private void Visit(string id, Dictionary<string, VisitState> state, List<string> path)
        {
            if (state.TryGetValue(id, out var current))
            {
                if (current == VisitState.Done) return;

                // Back edge: everything from the earlier visit of this id onward is the cycle.
                var start = path.LastIndexOf(id);
                if (start >= 0) RecordCycle(path, start, id);
                return;
            }

            state[id] = VisitState.Visiting;
            path.Add(id);

            foreach (var dependency in DependenciesOf(id))
            {
                Visit(dependency, state, path);
            }

            path.RemoveAt(path.Count - 1);
            state[id] = VisitState.Done;
            _order.Add(id);
        }

        private void RecordCycle(List<string> path, int start, string closing)
        {
            var cycle = new List<string>(path.Count - start + 1);
            for (var i = start; i < path.Count; i++) cycle.Add(path[i]);
            cycle.Add(closing);

            foreach (var existing in _cycles)
            {
                if (SameCycle(existing, cycle)) return;
            }

            _cycles.Add(cycle);
        }

        /// <summary>
        /// Two paths describe the same cycle when they contain the same ids. The traversal
        /// can reach one cycle from several entry points, and reporting it once per entry
        /// would bury the real count.
        /// </summary>
        private static bool SameCycle(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;

            var seen = new HashSet<string>(a);
            foreach (var id in b)
            {
                if (!seen.Contains(id)) return false;
            }

            return true;
        }

        /// <summary>Renders the cycles for display, e.g. <c>a → b → a</c>.</summary>
        public string DescribeCycles()
        {
            if (_cycles.Count == 0) return string.Empty;

            var lines = new List<string>(_cycles.Count);
            foreach (var cycle in _cycles) lines.Add(string.Join(" → ", cycle));
            return string.Join("; ", lines);
        }
    }
}
