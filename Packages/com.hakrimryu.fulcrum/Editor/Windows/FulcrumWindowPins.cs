using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// Setting and clearing a track's manual overrides from the graph itself.
    /// </summary>
    /// <remarks>
    /// Pinning a single value is one of the most common things anyone does to a curve:
    /// stage 1 has to be exactly 100 because the tutorial says so, or a boss stage needs a
    /// deliberate spike. Until now that meant leaving the graph, finding the overrides list
    /// in the inspector, adding an element and typing the axis position from memory — four
    /// steps away from the place the decision was made.
    ///
    /// The graph already knows which position is under the cursor, so it is the natural
    /// place to do this. Double clicking picks a position; the bar that appears edits it.
    ///
    /// The bar is inline rather than a popup on purpose. A popup would close on every
    /// interaction elsewhere, and adjusting a pin is something people do while watching the
    /// Issues pane react to it.
    /// </remarks>
    public sealed partial class FulcrumWindow
    {
        private VisualElement _pinBar;
        private Label _pinLabel;
        private DoubleField _pinValue;
        private Button _pinClearButton;

        /// <summary>Axis position the bar is editing, or int.MinValue when it is closed.</summary>
        private int _pinPosition = int.MinValue;

        private void BuildPinBar(VisualElement root)
        {
            _pinBar = new VisualElement();
            _pinBar.AddToClassList("fulcrum-pin");
            _pinBar.style.display = DisplayStyle.None;

            _pinLabel = new Label();
            _pinLabel.AddToClassList("fulcrum-pin__label");

            _pinValue = new DoubleField { isDelayed = true };
            _pinValue.AddToClassList("fulcrum-pin__field");
            _pinValue.RegisterValueChangedCallback(evt => SetPin(_pinPosition, evt.newValue));

            _pinClearButton = new Button(() => ClearPin(_pinPosition)) { text = "Clear" };
            _pinClearButton.tooltip = "Let the curve decide this position again.";

            var close = new Button(ClosePinBar) { text = "×", tooltip = "Close" };

            _pinBar.Add(_pinLabel);
            _pinBar.Add(_pinValue);
            _pinBar.Add(_pinClearButton);
            _pinBar.Add(close);

            // Directly above the plot, so the number and the point it moves are together.
            var host = root.Q<VisualElement>("graph-host");
            host?.parent?.Insert(host.parent.IndexOf(host), _pinBar);
        }

        private void BuildGraphPinning()
        {
            if (_graph == null) return;

            _graph.PositionPicked += OpenPinBar;
            _graph.AddManipulator(new ContextualMenuManipulator(PopulateGraphMenu));
        }

        private void PopulateGraphMenu(ContextualMenuPopulateEvent evt)
        {
            var track = SelectedTrack;
            if (track == null) return;

            var index = _graph.IndexAtLocalX(evt.localMousePosition.x);
            if (index < 0) return;

            var position = _graph.AxisPositionAt(index);
            var pinned = track.TryGetOverride(position, out var existing);

            evt.menu.AppendAction($"Pin {AxisLabel()} {position}…", _ => OpenPinBar(position));

            evt.menu.AppendAction(
                pinned ? $"Clear the pin at {position} ({Format(existing.Value)})"
                       : "Clear the pin here",
                _ => ClearPin(position),
                pinned ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(
                $"Clear all {track.Overrides.Count} pin(s) on this track",
                _ => ClearAllPins(),
                track.Overrides.Count > 0 ? DropdownMenuAction.Status.Normal
                                          : DropdownMenuAction.Status.Disabled);
        }

        /// <summary>Short enough for a menu line; exactness lives in the field.</summary>
        private static string Format(double value)
            => System.Math.Abs(value) >= 1000d ? value.ToString("N0") : value.ToString("0.###");

        private string AxisLabel()
        {
            var track = SelectedTrack;
            if (track != null && _profile != null && _profile.TryGetAxis(track.AxisId, out var axis)
                && !string.IsNullOrEmpty(axis.DisplayName))
            {
                return axis.DisplayName.ToLowerInvariant();
            }

            return "position";
        }

        /// <summary>Opens the bar on an axis position, seeded with what is there now.</summary>
        private void OpenPinBar(int axisPosition)
        {
            var track = SelectedTrack;
            if (track == null || _pinBar == null) return;

            if (axisPosition < track.RangeMin || axisPosition > track.RangeMax) return;

            _pinPosition = axisPosition;

            // Seeding with the current value means confirming without typing leaves the
            // curve exactly as it looks — the pin only freezes it in place.
            var seed = CurrentValueAt(track, axisPosition);
            var pinned = track.TryGetOverride(axisPosition, out var existing);
            if (pinned) seed = existing.Value;

            _pinLabel.text = $"{char.ToUpperInvariant(AxisLabel()[0])}{AxisLabel().Substring(1)} {axisPosition}";
            _pinValue.SetValueWithoutNotify(seed);
            _pinClearButton.SetEnabled(pinned);
            _pinBar.style.display = DisplayStyle.Flex;

            _pinValue.Focus();
        }

        private void ClosePinBar()
        {
            _pinPosition = int.MinValue;
            if (_pinBar != null) _pinBar.style.display = DisplayStyle.None;
        }

        private double CurrentValueAt(BalanceTrack track, int axisPosition)
        {
            var index = track.IndexOfAxisPosition(axisPosition);
            if (index < 0) return 0d;

            var series = _cache.GetSeries(track.Id);
            if (series == null || index >= series.Length) return 0d;

            var value = series[index];
            return double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
        }

        private void SetPin(int axisPosition, double value)
        {
            var track = SelectedTrack;
            if (track == null || axisPosition == int.MinValue) return;

            Undo.RecordObject(_profile, "Pin value");

            var overrides = track.Overrides;
            var replaced = false;

            for (var i = 0; i < overrides.Count; i++)
            {
                if (overrides[i] == null || overrides[i].Index != axisPosition) continue;

                overrides[i].Value = value;
                replaced = true;
                break;
            }

            if (!replaced) overrides.Add(new ManualOverride(axisPosition, value));

            _pinClearButton?.SetEnabled(true);
            CommitPinEdit();
        }

        private void ClearPin(int axisPosition)
        {
            var track = SelectedTrack;
            if (track == null || axisPosition == int.MinValue) return;

            Undo.RecordObject(_profile, "Clear pin");

            var overrides = track.Overrides;
            for (var i = overrides.Count - 1; i >= 0; i--)
            {
                if (overrides[i] != null && overrides[i].Index == axisPosition) overrides.RemoveAt(i);
            }

            if (axisPosition == _pinPosition)
            {
                _pinClearButton?.SetEnabled(false);
                _pinValue?.SetValueWithoutNotify(CurrentValueAt(track, axisPosition));
            }

            CommitPinEdit();
        }

        private void ClearAllPins()
        {
            var track = SelectedTrack;
            if (track == null || track.Overrides.Count == 0) return;

            if (!EditorUtility.DisplayDialog(
                "Clear pins",
                $"Remove all {track.Overrides.Count} pinned value(s) from "
                + $"'{track.DisplayName ?? track.Id}'?",
                "Clear", "Cancel"))
            {
                return;
            }

            Undo.RecordObject(_profile, "Clear pins");
            track.Overrides.Clear();

            ClosePinBar();
            CommitPinEdit();
        }

        /// <summary>Pushes an override edit through evaluation, the graph and the inspector.</summary>
        private void CommitPinEdit()
        {
            EditorUtility.SetDirty(_profile);
            _serializedProfile?.Update();

            // A pinned value can be read by another track, so the whole profile is stale.
            _cache.InvalidateValues();

            RefreshSelection();
            RequestSimulation();
        }

        /// <summary>The axis positions the selected track pins, for the graph markers.</summary>
        private IEnumerable<int> SelectedPins()
        {
            var track = SelectedTrack;
            if (track == null) yield break;

            foreach (var entry in track.Overrides)
            {
                if (entry != null) yield return entry.Index;
            }
        }
    }
}
