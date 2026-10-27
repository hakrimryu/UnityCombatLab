using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// The evaluated series of every track in a profile, plus what went wrong.
    /// </summary>
    /// <remarks>
    /// Also acts as the <see cref="ITrackResolver"/> handed to models while the profile is
    /// being evaluated, so a derived track reads the same array its dependents will.
    /// </remarks>
    public sealed class ProfileEvaluation : ITrackResolver
    {
        private readonly Dictionary<string, double[]> _series = new Dictionary<string, double[]>();
        private readonly Dictionary<string, BalanceTrack> _tracks = new Dictionary<string, BalanceTrack>();

        internal ProfileEvaluation(DependencyGraph graph) => Graph = graph;

        /// <summary>Dependency information the evaluation was ordered by.</summary>
        public DependencyGraph Graph { get; }

        /// <summary>Track ids that produced a series, in evaluation order.</summary>
        public IReadOnlyCollection<string> TrackIds => _series.Keys;

        /// <summary>Whether every track could be evaluated.</summary>
        public bool IsComplete => Graph.IsValid;

        /// <summary>The series for a track, or null when it was not evaluated.</summary>
        public double[] this[string trackId]
            => _series.TryGetValue(trackId, out var values) ? values : null;

        /// <summary>The series for a track.</summary>
        public bool TryGetSeries(string trackId, out double[] values)
            => _series.TryGetValue(trackId, out values);

        internal void Store(BalanceTrack track, double[] values)
        {
            _tracks[track.Id] = track;
            _series[track.Id] = values;
        }

        /// <inheritdoc />
        public bool TryGet(string trackId, double x, out double value)
        {
            value = 0d;

            if (string.IsNullOrEmpty(trackId)) return false;
            if (!_series.TryGetValue(trackId, out var values)) return false;
            if (!_tracks.TryGetValue(trackId, out var track)) return false;

            // Referenced positions are read at whole steps. A track is a sequence of
            // decisions at each position, not a continuous function, so interpolating
            // between two of them would invent a value nobody chose.
            var index = track.IndexOfAxisPosition((int)System.Math.Round(x));
            if (index < 0 || index >= values.Length) return false;

            value = values[index];
            return true;
        }
    }
}
