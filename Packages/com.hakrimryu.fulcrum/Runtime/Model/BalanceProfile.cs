using System.Collections.Generic;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// One complete set of balance settings: its axes, its tracks, and the constants they
    /// share.
    /// </summary>
    /// <remarks>
    /// <see cref="Notes"/> is not decoration. A spreadsheet records what a number is; it
    /// almost never records why, and the reasoning evaporates the moment the person who
    /// chose it moves on. Storing the intent next to the data is most of what separates a
    /// working tool from a toy.
    /// </remarks>
    [CreateAssetMenu(menuName = "Fulcrum/Balance Profile", fileName = "BalanceProfile")]
    public sealed class BalanceProfile : ScriptableObject
    {
        [SerializeField] private string _profileName;

        [TextArea(3, 10)]
        [SerializeField] private string _notes;

        [SerializeField] private List<AxisDefinition> _axes = new List<AxisDefinition>();
        [SerializeField] private List<BalanceTrack> _tracks = new List<BalanceTrack>();
        [SerializeField] private List<GlobalConstant> _globals = new List<GlobalConstant>();
        [SerializeField] private SimulationSettings _simulationSettings = new SimulationSettings();
        [SerializeField] private TrackBinding _simulationBinding = new TrackBinding();

        /// <summary>Human-readable name, such as "v1.2 hard mode pass".</summary>
        public string ProfileName
        {
            get => string.IsNullOrEmpty(_profileName) ? name : _profileName;
            set => _profileName = value;
        }

        /// <summary>Why this profile is tuned the way it is.</summary>
        public string Notes
        {
            get => _notes;
            set => _notes = value;
        }

        public List<AxisDefinition> Axes
        {
            get => _axes ?? (_axes = new List<AxisDefinition>());
            set => _axes = value ?? new List<AxisDefinition>();
        }

        public List<BalanceTrack> Tracks
        {
            get => _tracks ?? (_tracks = new List<BalanceTrack>());
            set => _tracks = value ?? new List<BalanceTrack>();
        }

        /// <summary>Constants shared across tracks, referenced by key.</summary>
        public List<GlobalConstant> Globals
        {
            get => _globals ?? (_globals = new List<GlobalConstant>());
            set => _globals = value ?? new List<GlobalConstant>();
        }

        public SimulationSettings SimulationSettings
        {
            get => _simulationSettings ?? (_simulationSettings = new SimulationSettings());
            set => _simulationSettings = value ?? new SimulationSettings();
        }

        /// <summary>
        /// Which track fills each role the simulators need.
        /// </summary>
        /// <remarks>
        /// Kept on the profile rather than assumed by the simulators, so a project can name
        /// its tracks whatever it already calls them.
        /// </remarks>
        public TrackBinding SimulationBinding
        {
            get => _simulationBinding ?? (_simulationBinding = new TrackBinding());
            set => _simulationBinding = value ?? new TrackBinding();
        }

        /// <summary>Finds a track by id. Ids are expected to be unique; see <see cref="FindDuplicateTrackIds"/>.</summary>
        public bool TryGetTrack(string id, out BalanceTrack track)
        {
            track = null;
            if (string.IsNullOrEmpty(id)) return false;

            var tracks = _tracks;
            if (tracks == null) return false;

            for (var i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null && tracks[i].Id == id)
                {
                    track = tracks[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Finds an axis by id.</summary>
        public bool TryGetAxis(string id, out AxisDefinition axis)
        {
            axis = null;
            if (string.IsNullOrEmpty(id)) return false;

            var axes = _axes;
            if (axes == null) return false;

            for (var i = 0; i < axes.Count; i++)
            {
                if (axes[i] != null && axes[i].Id == id)
                {
                    axis = axes[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Collapses <see cref="Globals"/> into the lookup an <see cref="EvalContext"/>
        /// needs. A repeated key keeps the last entry, matching how the list reads.
        /// </summary>
        public IReadOnlyDictionary<string, double> BuildGlobals()
        {
            var result = new Dictionary<string, double>();
            var globals = _globals;
            if (globals == null) return result;

            for (var i = 0; i < globals.Count; i++)
            {
                var global = globals[i];
                if (global == null || string.IsNullOrEmpty(global.Key)) continue;
                result[global.Key] = global.Value;
            }

            return result;
        }

        /// <summary>Builds an evaluation context carrying this profile's constants.</summary>
        public EvalContext CreateContext(int index = 0) => new EvalContext(index, BuildGlobals());

        /// <summary>
        /// Track ids that appear more than once.
        /// </summary>
        /// <remarks>
        /// Duplicates are worth surfacing rather than tolerating: once tracks reference each
        /// other by id, a repeated id makes the target of a reference a matter of list order.
        /// </remarks>
        public IReadOnlyList<string> FindDuplicateTrackIds()
        {
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            var tracks = _tracks;
            if (tracks == null) return duplicates;

            for (var i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                if (!seen.Add(track.Id) && !duplicates.Contains(track.Id)) duplicates.Add(track.Id);
            }

            return duplicates;
        }
    }
}
