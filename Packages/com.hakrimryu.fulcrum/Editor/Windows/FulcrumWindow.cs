using System.Collections.Generic;
using Fulcrum.Editor.Graph;
using Fulcrum.Editor.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// The authoring window: pick a profile, walk its tracks, see the curve each one
    /// produces.
    /// </summary>
    /// <remarks>
    /// Layout, selection and profile binding live here; the graph and the per-track
    /// inspector are separate pieces this window hosts.
    ///
    /// The window edits through a <see cref="SerializedObject"/> rather than touching the
    /// profile directly, so undo, the dirty flag and multi-window consistency come from
    /// Unity instead of being reimplemented badly.
    /// </remarks>
    public sealed partial class FulcrumWindow : EditorWindow
    {
        private const string UxmlPath = "Packages/com.hakrimryu.fulcrum/Editor/UI/FulcrumWindow.uxml";
        private const string UssPath = "Packages/com.hakrimryu.fulcrum/Editor/UI/FulcrumWindow.uss";

        private const string ProfilePrefKey = "Fulcrum.Window.ProfileGuid";
        private const string TracksWidthPrefKey = "Fulcrum.Window.TracksPaneWidth";
        private const string InspectorWidthPrefKey = "Fulcrum.Window.InspectorPaneWidth";

        private const float DefaultTracksWidth = 200f;
        private const float DefaultInspectorWidth = 260f;

        [SerializeField] private BalanceProfile _profile;
        [SerializeField] private int _selectedTrackIndex = -1;

        private SerializedObject _serializedProfile;
        private readonly ProfileEvaluationCache _cache = new ProfileEvaluationCache();
        private ListView _trackList;
        private Label _tracksEmpty;
        private Label _banner;
        private ScrollView _inspectorContent;
        private TrackInspector _inspector;
        private ProfileInspector _profileInspector;
        private ScrollView _profileContent;
        private Button _trackTab;
        private Button _profileTab;
        [UnityEngine.SerializeField] private bool _showProfileTab;
        private CurveGraphElement _graph;
        private Label _graphTitle;
        private Label _graphFormula;
        private Label _graphReadout;
        private Label _axisMinLabel;
        private Label _axisMaxLabel;
        private Label _valueMaxLabel;
        private Label _valueMinLabel;
        private Label _dirtyIndicator;
        private Button _exportTableButton;
        private Button _exportCsvButton;
        private Button _importCsvButton;
        private TwoPaneSplitView _outerSplit;
        private TwoPaneSplitView _innerSplit;

        [MenuItem("Window/Fulcrum/Balance Editor")]
        public static void Open()
        {
            var window = GetWindow<FulcrumWindow>();
            window.titleContent = new GUIContent("Fulcrum");
            window.minSize = new Vector2(720f, 360f);
            window.Show();
        }

        /// <summary>Opens the window already pointed at <paramref name="profile"/>.</summary>
        public static void Open(BalanceProfile profile)
        {
            var window = GetWindow<FulcrumWindow>();
            window.titleContent = new GUIContent("Fulcrum");
            window.SetProfile(profile);
            window.Show();
        }

        private BalanceTrack SelectedTrack
        {
            get
            {
                if (_profile == null) return null;
                var tracks = _profile.Tracks;
                if (_selectedTrackIndex < 0 || _selectedTrackIndex >= tracks.Count) return null;
                return tracks[_selectedTrackIndex];
            }
        }

        private void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (tree == null)
            {
                rootVisualElement.Add(new HelpBox(
                    $"Could not load {UxmlPath}. The package may be misplaced.", HelpBoxMessageType.Error));
                return;
            }

            var root = tree.Instantiate();
            root.AddToClassList("fulcrum-root");
            if (style != null) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.Add(root);

            _banner = root.Q<Label>("banner");
            _tracksEmpty = root.Q<Label>("tracks-empty");
            _inspectorContent = root.Q<ScrollView>("inspector-content");
            _graphTitle = root.Q<Label>("graph-title");
            _graphFormula = root.Q<Label>("graph-formula");
            _graphReadout = root.Q<Label>("graph-readout");
            _dirtyIndicator = root.Q<Label>("dirty-indicator");
            _outerSplit = root.Q<TwoPaneSplitView>("outer-split");
            _innerSplit = root.Q<TwoPaneSplitView>("inner-split");

            // Pane widths are stored in pixels, so a layout saved on a wide monitor would
            // otherwise leave nothing for the graph when the same window opens on a laptop.
            root.RegisterCallback<GeometryChangedEvent>(_ => ClampPaneWidths());

            _profileContent = root.Q<ScrollView>("profile-content");

            _inspector = new TrackInspector(_inspectorContent)
            {
                SeriesProvider = () =>
                {
                    var track = SelectedTrack;
                    return track == null || _profile == null ? null : _cache.GetSeries(track.Id);
                },
            };
            _inspector.Changed += OnTrackEdited;

            _profileInspector = new ProfileInspector(_profileContent);
            _profileInspector.Changed += OnProfileEdited;
            _profileInspector.StructureChanged += () =>
            {
                _cache.InvalidateStructure();
                RefreshTrackList();
                RefreshSelection();
                RequestSimulation();
            };

            BuildInspectorTabs(root);

            BuildToolbar(root);
            BuildTrackList(root);
            BuildTrackFooter(root);
            BuildGraph(root);
            BuildPinBar(root);
            BuildGraphPinning();
            BuildTable(root);
            BuildStartPanel(root);
            BuildSimulation(root);
            RestorePaneWidths();

            if (_profile == null) RestoreProfileFromPrefs();
            Rebind();
        }

        private void OnDisable()
        {
            SavePaneWidths();
            _driver?.Dispose();
        }

        // --- toolbar ------------------------------------------------------------------

        private void BuildToolbar(VisualElement root)
        {
            var left = root.Q<VisualElement>("toolbar-left");

            var field = new ObjectField("Profile")
            {
                objectType = typeof(BalanceProfile),
                allowSceneObjects = false,
                value = _profile,
            };
            field.labelElement.style.minWidth = 48f;
            field.style.minWidth = 260f;
            field.RegisterValueChangedCallback(evt => SetProfile(evt.newValue as BalanceProfile));
            left.Add(field);

            var right = root.Q<VisualElement>("toolbar-right");

            _exportTableButton = new Button(() =>
            {
                var table = Export.ProfileExporter.PromptAndExportTable(_profile);
                if (table != null) EditorGUIUtility.PingObject(table);
            })
            { text = "Export Table" };

            _exportCsvButton = new Button(() => Export.ProfileExporter.PromptAndExportCsv(_profile))
            {
                text = "Export CSV",
            };

            _importCsvButton = new Button(ImportCsv)
            {
                text = "Import CSV",
                tooltip = "Read a CSV back in. Matching columns replace that track's curve "
                          + "with the numbers from the file.",
            };

            BuildViewToggles(right);
            right.Add(_exportTableButton);
            right.Add(_exportCsvButton);
            right.Add(_importCsvButton);
        }

        private void ImportCsv()
        {
            if (!Export.ProfileImporter.PromptAndImportCsv(_profile)) return;

            // Models and ranges both changed, so nothing cached about the profile survives.
            _serializedProfile?.Update();
            _cache.InvalidateStructure();

            RefreshTrackList();
            RefreshBanner();
            RefreshSelection();
            RefreshToolbar();
            RebindProfileInspector();
            RequestSimulation();
        }

        // --- track list ---------------------------------------------------------------

        private void BuildTrackList(VisualElement root)
        {
            _trackList = root.Q<ListView>("track-list");
            _trackList.selectionType = SelectionType.Single;
            _trackList.fixedItemHeight = 20f;
            _trackList.makeItem = MakeTrackRow;
            _trackList.bindItem = BindTrackRow;

            _trackList.selectionChanged += _ =>
            {
                var row = _trackList.selectedIndex;
                _selectedTrackIndex = row >= 0 && row < _visibleTracks.Count ? _visibleTracks[row] : -1;
                RefreshSelection();
            };
        }

        private static VisualElement MakeTrackRow()
        {
            var row = new VisualElement();
            row.AddToClassList("fulcrum-track-row");

            var name = new Label { name = "name" };
            name.AddToClassList("fulcrum-track-row__name");
            row.Add(name);

            var badge = new Label { name = "badge" };
            badge.AddToClassList("fulcrum-track-row__badge");
            row.Add(badge);

            return row;
        }

        private void BindTrackRow(VisualElement element, int row)
        {
            // Rows index the filtered view; the profile index comes from it.
            var index = row >= 0 && row < _visibleTracks.Count ? _visibleTracks[row] : -1;
            var tracks = _profile != null ? _profile.Tracks : null;
            var track = tracks != null && index >= 0 && index < tracks.Count ? tracks[index] : null;

            var name = element.Q<Label>("name");
            var badge = element.Q<Label>("badge");

            if (track == null)
            {
                name.text = "(empty)";
                badge.text = string.Empty;
                element.AddToClassList("fulcrum-track-row--broken");
                return;
            }

            name.text = string.IsNullOrEmpty(track.DisplayName) ? "(unnamed)" : track.DisplayName;

            // A missing model is shown rather than left blank: it is also what a broken
            // [SerializeReference] looks like, and silently drawing a flat zero line is
            // how that goes unnoticed.
            var broken = !track.HasModel;
            element.EnableInClassList("fulcrum-track-row--broken", broken);
            badge.text = broken ? "no model" : (track.Accumulate ? "cumulative" : string.Empty);
        }

        // --- graph --------------------------------------------------------------------

        private void BuildGraph(VisualElement root)
        {
            var host = root.Q<VisualElement>("graph-host");
            host.style.position = Position.Relative;

            _graph = new CurveGraphElement();
            _graph.HoverIndexChanged += OnGraphHover;

            // Zooming changes what the labels describe. Without this they keep announcing
            // the track's full range while the graph shows a slice of it.
            _graph.ViewChanged += SetAxisLabelsFromView;
            host.Add(_graph);

            // painter2D draws no text, so the axis labels are overlaid as children.
            _axisMinLabel = AxisLabel(alignLeft: true);
            _axisMaxLabel = AxisLabel(alignLeft: false);
            _valueMaxLabel = ValueLabel(top: true);
            _valueMinLabel = ValueLabel(top: false);
            host.Add(_axisMinLabel);
            host.Add(_axisMaxLabel);
            host.Add(_valueMaxLabel);
            host.Add(_valueMinLabel);
        }

        private static Label AxisLabel(bool alignLeft)
        {
            var label = new Label();
            label.AddToClassList("fulcrum-graph__axis-label");
            label.style.position = Position.Absolute;
            label.style.bottom = 2f;
            if (alignLeft) label.style.left = 46f;
            else label.style.right = 10f;
            return label;
        }

        private static Label ValueLabel(bool top)
        {
            var label = new Label();
            label.AddToClassList("fulcrum-graph__axis-label");
            label.style.position = Position.Absolute;
            label.style.left = 4f;

            // The floor sits above the axis labels rather than beside them, or the two
            // numbers in the corner read as one.
            if (top) label.style.top = 2f;
            else label.style.bottom = 22f;

            return label;
        }

        private void OnGraphHover(int index)
        {
            if (_graphReadout == null) return;

            if (index < 0 || !_graph.TryGetValue(index, out var value))
            {
                _graphReadout.text = string.Empty;
                return;
            }

            _graphReadout.text = double.IsNaN(value) || double.IsInfinity(value)
                ? $"{_graph.AxisPositionAt(index)}  =  {value}"
                : $"{_graph.AxisPositionAt(index)}  =  {value:N2}";
        }

        // --- binding ------------------------------------------------------------------

        /// <summary>Points the window at a profile and refreshes everything downstream.</summary>
        public void SetProfile(BalanceProfile profile)
        {
            _driver?.Cancel();
            _analysis = null;
            _missingBindings.Clear();
            _profile = profile;
            _selectedTrackIndex = profile != null && profile.Tracks.Count > 0 ? 0 : -1;
            SaveProfileToPrefs();
            Rebind();
        }

        private void Rebind()
        {
            _serializedProfile = _profile != null ? new SerializedObject(_profile) : null;
            _cache.Profile = _profile;
            _cache.InvalidateStructure();

            _profileInspector?.Bind(_serializedProfile, _profile);

            RefreshTrackList();
            RefreshBanner();
            RefreshSelection();
            RefreshToolbar();
            RequestSimulation();
        }

        private void RefreshToolbar()
        {
            var enabled = _profile != null && _profile.Tracks.Count > 0;
            _exportTableButton?.SetEnabled(enabled);
            _exportCsvButton?.SetEnabled(enabled);
            _importCsvButton?.SetEnabled(enabled);

            RefreshStartPanel();
            RefreshDirtyIndicator();
        }

        /// <summary>
        /// Whether the profile has edits not yet written to disk.
        /// </summary>
        /// <remarks>
        /// Unity keeps unsaved asset changes in memory with no visible sign. Someone who
        /// spent an afternoon tuning has no way to tell whether closing the editor would
        /// lose it, and the honest answer is that it might.
        /// </remarks>
        private void RefreshDirtyIndicator()
        {
            if (_dirtyIndicator == null) return;

            var dirty = _profile != null && EditorUtility.IsDirty(_profile);
            _dirtyIndicator.text = dirty ? "unsaved — Ctrl+S" : string.Empty;
        }

        private void RefreshBanner()
        {
            if (_banner == null) return;

            var message = DescribeProfileProblem();
            _banner.text = message ?? string.Empty;
            _banner.EnableInClassList("fulcrum-banner--visible", message != null);
        }

        /// <summary>
        /// Problems worth interrupting for. Duplicate ids matter because once tracks refer
        /// to each other by id, a repeat makes the target depend on list order.
        /// </summary>
        private string DescribeProfileProblem()
        {
            if (_profile == null) return null;

            var duplicates = _profile.FindDuplicateTrackIds();
            if (duplicates.Count > 0)
            {
                return "Duplicate track ids: " + string.Join(", ", duplicates);
            }

            // A cycle leaves its tracks with no value at all, so it outranks everything else.
            var graph = _cache.Graph;
            if (!graph.IsValid)
            {
                return "Circular reference: " + graph.DescribeCycles();
            }

            if (graph.MissingReferences.Count > 0)
            {
                var names = new List<string>();
                foreach (var reference in graph.MissingReferences) names.Add(reference.ToString());
                return "References a track that does not exist: " + string.Join(", ", names);
            }

            var track = SelectedTrack;
            if (track != null && !track.HasModel)
            {
                return $"'{track.Id}' has no model. If it used to have one, a renamed class "
                     + "may have broken the reference.";
            }

            // Otherwise a gap in the line looks like a drawing bug rather than a formula
            // that has no value over part of its range.
            if (_graph != null && _graph.HasNonFiniteValues)
            {
                return "Some values are not finite. A clamp in the post-processing chain "
                     + "is usually what this needs.";
            }

            return null;
        }

        private void RefreshSelection()
        {
            var track = SelectedTrack;

            if (_graphTitle != null)
            {
                _graphTitle.text = track == null ? "Graph" : track.DisplayName;
            }

            if (_graphFormula != null)
            {
                _graphFormula.text = DescribeTrack(track);
            }

            RefreshGraph(track);
            RefreshTable();
            RefreshInspector(track);
            RefreshBanner();
        }

        private static string DescribeTrack(BalanceTrack track)
        {
            if (track == null) return string.Empty;
            if (!track.HasModel) return "No model assigned.";

            var text = track.Model.Describe();
            var chain = PostProcessorChain.Describe(track.PostProcessors);
            if (!string.IsNullOrEmpty(chain)) text += "   →   " + chain;
            return text;
        }

        private void RefreshGraph(BalanceTrack track)
        {
            if (_graph == null) return;

            if (track == null)
            {
                _graph.SetValues(System.Array.Empty<double>());
                _graph.SetPins(null);
                ClosePinBar();
                SetAxisLabels(null);
                return;
            }

            // Evaluated through the whole profile rather than in isolation: a derived track
            // reads other tracks, and on its own it would draw a flat zero line that looks
            // like a broken formula.
            if (_overlayToggle != null && _overlayToggle.value)
            {
                var comparison = BuildComparisonSeries();
                if (comparison.Count > 0)
                {
                    _graph.SetSeries(comparison);

                    // The compared curves are win rates, not this track, so its pins would
                    // land on points that mean something else.
                    _graph.SetPins(null);

                    // The header and the formula line describe the selected track, which is
                    // not what is on screen here. Left alone they label win-rate curves
                    // 'Monster HP' and annotate them with that track's formula.
                    if (_graphTitle != null) _graphTitle.text = "Win rate by player";
                    if (_graphFormula != null)
                    {
                        _graphFormula.text =
                            "Simulated win rate at each position, one line per player preset. "
                            + "Where a line falls away is where that player stops progressing.";
                    }
                    _graph.SetHighlights(BuildHighlights());
                    RefreshLegend(comparison);
                    SetAxisLabels(track);
                    if (_graphReadout != null) _graphReadout.text = string.Empty;
                    return;
                }
            }

            var values = _profile != null ? _cache.GetSeries(track.Id) : null;
            _graph.SetValues(values ?? TrackEvaluator.Evaluate(track, null), track.RangeMin);
            _graph.SetHighlights(BuildHighlights());
            _graph.SetPins(SelectedPins());
            RefreshLegend(_graph.Series);
            SetAxisLabels(track);

            if (_graphReadout != null) _graphReadout.text = string.Empty;
        }

        private void SetAxisLabels(BalanceTrack track) => SetAxisLabelsFromView();

        /// <summary>
        /// Labels describe what is on screen, which is the visible range rather than the
        /// track's full extent once the graph has been zoomed.
        /// </summary>
        private void SetAxisLabelsFromView()
        {
            if (_axisMinLabel == null || _graph == null) return;

            if (_graph.Count == 0)
            {
                _axisMinLabel.text = string.Empty;
                _axisMaxLabel.text = string.Empty;
                _valueMaxLabel.text = string.Empty;
                _valueMinLabel.text = string.Empty;
                return;
            }

            _axisMinLabel.text = _graph.ViewFrom.ToString();
            _axisMaxLabel.text = _graph.ViewTo.ToString();
            _valueMaxLabel.text = FormatValueLabel(_graph.MaxValue);
            _valueMinLabel.text = FormatValueLabel(_graph.MinValue);
        }

        /// <summary>
        /// Formats a value-axis label.
        /// </summary>
        /// <remarks>
        /// Whole-number formatting suits monster health and ruins anything below one: a win
        /// rate topping out at 0.97 was labelled '1', and a drop rate of 0.006 became '0'.
        /// </remarks>
        private static string FormatValueLabel(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;

            var magnitude = System.Math.Abs(value);
            if (magnitude >= 1000d) return value.ToString("N0");
            if (magnitude >= 1d) return value.ToString("0.##");

            return value.ToString("0.####");
        }

        private void BuildInspectorTabs(VisualElement root)
        {
            var tabs = root.Q<VisualElement>("inspector-tabs");

            _trackTab = new Button(() => SetProfileTab(false)) { text = "Track" };
            _profileTab = new Button(() => SetProfileTab(true)) { text = "Profile" };

            tabs.Add(_trackTab);
            tabs.Add(_profileTab);
            SetProfileTab(_showProfileTab);
        }

        /// <summary>
        /// Switches the right pane between the selected track and the profile's own
        /// settings. Both were previously reachable only from the asset inspector.
        /// </summary>
        private void SetProfileTab(bool profile)
        {
            _showProfileTab = profile;

            if (_inspectorContent != null)
            {
                _inspectorContent.style.display = profile ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_profileContent != null)
            {
                _profileContent.style.display = profile ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _trackTab?.EnableInClassList("fulcrum-tab--active", !profile);
            _profileTab?.EnableInClassList("fulcrum-tab--active", profile);
        }

        /// <summary>
        /// Rebuilds the track pane only.
        /// </summary>
        /// <remarks>
        /// The profile pane is not track-dependent, and rebuilding it here would be a loop:
        /// a rebuilt PropertyField reports a change as it binds, which applies an edit,
        /// which refreshes the selection, which rebuilds it again.
        /// </remarks>
        private void RefreshInspector(BalanceTrack track)
        {
            _inspector?.Bind(_serializedProfile, track == null ? -1 : _selectedTrackIndex);
        }

        /// <summary>Rebuilds the profile pane. Only for edits that changed its choices.</summary>
        private void RebindProfileInspector()
            => _profileInspector?.Bind(_serializedProfile, _profile);

        /// <summary>
        /// A profile-level edit can reach every track — a shared constant, a renamed axis —
        /// so everything is re-evaluated rather than guessing at the blast radius.
        /// </summary>
        private void OnProfileEdited()
        {
            _cache.InvalidateValues();

            // Deliberately not RefreshSelection: that rebuilds the inspectors, and the pane
            // being edited must not be rebuilt underneath the cursor.
            var track = SelectedTrack;
            if (_graphFormula != null) _graphFormula.text = DescribeTrack(track);
            RefreshGraph(track);
            RefreshBanner();
            _trackList?.RefreshItems();
            RefreshDirtyIndicator();
            RequestSimulation();
        }

        /// <summary>
        /// Re-evaluates and redraws after an edit, without rebuilding the inspector that
        /// is currently being interacted with.
        /// </summary>
        private void OnTrackEdited()
        {
            var track = SelectedTrack;

            // The inspector cannot tell a parameter edit from one that changed which tracks
            // a model reads, so the structure is rebuilt. One rebuild costs less than a
            // stale evaluation order.
            _cache.InvalidateStructure();

            if (_graphTitle != null) _graphTitle.text = track == null ? "Graph" : track.DisplayName;
            if (_graphFormula != null) _graphFormula.text = DescribeTrack(track);

            RefreshGraph(track);
            RefreshBanner();
            _trackList?.RefreshItems();
            RefreshDirtyIndicator();
            RequestSimulation();
        }

        // --- persistence --------------------------------------------------------------

        private void RestoreProfileFromPrefs()
        {
            var guid = EditorPrefs.GetString(ProfilePrefKey, string.Empty);
            if (string.IsNullOrEmpty(guid)) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            _profile = AssetDatabase.LoadAssetAtPath<BalanceProfile>(path);
            if (_profile != null && _profile.Tracks.Count > 0 && _selectedTrackIndex < 0)
            {
                _selectedTrackIndex = 0;
            }
        }

        private void SaveProfileToPrefs()
        {
            if (_profile == null)
            {
                EditorPrefs.DeleteKey(ProfilePrefKey);
                return;
            }

            var path = AssetDatabase.GetAssetPath(_profile);
            if (string.IsNullOrEmpty(path)) return;

            EditorPrefs.SetString(ProfilePrefKey, AssetDatabase.AssetPathToGUID(path));
        }

        private void RestorePaneWidths()
        {
            if (_outerSplit != null)
            {
                _outerSplit.fixedPaneInitialDimension =
                    EditorPrefs.GetFloat(TracksWidthPrefKey, DefaultTracksWidth);
            }

            if (_innerSplit != null)
            {
                _innerSplit.fixedPaneInitialDimension =
                    EditorPrefs.GetFloat(InspectorWidthPrefKey, DefaultInspectorWidth);
            }
        }

        /// <summary>
        /// Keeps the side panes from crowding out the middle one.
        /// </summary>
        /// <remarks>
        /// Restoring 377 and 454 pixels into a 760 pixel window leaves the graph about a
        /// hundred wide — technically present, and useless. The side panes give way instead,
        /// because the curve is what the window is for.
        /// </remarks>
        private void ClampPaneWidths()
        {
            var available = rootVisualElement.resolvedStyle.width;
            if (float.IsNaN(available) || available <= 1f) return;

            const float minGraph = 220f;
            const float minSide = 120f;

            var tracks = _outerSplit?.fixedPane?.resolvedStyle.width ?? DefaultTracksWidth;
            var inspector = _innerSplit?.fixedPane?.resolvedStyle.width ?? DefaultInspectorWidth;
            if (float.IsNaN(tracks) || tracks <= 0f) tracks = DefaultTracksWidth;
            if (float.IsNaN(inspector) || inspector <= 0f) inspector = DefaultInspectorWidth;

            var spare = available - minGraph;
            if (tracks + inspector <= spare) return;

            // Shrink both in proportion, so a deliberately wide track list stays the wider
            // of the two after the squeeze.
            var scale = Mathf.Max(0f, spare) / (tracks + inspector);
            var newTracks = Mathf.Max(minSide, tracks * scale);
            var newInspector = Mathf.Max(minSide, inspector * scale);

            if (_outerSplit != null) _outerSplit.fixedPaneInitialDimension = newTracks;
            if (_innerSplit != null) _innerSplit.fixedPaneInitialDimension = newInspector;
        }

        private void SavePaneWidths()
        {
            SavePaneWidth(_outerSplit, TracksWidthPrefKey);
            SavePaneWidth(_innerSplit, InspectorWidthPrefKey);
        }

        private static void SavePaneWidth(TwoPaneSplitView split, string key)
        {
            var pane = split?.fixedPane;
            if (pane == null) return;

            var width = pane.resolvedStyle.width;
            if (width > 1f && !float.IsNaN(width)) EditorPrefs.SetFloat(key, width);
        }
    }
}
