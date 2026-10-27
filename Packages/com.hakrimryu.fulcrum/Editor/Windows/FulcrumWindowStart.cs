using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// What the window shows before there is anything to show, and how to get past it.
    /// </summary>
    /// <remarks>
    /// The first five minutes decide whether a tool gets used. Someone who creates a
    /// profile, opens this and finds an empty list has learned nothing about what the tool
    /// does and has no obvious next step.
    ///
    /// The starter adds a small set of tracks with plausible curves rather than asking a
    /// series of questions. What a designer wants first is something on screen to react to;
    /// every value here is meant to be changed.
    /// </remarks>
    public sealed partial class FulcrumWindow
    {
        private VisualElement _startPanel;

        private void BuildStartPanel(VisualElement root)
        {
            _startPanel = root.Q<VisualElement>("start-panel");

            var title = new Label("Nothing to show yet");
            title.AddToClassList("fulcrum-empty__title");

            var body = new Label(
                "A profile holds the curves for one set of balance numbers. Add tracks with "
                + "the + button on the left, or start from a typical setup and change it.");
            body.AddToClassList("fulcrum-empty__body");

            var starter = new Button(AddStarterTracks) { text = "Add a typical stage setup" };
            starter.tooltip =
                "Creates a stage axis and the tracks a stage-based game usually needs. "
                + "Every number is a starting point, not a recommendation.";

            _startPanel.Add(title);
            _startPanel.Add(body);
            _startPanel.Add(starter);
        }

        private void RefreshStartPanel()
        {
            if (_startPanel == null) return;

            var show = _profile != null && _profile.Tracks.Count == 0;
            _startPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Creates a stage axis and a small set of tracks that already simulate.
        /// </summary>
        /// <remarks>
        /// The set is chosen so the Issues pane has something to say immediately: monster
        /// stats to fight, experience to level on, currency to spend and something to spend
        /// it on. Leaving any of them out means part of the analysis silently reports
        /// nothing, which reads as the feature being broken.
        /// </remarks>
        private void AddStarterTracks()
        {
            if (_profile == null) return;

            Undo.RecordObject(_profile, "Add starter tracks");

            if (_profile.Axes.Count == 0)
            {
                _profile.Axes.Add(new AxisDefinition("stage", 1, 200, "Stage"));
            }

            if (_profile.Globals.Count == 0)
            {
                _profile.Globals.Add(new GlobalConstant("difficulty", 1d, "Multiplies every enemy stat."));
            }

            AddTrack("monster_hp", "Monster HP", 1, 200, new PowerModel(70d, 1.05d), new PostProcessor[]
            {
                new RoundProcessor(RoundingMode.SignificantDigits, 3),
                new MultiplierProcessor(1d, "difficulty"),
                ClampProcessor.AtLeast(1d),
            });

            AddTrack("monster_atk", "Monster ATK", 1, 200, new PowerModel(6d, 0.9d), new PostProcessor[]
            {
                new RoundProcessor(RoundingMode.Nearest),
                new MinDeltaProcessor(1d),
            });

            AddTrack("stage_exp", "EXP per stage", 1, 200, new PowerModel(40d, 1.2d), new PostProcessor[]
            {
                new RoundProcessor(RoundingMode.SignificantDigits, 3),
            });

            AddTrack("exp_required", "EXP to next level", 1, 99, new ExponentialModel(120d, 1.15d),
                new PostProcessor[] { new RoundProcessor(RoundingMode.SignificantDigits, 3) });

            AddTrack("stage_gold", "Gold per stage", 1, 200, new PowerModel(30d, 1.1d), new PostProcessor[]
            {
                new RoundProcessor(RoundingMode.SignificantDigits, 2),
            });

            AddTrack("upgrade_cost", "Upgrade cost", 1, 300, new ExponentialModel(100d, 1.12d),
                new PostProcessor[] { new RoundProcessor(RoundingMode.SignificantDigits, 3) });

            // Binding them up front is the point: a starter that still reports nothing has
            // not demonstrated anything.
            var binding = _profile.SimulationBinding;
            binding.AxisId = "stage";
            binding.MonsterHp = "monster_hp";
            binding.MonsterAttack = "monster_atk";
            binding.StageExp = "stage_exp";
            binding.ExpRequired = "exp_required";
            binding.StageGold = "stage_gold";
            binding.UpgradeCost = "upgrade_cost";

            if (string.IsNullOrEmpty(_profile.Notes))
            {
                _profile.Notes =
                    "Started from the typical stage setup. Replace this with why the numbers "
                    + "are what they are — that reasoning is the part a spreadsheet loses.";
            }

            EditorUtility.SetDirty(_profile);
            _serializedProfile?.Update();

            _cache.InvalidateStructure();
            RefreshTrackList();
            SelectedTrackIndex = 0;
            RefreshSelection();
            RefreshToolbar();
            RebindProfileInspector();
            RefreshStartPanel();
            RequestSimulation();
        }
    }
}
