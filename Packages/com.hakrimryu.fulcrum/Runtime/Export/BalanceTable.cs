using System;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// The baked result of a profile: the numbers a game reads at runtime.
    /// </summary>
    /// <remarks>
    /// This is the only Fulcrum type a shipped game needs. It carries no formulas, no
    /// post-processing and no editor dependency — just the evaluated values, so gameplay
    /// code never pays for the authoring machinery.
    ///
    /// Values live in one flat array rather than a dictionary of arrays. A dictionary
    /// cannot be serialised by Unity without a custom pass, and the flat form also keeps
    /// every track contiguous, which matters when the table is read every frame.
    ///
    /// <see cref="SourceProfileHash"/> records which profile produced the table. Without
    /// it, a table that drifted out of sync with its source is indistinguishable from one
    /// that did not, and that question always comes up eventually.
    /// </remarks>
    [CreateAssetMenu(menuName = "Fulcrum/Balance Table", fileName = "BalanceTable")]
    public sealed class BalanceTable : ScriptableObject
    {
        [SerializeField] private string _sourceProfileName;
        [SerializeField] private string _sourceProfileHash;
        [SerializeField] private string _exportedAtUtc;

        [SerializeField] private string[] _trackIds = Array.Empty<string>();
        [SerializeField] private int[] _rangeMins = Array.Empty<int>();
        [SerializeField] private int[] _lengths = Array.Empty<int>();
        [SerializeField] private int[] _offsets = Array.Empty<int>();
        [SerializeField] private double[] _values = Array.Empty<double>();

        /// <summary>Name of the profile this table was baked from.</summary>
        public string SourceProfileName => _sourceProfileName;

        /// <summary>Fingerprint of the profile at export time.</summary>
        public string SourceProfileHash => _sourceProfileHash;

        /// <summary>When the table was exported, in round-trip UTC form.</summary>
        public string ExportedAtUtc => _exportedAtUtc;

        /// <summary>Number of tracks stored.</summary>
        public int TrackCount => _trackIds?.Length ?? 0;

        /// <summary>Ids of the stored tracks, in storage order.</summary>
        public string[] TrackIds => _trackIds;

        /// <summary>Replaces the contents. Called by the exporter.</summary>
        internal void SetContents(
            string profileName, string profileHash, string exportedAtUtc,
            string[] trackIds, int[] rangeMins, int[] lengths, int[] offsets, double[] values)
        {
            _sourceProfileName = profileName;
            _sourceProfileHash = profileHash;
            _exportedAtUtc = exportedAtUtc;
            _trackIds = trackIds ?? Array.Empty<string>();
            _rangeMins = rangeMins ?? Array.Empty<int>();
            _lengths = lengths ?? Array.Empty<int>();
            _offsets = offsets ?? Array.Empty<int>();
            _values = values ?? Array.Empty<double>();
        }

        /// <summary>
        /// Reads a value at an axis position.
        /// </summary>
        /// <returns>
        /// False when the track is unknown or the position falls outside its exported
        /// range. Gameplay code asking for a stage beyond the table is a normal condition,
        /// not an exception.
        /// </returns>
        public bool TryGet(string trackId, int axisPosition, out double value)
        {
            value = 0d;

            var track = IndexOfTrack(trackId);
            if (track < 0) return false;

            var offset = axisPosition - _rangeMins[track];
            if (offset < 0 || offset >= _lengths[track]) return false;

            value = _values[_offsets[track] + offset];
            return true;
        }

        /// <summary>
        /// Reads a value, falling back to <paramref name="fallback"/> when it is missing.
        /// </summary>
        public double Get(string trackId, int axisPosition, double fallback = 0d)
            => TryGet(trackId, axisPosition, out var value) ? value : fallback;

        /// <summary>Axis range a track covers. Returns false when the track is unknown.</summary>
        public bool TryGetRange(string trackId, out int rangeMin, out int rangeMax)
        {
            rangeMin = 0;
            rangeMax = 0;

            var track = IndexOfTrack(trackId);
            if (track < 0) return false;

            rangeMin = _rangeMins[track];
            rangeMax = _rangeMins[track] + _lengths[track] - 1;
            return true;
        }

        /// <summary>Whether a track is present.</summary>
        public bool Contains(string trackId) => IndexOfTrack(trackId) >= 0;

        private int IndexOfTrack(string trackId)
        {
            if (string.IsNullOrEmpty(trackId) || _trackIds == null) return -1;

            for (var i = 0; i < _trackIds.Length; i++)
            {
                if (_trackIds[i] == trackId) return i;
            }

            return -1;
        }
    }
}
