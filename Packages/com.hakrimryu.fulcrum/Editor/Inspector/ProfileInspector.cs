using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Inspector
{
    /// <summary>
    /// The profile's own settings: what it is called, why it is tuned this way, the axes
    /// it runs over, its shared constants, and which track fills each simulated role.
    /// </summary>
    /// <remarks>
    /// All of this used to live only in the default inspector on the asset, which meant a
    /// designer had to leave the window and edit raw lists to configure anything. The
    /// simulation binding was the worst of it: without it the Issues pane simply reported
    /// nothing, with no indication that a setting was missing.
    /// </remarks>
    internal sealed class ProfileInspector
    {
        private readonly VisualElement _root;
        private SerializedObject _serialized;
        private BalanceProfile _profile;

        /// <summary>Raised after an edit, once it has been applied to the asset.</summary>
        public event Action Changed;

        /// <summary>Raised when an edit changed the set of tracks or axes.</summary>
        public event Action StructureChanged;

        public ProfileInspector(VisualElement root) => _root = root;

        public void Bind(SerializedObject serialized, BalanceProfile profile)
        {
            _serialized = serialized;
            _profile = profile;
            Rebuild();
        }

        private void Rebuild()
        {
            _suppressChanges = true;
            try { RebuildCore(); }
            finally { _suppressChanges = false; }
        }

        private void RebuildCore()
        {
            _root.Clear();

            if (_serialized == null || _profile == null)
            {
                _root.Add(new Label("No profile selected."));
                return;
            }

            _serialized.Update();

            _root.Add(Section("Profile"));
            _root.Add(Bound("_profileName", "Name"));

            var notes = Bound("_notes", "Notes");
            notes.tooltip =
                "Why this profile is tuned the way it is. A spreadsheet records what a "
                + "number is; it almost never records why, and that reasoning is what "
                + "disappears when someone moves on.";
            _root.Add(notes);

            _root.Add(Section("Axes"));
            _root.Add(Hint("What the tracks run over — stage, character level, equipment tier."));
            _root.Add(BoundStructural("_axes"));

            _root.Add(Section("Shared constants"));
            _root.Add(Hint("Values several tracks read by name, such as a difficulty multiplier."));
            _root.Add(Bound("_globals"));

            _root.Add(Section("Simulation"));
            _root.Add(Hint("How many fights to run per position, and the seed. A fixed seed "
                           + "means two runs of the same profile agree."));
            _root.Add(Bound("_simulationSettings"));

            _root.Add(Section("Simulation binding"));
            _root.Add(Hint("Which track fills each role. The simulators cannot guess these — "
                           + "leave one empty and that part of the analysis is skipped."));
            _root.Add(BuildBinding());
        }

        // --- binding --------------------------------------------------------------------

        private static readonly (string Field, string Label, string Tooltip)[] BindingRoles =
        {
            ("_axisId", "Axis", "The axis the simulation walks along."),
            ("_monsterHp", "Monster HP", "Health of what the player fights at each position."),
            ("_monsterAttack", "Monster ATK", "Damage the opponent deals."),
            ("_monsterDefense", "Monster DEF", "Optional. Reduces the player's damage."),
            ("_stageExp", "EXP gained", "Experience granted by clearing a position."),
            ("_expRequired", "EXP to level", "Experience needed for one level, indexed by level."),
            ("_stageGold", "Currency gained", "Currency granted by clearing a position."),
            ("_upgradeCost", "Upgrade cost", "Cost of one upgrade, indexed by how many are owned."),
        };

        /// <summary>
        /// Roles are chosen from the tracks that exist rather than typed.
        /// </summary>
        /// <remarks>
        /// A typed id that does not match any track fails silently — the simulator reads
        /// nothing and the analysis quietly covers less than it appears to.
        /// </remarks>
        private VisualElement BuildBinding()
        {
            var container = new VisualElement();
            var binding = _serialized.FindProperty("_simulationBinding");
            if (binding == null) return container;

            var trackIds = new List<string> { "(none)" };
            foreach (var track in _profile.Tracks)
            {
                if (track != null && !string.IsNullOrEmpty(track.Id)) trackIds.Add(track.Id);
            }

            var axisIds = new List<string> { "(none)" };
            foreach (var axis in _profile.Axes)
            {
                if (axis != null && !string.IsNullOrEmpty(axis.Id)) axisIds.Add(axis.Id);
            }

            foreach (var role in BindingRoles)
            {
                var property = binding.FindPropertyRelative(role.Field);
                if (property == null) continue;

                var choices = role.Field == "_axisId" ? axisIds : trackIds;
                container.Add(BuildRolePicker(property, role.Label, role.Tooltip, choices));
            }

            return container;
        }

        private VisualElement BuildRolePicker(
            SerializedProperty property, string label, string tooltip, List<string> choices)
        {
            var current = property.stringValue;
            var index = string.IsNullOrEmpty(current) ? 0 : choices.IndexOf(current);

            // A binding left pointing at a deleted track has to stay visible, or the
            // designer cannot tell why the simulation stopped covering it.
            if (index < 0)
            {
                choices = new List<string>(choices) { current + "  (missing)" };
                index = choices.Count - 1;
            }

            var field = new PopupField<string>(label, choices, index) { tooltip = tooltip };
            field.RegisterValueChangedCallback(evt =>
            {
                var selected = choices.IndexOf(evt.newValue);
                property.stringValue = selected <= 0 ? string.Empty : choices[selected];
                Apply();
            });

            return field;
        }

        // --- helpers ---------------------------------------------------------------------

        private PropertyField Bound(string name, string label = null)
        {
            var property = _serialized.FindProperty(name);
            if (property == null) return new PropertyField();

            var field = label == null ? new PropertyField(property) : new PropertyField(property, label);
            field.Bind(_serialized);
            field.RegisterValueChangeCallback(_ => Apply());
            return field;
        }

        /// <summary>A field whose edits can change what tracks refer to.</summary>
        private PropertyField BoundStructural(string name)
        {
            var property = _serialized.FindProperty(name);
            if (property == null) return new PropertyField();

            var field = new PropertyField(property);
            field.Bind(_serialized);
            field.RegisterValueChangeCallback(_ =>
            {
                Apply();
                StructureChanged?.Invoke();
            });
            return field;
        }

        /// <summary>
        /// Guards against the loop a rebuild would otherwise start.
        /// </summary>
        /// <remarks>
        /// PropertyField raises its change callback when it is bound, not only when a human
        /// edits it. Without this, applying an edit rebuilds the surface, the fresh fields
        /// report a change on bind, and that applies another edit — forever.
        /// </remarks>
        private bool _suppressChanges;

        private void Apply()
        {
            if (_suppressChanges) return;

            if (_serialized == null) return;

            _serialized.ApplyModifiedProperties();
            Changed?.Invoke();
        }

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("fulcrum-inspector__section");
            return label;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("fulcrum-hint");
            return label;
        }
    }
}
