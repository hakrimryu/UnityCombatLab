using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// The numbers themselves, one row per axis position and one column per track.
    /// </summary>
    /// <remarks>
    /// A graph answers "what shape is this". A designer also needs "what is it at stage 42",
    /// and a spreadsheet is the form they already read that in. Hovering the graph once per
    /// value turns a glance into a task, and comparing two tracks that way is not practical
    /// at all.
    ///
    /// Rows are built on demand through a <see cref="ListView"/>, so a two-hundred-position
    /// profile with a dozen tracks costs only the rows on screen.
    /// </remarks>
    public sealed partial class FulcrumWindow
    {
        private const string TableModePrefKey = "Fulcrum.Window.TableMode";

        /// <summary>Narrow enough for a short id, wide enough for a five-digit number.</summary>
        private const float MinColumnWidth = 72f;

        /// <summary>Past this a long id costs more room than it is worth, and clips instead.</summary>
        private const float MaxColumnWidth = 168f;

        private ListView _table;
        private VisualElement _tableHeader;
        private VisualElement _tableRoot;
        private Toggle _tableToggle;

        /// <summary>The single source of truth; the toggle mirrors it.</summary>
        private bool _tableMode;

        private readonly List<string> _tableColumns = new List<string>();
        private readonly List<int> _tableRows = new List<int>();

        /// <summary>Per-column pixel widths, measured from the header text.</summary>
        private readonly List<float> _tableWidths = new List<float>();

        private ScrollView _tableScroll;

        private void BuildTable(VisualElement root)
        {
            _tableRoot = root.Q<VisualElement>("table-root");
            _tableHeader = root.Q<VisualElement>("table-header");
            _table = root.Q<ListView>("table-list");

            _table.fixedItemHeight = 18f;
            _table.makeItem = MakeTableRow;
            _table.bindItem = BindTableRow;
            _table.selectionType = SelectionType.None;

            // Ten tracks do not fit across the middle pane at any sensible column width, so
            // the table scrolls sideways rather than silently losing the last column.
            _tableScroll = _table.Q<ScrollView>();
            if (_tableScroll != null)
            {
                _tableScroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;

                // The header sits outside the scroll view, so it is moved to match.
                _tableScroll.horizontalScroller.valueChanged += offset =>
                {
                    if (_tableHeader != null) _tableHeader.style.left = -offset;
                };
            }

            SetTableMode(EditorPrefs.GetBool(TableModePrefKey, false));
        }

        private void BuildTableToggle(VisualElement toolbarRight)
        {
            _tableToggle = new Toggle("Table") { value = EditorPrefs.GetBool(TableModePrefKey, false) };
            _tableToggle.tooltip = "Show the numbers instead of the curve.";
            _tableToggle.RegisterValueChangedCallback(evt => SetTableMode(evt.newValue));
            toolbarRight.Add(_tableToggle);
        }

        private void SetTableMode(bool table)
        {
            _tableMode = table;
            EditorPrefs.SetBool(TableModePrefKey, table);

            // Without notifying, or the toggle's own callback would call back into here.
            _tableToggle?.SetValueWithoutNotify(table);

            if (_tableRoot != null) _tableRoot.style.display = table ? DisplayStyle.Flex : DisplayStyle.None;

            var graphHost = rootVisualElement.Q<VisualElement>("graph-host");
            if (graphHost != null) graphHost.style.display = table ? DisplayStyle.None : DisplayStyle.Flex;

            if (table) RefreshTable();
        }

        private bool TableVisible => _tableMode;

        /// <summary>
        /// Rebuilds the columns and rows from the current profile.
        /// </summary>
        /// <remarks>
        /// The selected track leads, then the rest in authoring order — the column being
        /// worked on should not have to be hunted for among a dozen others.
        /// </remarks>
        private void RefreshTable()
        {
            if (_table == null || !TableVisible) return;

            _tableColumns.Clear();
            _tableRows.Clear();

            if (_profile != null)
            {
                var selected = SelectedTrack;
                if (selected != null && !string.IsNullOrEmpty(selected.Id)) _tableColumns.Add(selected.Id);

                foreach (var track in _profile.Tracks)
                {
                    if (track == null || string.IsNullOrEmpty(track.Id)) continue;
                    if (_tableColumns.Contains(track.Id)) continue;
                    _tableColumns.Add(track.Id);
                }

                FindTableBounds(out var from, out var to);
                for (var position = from; position <= to; position++) _tableRows.Add(position);
            }

            BuildTableHeader();
            _table.itemsSource = _tableRows;
            _table.Rebuild();
        }

        private void FindTableBounds(out int from, out int to)
        {
            from = int.MaxValue;
            to = int.MinValue;

            foreach (var track in _profile.Tracks)
            {
                if (track == null || track.Length == 0) continue;
                if (track.RangeMin < from) from = track.RangeMin;
                if (track.RangeMax > to) to = track.RangeMax;
            }

            if (from > to)
            {
                from = 0;
                to = -1;
            }
        }

        private void BuildTableHeader()
        {
            _tableHeader.Clear();
            _tableHeader.Add(TableCell("axis", header: true));

            foreach (var id in _tableColumns) _tableHeader.Add(TableCell(id, header: true));

            MeasureColumnWidths();
        }

        /// <summary>
        /// Sizes each column to its own header.
        /// </summary>
        /// <remarks>
        /// One width for every column has to fit the longest id or it truncates it, and a
        /// truncated header is worse than a wide one: 'monster_count' rendered as
        /// 'nonster_count' does not read as clipped, it reads as a different track.
        ///
        /// Text can only be measured once the font has resolved, so a header built before
        /// the first layout schedules itself to be measured again when one arrives.
        /// </remarks>
        private void MeasureColumnWidths()
        {
            _tableWidths.Clear();

            var settled = true;

            foreach (var child in _tableHeader.Children())
            {
                var label = (Label)child;
                var text = label.MeasureTextSize(
                    label.text, 0f, VisualElement.MeasureMode.Undefined,
                    0f, VisualElement.MeasureMode.Undefined).x;

                if (float.IsNaN(text) || text <= 0f)
                {
                    settled = false;
                    text = 0f;
                }

                var width = UnityEngine.Mathf.Clamp(text + 16f, MinColumnWidth, MaxColumnWidth);
                _tableWidths.Add(width);
                label.style.width = width;
            }

            var total = TotalWidth();
            PinRowWidth(_tableHeader, total);

            // Rows live inside item wrappers that stretch to the viewport, so widening a row
            // is not enough on its own: the scroll content itself has to be told, or it
            // reports exactly its own width and no scrollbar ever appears.
            if (_tableScroll != null) _tableScroll.contentContainer.style.minWidth = total;

            if (settled) return;

            _tableHeader.schedule.Execute(() =>
            {
                if (!TableVisible) return;

                MeasureColumnWidths();
                _table?.Rebuild();
            }).ExecuteLater(0);
        }

        private float WidthAt(int column)
            => column >= 0 && column < _tableWidths.Count ? _tableWidths[column] : MinColumnWidth;

        private float TotalWidth()
        {
            var total = 0f;
            for (var i = 0; i < _tableWidths.Count; i++) total += _tableWidths[i];
            return total;
        }

        private static void PinRowWidth(VisualElement row, float width)
        {
            row.style.width = width;
            row.style.minWidth = width;
            row.style.flexShrink = 0f;
        }

        private VisualElement MakeTableRow()
        {
            var row = new VisualElement();
            row.AddToClassList("fulcrum-table__row");

            // One cell more than there are columns: the axis position leads each row.
            for (var i = 0; i <= _tableColumns.Count; i++) row.Add(TableCell(string.Empty));

            return row;
        }

        private void BindTableRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _tableRows.Count) return;

            var position = _tableRows[index];

            // makeItem ran before the columns settled if the profile changed since.
            while (element.childCount <= _tableColumns.Count) element.Add(TableCell(string.Empty));

            PinRowWidth(element, TotalWidth());

            var axisCell = (Label)element[0];
            axisCell.text = position.ToString();
            axisCell.style.width = WidthAt(0);

            for (var i = 0; i < _tableColumns.Count; i++)
            {
                var cell = (Label)element[i + 1];
                cell.text = FormatCell(_tableColumns[i], position);

                // Set here rather than in USS: the width belongs to the column, and only the
                // header knows how wide that column has to be.
                cell.style.width = WidthAt(i + 1);
            }
        }

        /// <summary>
        /// A cell, or blank when the track does not reach this position.
        /// </summary>
        /// <remarks>
        /// Blank rather than zero: a track that stops at position 50 has no value at 51, and
        /// a zero would claim it does.
        /// </remarks>
        private string FormatCell(string trackId, int position)
        {
            if (_profile == null || !_profile.TryGetTrack(trackId, out var track)) return string.Empty;

            var index = track.IndexOfAxisPosition(position);
            if (index < 0) return string.Empty;

            var series = _cache.GetSeries(trackId);
            if (series == null || index >= series.Length) return string.Empty;

            var value = series[index];
            if (double.IsNaN(value)) return "—";
            if (double.IsInfinity(value)) return "∞";

            return System.Math.Abs(value) >= 1000d ? value.ToString("N0") : value.ToString("0.###");
        }

        private static Label TableCell(string text, bool header = false)
        {
            var label = new Label(text);
            label.AddToClassList(header ? "fulcrum-table__header-cell" : "fulcrum-table__cell");
            return label;
        }
    }
}
