using System.Collections.Generic;

namespace Fulcrum
{
    /// <summary>
    /// Evaluated numbers, detached from the assets that produced them.
    /// </summary>
    /// <remarks>
    /// The simulators are plain C# — <c>Fulcrum.Simulation</c> declares
    /// <c>noEngineReferences</c> — so they cannot take a <see cref="BalanceProfile"/>,
    /// which is a <c>ScriptableObject</c>. Handing them a snapshot keeps that boundary
    /// real rather than aspirational, and it means a simulation can run against numbers
    /// from anywhere: a profile, a baked table, or a test fixture written by hand.
    /// </remarks>
    public sealed class BalanceSnapshot
    {
        private readonly Dictionary<string, Series> _series = new Dictionary<string, Series>();

        private readonly struct Series
        {
            public readonly int RangeMin;
            public readonly double[] Values;

            public Series(int rangeMin, double[] values)
            {
                RangeMin = rangeMin;
                Values = values;
            }
        }

        /// <summary>Track ids present.</summary>
        public IReadOnlyCollection<string> TrackIds => _series.Keys;

        /// <summary>Adds or replaces a track's values.</summary>
        public void Set(string trackId, int rangeMin, double[] values)
        {
            if (string.IsNullOrEmpty(trackId) || values == null) return;
            _series[trackId] = new Series(rangeMin, values);
        }

        /// <summary>Whether a track is present.</summary>
        public bool Contains(string trackId)
            => !string.IsNullOrEmpty(trackId) && _series.ContainsKey(trackId);

        /// <summary>Reads a value at an axis position.</summary>
        public bool TryGet(string trackId, int axisPosition, out double value)
        {
            value = 0d;
            if (string.IsNullOrEmpty(trackId)) return false;
            if (!_series.TryGetValue(trackId, out var series)) return false;

            var index = axisPosition - series.RangeMin;
            if (index < 0 || index >= series.Values.Length) return false;

            value = series.Values[index];
            return true;
        }

        /// <summary>
        /// Reads a value, falling back when the track or position is absent.
        /// </summary>
        /// <remarks>
        /// A simulation reaching past the end of a track is normal — a progression run
        /// walks until the player stalls, not until the data runs out — so this reports a
        /// default rather than failing.
        /// </remarks>
        public double Get(string trackId, int axisPosition, double fallback = 0d)
            => TryGet(trackId, axisPosition, out var value) ? value : fallback;

        /// <summary>Axis range a track covers.</summary>
        public bool TryGetRange(string trackId, out int rangeMin, out int rangeMax)
        {
            rangeMin = 0;
            rangeMax = 0;
            if (string.IsNullOrEmpty(trackId)) return false;
            if (!_series.TryGetValue(trackId, out var series)) return false;

            rangeMin = series.RangeMin;
            rangeMax = series.RangeMin + series.Values.Length - 1;
            return true;
        }

        /// <summary>Builds a snapshot from an evaluated profile.</summary>
        public static BalanceSnapshot From(BalanceProfile profile)
        {
            var snapshot = new BalanceSnapshot();
            if (profile == null) return snapshot;

            var evaluation = TrackEvaluator.EvaluateProfile(profile);
            foreach (var id in evaluation.TrackIds)
            {
                if (!profile.TryGetTrack(id, out var track)) continue;
                snapshot.Set(id, track.RangeMin, evaluation[id]);
            }

            return snapshot;
        }

        /// <summary>Builds a snapshot from an already-evaluated profile.</summary>
        public static BalanceSnapshot From(BalanceProfile profile, ProfileEvaluation evaluation)
        {
            var snapshot = new BalanceSnapshot();
            if (profile == null || evaluation == null) return snapshot;

            foreach (var id in evaluation.TrackIds)
            {
                if (!profile.TryGetTrack(id, out var track)) continue;
                snapshot.Set(id, track.RangeMin, evaluation[id]);
            }

            return snapshot;
        }
    }
}
