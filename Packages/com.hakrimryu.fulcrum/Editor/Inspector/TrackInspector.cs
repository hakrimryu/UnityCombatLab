using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Inspector
{
    /// <summary>
    /// Builds the editing surface for one track from its serialized data.
    /// </summary>
    /// <remarks>
    /// Nothing here knows the shape of any particular model. Fields come from walking the
    /// <see cref="SerializedProperty"/> tree, so a model with parameters this package has
    /// never seen still gets a usable inspector. Writing one drawer per model type would
    /// mean every extension needs a matching editor, which defeats the point of the
    /// extension point.
    /// </remarks>
    internal sealed class TrackInspector
    {
        private readonly VisualElement _root;
        private SerializedObject _serializedProfile;
        private int _trackIndex = -1;

        /// <summary>Raised after any edit, once the change has been applied to the asset.</summary>
        public event Action Changed;

        /// <summary>Supplies the evaluated series for the track being edited, for previews.</summary>
        public Func<double[]> SeriesProvider { get; set; }

        public TrackInspector(VisualElement root) => _root = root;

        /// <summary>Rebuilds the surface for the track at <paramref name="trackIndex"/>.</summary>
        public void Bind(SerializedObject serializedProfile, int trackIndex)
        {
            _serializedProfile = serializedProfile;
            _trackIndex = trackIndex;
            Rebuild();
        }

        private SerializedProperty TrackProperty()
        {
            if (_serializedProfile == null || _trackIndex < 0) return null;

            var tracks = _serializedProfile.FindProperty("_tracks");
            if (tracks == null || _trackIndex >= tracks.arraySize) return null;

            return tracks.GetArrayElementAtIndex(_trackIndex);
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

            var track = TrackProperty();
            if (track == null)
            {
                _root.Add(new Label("Select a track."));
                return;
            }

            _serializedProfile.Update();

            _root.Add(Section("Track"));
            foreach (var name in new[] { "_id", "_displayName", "_axisId", "_rangeMin", "_rangeMax", "_accumulate" })
            {
                var property = track.FindPropertyRelative(name);
                if (property != null) _root.Add(Bound(property));
            }

            _root.Add(Section("Model"));
            _root.Add(BuildReferenceEditor<GrowthModel>(
                track.FindPropertyRelative("_model"), "Model", "Model"));
            _root.Add(BuildPreview(track));

            _root.Add(Section("Post-processing"));
            _root.Add(BuildChainEditor(track.FindPropertyRelative("_postProcessors")));

            _root.Add(Section("Overrides"));
            var overrides = track.FindPropertyRelative("_overrides");
            if (overrides != null) _root.Add(Bound(overrides));
        }

        /// <summary>
        /// A few of the numbers the track actually produces.
        /// </summary>
        /// <remarks>
        /// The graph shows shape; this answers "so what is it at stage 1, and at the end".
        /// Designers check a curve by reading values, and having to hover the graph for each
        /// one turns a glance into a task.
        /// </remarks>
        private VisualElement BuildPreview(SerializedProperty track)
        {
            var row = new Label();
            row.AddToClassList("fulcrum-preview");

            var values = SeriesProvider?.Invoke();
            if (values == null || values.Length == 0)
            {
                row.text = string.Empty;
                return row;
            }

            var min = track.FindPropertyRelative("_rangeMin");
            var start = min?.intValue ?? 0;

            var samples = new List<string>();
            foreach (var index in PreviewIndices(values.Length))
            {
                samples.Add($"{start + index} → {Format(values[index])}");
            }

            row.text = string.Join("     ", samples);
            return row;
        }

        private static IEnumerable<int> PreviewIndices(int count)
        {
            yield return 0;
            if (count > 2) yield return count / 2;
            if (count > 1) yield return count - 1;
        }

        private static string Format(double value)
        {
            if (double.IsNaN(value)) return "—";
            if (double.IsInfinity(value)) return "∞";
            return Math.Abs(value) >= 1000d ? value.ToString("N0") : value.ToString("0.###");
        }

        private PropertyField Bound(SerializedProperty property)
        {
            var field = new PropertyField(property);
            field.Bind(_serializedProfile);
            field.RegisterValueChangeCallback(_ => Apply());
            return field;
        }

        /// <summary>
        /// A parameter row: the field itself, plus a slider when the type declared a
        /// sensible range.
        /// </summary>
        /// <remarks>
        /// A bare number box gives no sense of scale. Someone typing into "how quickly it
        /// steepens" has no way to know whether 1.1 or 11 is the interesting neighbourhood,
        /// and finds out only by trying. The slider answers that before the first edit; the
        /// field beside it still accepts anything.
        /// </remarks>
        private VisualElement BoundParameter(SerializedProperty property, object owner)
        {
            var field = Bound(property);
            var range = FindRange(owner, property.name);
            if (range == null || property.propertyType != SerializedPropertyType.Float) return field;

            var container = new VisualElement();
            container.Add(field);

            var slider = new Slider((float)range.Min, (float)range.Max)
            {
                value = Mathf.Clamp((float)property.doubleValue, (float)range.Min, (float)range.Max),
                showInputField = false,
            };
            slider.AddToClassList("fulcrum-param-slider");
            slider.RegisterValueChangedCallback(evt =>
            {
                property.doubleValue = evt.newValue;
                Apply();
            });

            // The field is authoritative; the slider follows it, including past its own ends.
            field.RegisterValueChangeCallback(_ =>
                slider.SetValueWithoutNotify(
                    Mathf.Clamp((float)property.doubleValue, (float)range.Min, (float)range.Max)));

            container.Add(slider);
            return container;
        }

        private static FulcrumRangeAttribute FindRange(object owner, string fieldName)
        {
            if (owner == null || string.IsNullOrEmpty(fieldName)) return null;

            var field = owner.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);

            return field == null
                ? null
                : (FulcrumRangeAttribute)Attribute.GetCustomAttribute(field, typeof(FulcrumRangeAttribute));
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

            if (_serializedProfile == null) return;

            // Writing through SerializedObject is what registers the undo step and marks
            // the asset dirty; assigning to the model directly would do neither.
            _serializedProfile.ApplyModifiedProperties();
            Changed?.Invoke();
        }

        /// <summary>
        /// A type dropdown plus the chosen instance's own fields.
        /// </summary>
        private VisualElement BuildReferenceEditor<T>(SerializedProperty property, string label, string suffix)
        {
            var container = new VisualElement();
            if (property == null) return container;

            var types = SerializedReferenceCatalog.Implementations<T>();
            var choices = new List<string> { "(none)" };
            foreach (var type in types) choices.Add(SerializedReferenceCatalog.DisplayName(type, suffix));

            var current = property.managedReferenceValue;
            var index = current == null ? 0 : IndexOf(types, current.GetType()) + 1;

            var dropdown = new PopupField<string>(label, choices, Math.Max(index, 0));
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var selected = choices.IndexOf(evt.newValue);
                property.managedReferenceValue = selected <= 0
                    ? null
                    : Activator.CreateInstance(types[selected - 1]);
                Apply();
                Rebuild();
            });
            container.Add(dropdown);

            if (current != null)
            {
                var summary = SerializedReferenceCatalog.Summary(current.GetType());
                if (!string.IsNullOrEmpty(summary))
                {
                    var hint = new Label(summary);
                    hint.AddToClassList("fulcrum-hint");
                    container.Add(hint);
                }

                var body = new VisualElement();
                body.style.marginLeft = 12f;
                AddChildProperties(body, property);
                container.Add(body);
            }

            return container;
        }

        /// <summary>
        /// Walks a managed reference's own serialized fields.
        /// </summary>
        /// <remarks>
        /// A <see cref="PropertyField"/> on the reference itself would draw the dropdown
        /// Unity supplies as well as ours, so the children are drawn individually.
        /// </remarks>
        private void AddChildProperties(VisualElement parent, SerializedProperty property)
        {
            var owner = property.managedReferenceValue;
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            var entered = false;

            while (iterator.NextVisible(!entered))
            {
                entered = true;
                if (SerializedProperty.EqualContents(iterator, end)) break;
                if (iterator.depth <= property.depth) break;

                parent.Add(BoundParameter(iterator.Copy(), owner));
            }
        }

        private VisualElement BuildChainEditor(SerializedProperty list)
        {
            var container = new VisualElement();
            if (list == null) return container;

            for (var i = 0; i < list.arraySize; i++)
            {
                container.Add(BuildChainRow(list, i));
            }

            var add = new Button(() =>
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = null;
                Apply();
                Rebuild();
            })
            { text = "Add step" };
            container.Add(add);

            return container;
        }

        private VisualElement BuildChainRow(SerializedProperty list, int index)
        {
            var row = new VisualElement();
            row.style.marginBottom = 4f;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            var editor = BuildReferenceEditor<PostProcessor>(
                list.GetArrayElementAtIndex(index), $"{index + 1}.", "Processor");
            editor.style.flexGrow = 1f;
            header.Add(editor);

            // Order is configuration here, so moving a step has to be possible.
            header.Add(SmallButton("▲", () => Move(list, index, index - 1)));
            header.Add(SmallButton("▼", () => Move(list, index, index + 1)));
            header.Add(SmallButton("✕", () =>
            {
                list.DeleteArrayElementAtIndex(index);
                Apply();
                Rebuild();
            }));

            row.Add(header);
            return row;
        }

        private void Move(SerializedProperty list, int from, int to)
        {
            if (to < 0 || to >= list.arraySize) return;

            list.MoveArrayElement(from, to);
            Apply();
            Rebuild();
        }

        private static Button SmallButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.width = 22f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            return button;
        }

        private static int IndexOf(IReadOnlyList<Type> types, Type type)
        {
            for (var i = 0; i < types.Count; i++)
            {
                if (types[i] == type) return i;
            }

            return -1;
        }

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("fulcrum-inspector__section");
            return label;
        }
    }
}
