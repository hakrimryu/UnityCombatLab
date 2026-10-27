using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// Creating, removing and reordering tracks from inside the window.
    /// </summary>
    /// <remarks>
    /// Without this the window can only show a profile someone else built, and building one
    /// means editing a raw list in the default inspector — which is an engineer's workflow,
    /// not a designer's.
    ///
    /// Every operation goes through <see cref="SerializedObject"/> so undo works and the
    /// asset is marked dirty, rather than mutating the profile and hoping Unity notices.
    /// </remarks>
    public sealed partial class FulcrumWindow
    {
        private TextField _trackSearch;
        private string _filter = string.Empty;

        /// <summary>Indices into the profile's track list, filtered and in list order.</summary>
        private readonly List<int> _visibleTracks = new List<int>();

        private void BuildTrackFooter(VisualElement root)
        {
            var footer = root.Q<VisualElement>("tracks-footer");

            footer.Add(IconButton("+", "Add a track", AddTrack));
            footer.Add(IconButton("⧉", "Duplicate the selected track", DuplicateTrack));
            footer.Add(IconButton("▲", "Move up", () => MoveTrack(-1)));
            footer.Add(IconButton("▼", "Move down", () => MoveTrack(1)));
            footer.Add(IconButton("−", "Remove the selected track", RemoveTrack));

            _trackSearch = root.Q<TextField>("track-search");
            _trackSearch.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? string.Empty;
                RefreshTrackList();
            });
        }

        private static Button IconButton(string text, string tooltip, System.Action action)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            return button;
        }

        /// <summary>Profile index of the selected row, or -1.</summary>
        private int SelectedTrackIndex
        {
            get => _selectedTrackIndex;
            set => _selectedTrackIndex = value;
        }

        private SerializedProperty TracksProperty()
            => _serializedProfile?.FindProperty("_tracks");

        private void AddTrack()
        {
            var tracks = TracksProperty();
            if (tracks == null) return;

            var index = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(index);
            InitialiseTrack(tracks.GetArrayElementAtIndex(index), UniqueTrackId("track"));
            CommitStructuralEdit(index);
        }

        /// <summary>Adds a track with a stated shape, used by the starter template.</summary>
        private void AddTrack(string id, string displayName, int rangeMin, int rangeMax,
            GrowthModel model, IReadOnlyList<PostProcessor> chain = null)
        {
            if (_profile == null) return;

            var track = new BalanceTrack(UniqueTrackId(id), DefaultAxisId(), rangeMin, rangeMax, model)
            {
                DisplayName = displayName,
            };

            if (chain != null)
            {
                foreach (var step in chain) track.PostProcessors.Add(step);
            }

            Undo.RecordObject(_profile, "Add track");
            _profile.Tracks.Add(track);
            EditorUtility.SetDirty(_profile);

            _serializedProfile?.Update();
            CommitStructuralEdit(_profile.Tracks.Count - 1);
        }

        private void DuplicateTrack()
        {
            var tracks = TracksProperty();
            if (tracks == null || SelectedTrackIndex < 0 || SelectedTrackIndex >= tracks.arraySize) return;

            var index = SelectedTrackIndex;

            // Unity copies the preceding element on insert, which is exactly what a
            // duplicate wants; only the id has to be made unique again.
            tracks.InsertArrayElementAtIndex(index);

            var copy = tracks.GetArrayElementAtIndex(index + 1);
            var idProperty = copy.FindPropertyRelative("_id");
            if (idProperty != null) idProperty.stringValue = UniqueTrackId(idProperty.stringValue);

            CommitStructuralEdit(index + 1);
        }

        private void RemoveTrack()
        {
            var tracks = TracksProperty();
            if (tracks == null || SelectedTrackIndex < 0 || SelectedTrackIndex >= tracks.arraySize) return;

            var index = SelectedTrackIndex;
            var label = _profile.Tracks[index]?.DisplayName ?? "this track";

            // Deleting authored work is worth one confirmation; nothing else in the window
            // destroys anything.
            if (!EditorUtility.DisplayDialog(
                "Remove track", $"Remove '{label}'? This cannot be undone from the asset file.",
                "Remove", "Cancel"))
            {
                return;
            }

            tracks.DeleteArrayElementAtIndex(index);
            CommitStructuralEdit(UnityEngine.Mathf.Min(index, tracks.arraySize - 1));
        }

        private void MoveTrack(int delta)
        {
            var tracks = TracksProperty();
            if (tracks == null || SelectedTrackIndex < 0) return;

            var target = SelectedTrackIndex + delta;
            if (target < 0 || target >= tracks.arraySize) return;

            tracks.MoveArrayElement(SelectedTrackIndex, target);
            CommitStructuralEdit(target);
        }

        /// <summary>
        /// Applies an edit that changed the shape of the profile and reselects a row.
        /// </summary>
        private void CommitStructuralEdit(int selectIndex)
        {
            _serializedProfile.ApplyModifiedProperties();
            _cache.InvalidateStructure();

            SelectedTrackIndex = selectIndex;
            RefreshTrackList();
            RefreshBanner();
            RefreshSelection();
            RefreshToolbar();

            // Track ids feed the binding pickers, so those choices are now stale.
            RebindProfileInspector();
            RequestSimulation();
        }

        private void InitialiseTrack(SerializedProperty track, string id)
        {
            Set(track, "_id", id);
            Set(track, "_displayName", string.Empty);
            Set(track, "_axisId", DefaultAxisId());

            var min = track.FindPropertyRelative("_rangeMin");
            var max = track.FindPropertyRelative("_rangeMax");
            if (_profile != null && _profile.TryGetAxis(DefaultAxisId(), out var axis))
            {
                if (min != null) min.intValue = axis.Min;
                if (max != null) max.intValue = axis.Max;
            }
            else
            {
                if (min != null) min.intValue = 1;
                if (max != null) max.intValue = 100;
            }

            var accumulate = track.FindPropertyRelative("_accumulate");
            if (accumulate != null) accumulate.boolValue = false;

            // A new track starts with a curve rather than nothing: an empty model reads as
            // broken, and the first thing to do is see a line.
            var model = track.FindPropertyRelative("_model");
            if (model != null) model.managedReferenceValue = new LinearModel(1d);

            var chain = track.FindPropertyRelative("_postProcessors");
            chain?.ClearArray();

            var overrides = track.FindPropertyRelative("_overrides");
            overrides?.ClearArray();
        }

        private static void Set(SerializedProperty parent, string name, string value)
        {
            var property = parent.FindPropertyRelative(name);
            if (property != null) property.stringValue = value;
        }

        /// <summary>The axis a new track should default to.</summary>
        private string DefaultAxisId()
        {
            if (_profile == null) return "stage";

            foreach (var axis in _profile.Axes)
            {
                if (axis != null && !string.IsNullOrEmpty(axis.Id)) return axis.Id;
            }

            return "stage";
        }

        /// <summary>
        /// A track id not already in use. Duplicated ids make the target of a reference
        /// depend on list order, so they are avoided at the point of creation.
        /// </summary>
        private string UniqueTrackId(string preferred)
        {
            if (string.IsNullOrEmpty(preferred)) preferred = "track";
            if (_profile == null) return preferred;

            var taken = new HashSet<string>();
            foreach (var track in _profile.Tracks)
            {
                if (track != null && !string.IsNullOrEmpty(track.Id)) taken.Add(track.Id);
            }

            if (!taken.Contains(preferred)) return preferred;

            for (var i = 2; i < 1000; i++)
            {
                var candidate = $"{preferred}_{i}";
                if (!taken.Contains(candidate)) return candidate;
            }

            return preferred;
        }

        // --- filtering ------------------------------------------------------------------

        /// <summary>Rebuilds the visible rows, honouring the search box.</summary>
        private void RefreshTrackList()
        {
            _visibleTracks.Clear();

            if (_profile != null)
            {
                for (var i = 0; i < _profile.Tracks.Count; i++)
                {
                    if (MatchesFilter(_profile.Tracks[i])) _visibleTracks.Add(i);
                }
            }

            if (_trackList == null) return;

            _trackList.itemsSource = _visibleTracks;
            _trackList.Rebuild();

            var row = _visibleTracks.IndexOf(SelectedTrackIndex);
            _trackList.SetSelectionWithoutNotify(row >= 0 ? new[] { row } : new int[0]);

            if (_tracksEmpty != null)
            {
                var hasRows = _visibleTracks.Count > 0;
                _tracksEmpty.style.display = hasRows ? DisplayStyle.None : DisplayStyle.Flex;
                _tracksEmpty.text = _profile == null
                    ? "No profile selected."
                    : _profile.Tracks.Count == 0
                        ? "No tracks yet. Use + below."
                        : "No track matches the search.";
            }
        }

        private bool MatchesFilter(BalanceTrack track)
        {
            if (string.IsNullOrEmpty(_filter)) return true;
            if (track == null) return false;

            return Contains(track.Id, _filter) || Contains(track.DisplayName, _filter);
        }

        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
               && haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
