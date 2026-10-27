using UnityEditor;
using UnityEngine;

namespace Fulcrum.Tests.Fixtures
{
    /// <summary>
    /// Regenerates the committed migration fixture.
    /// </summary>
    /// <remarks>
    /// The fixture is a frozen asset, not something the tests rebuild. That is the whole
    /// point: a profile constructed in code cannot detect a rename, because the code and
    /// the data change together. A committed <c>.asset</c> still says <c>PowerModel</c>
    /// after someone renames the class, so loading it produces null and the test fails.
    ///
    /// Regenerate only when a new model or processor is added, and commit the result.
    ///
    /// This writes into the package folder, so it only works while the package is embedded
    /// or local. A package installed from a Git URL lives in a read-only cache. The tests
    /// themselves never write here — they stage what they need under <c>Assets/</c> — so a
    /// read-only install still runs them.
    /// </remarks>
    internal static class MigrationFixtureBuilder
    {
        public const string FixtureFolder = "Packages/com.hakrimryu.fulcrum/Tests/Editor/Fixtures";
        public const string FixturePath = FixtureFolder + "/MigrationFixture.asset";

        [MenuItem("Window/Fulcrum/Regenerate Migration Fixture")]
        public static void Regenerate()
        {
            var profile = Build();
            AssetDatabase.CreateAsset(profile, FixturePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Builds a profile that exercises every serialized reference type the package
        /// ships, including a nested piecewise model and a processor chain.
        /// </summary>
        public static BalanceProfile Build()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.ProfileName = "Migration fixture";
            profile.Notes =
                "Frozen asset used to detect broken [SerializeReference] links. "
                + "Covers every shipped model and processor. Regenerate from "
                + "Window/Fulcrum/Regenerate Migration Fixture when a type is added.";

            profile.Axes.Add(new AxisDefinition("stage", 1, 200, "Stage"));
            profile.Axes.Add(new AxisDefinition("char_level", 1, 99, "Character Level"));
            profile.Globals.Add(new GlobalConstant("difficulty", 1.3d, "Hard mode multiplier"));
            profile.SimulationSettings = new SimulationSettings(1000, 20260804);

            profile.Tracks.Add(TrackWith("linear", new LinearModel(2.5d, 10d)));
            profile.Tracks.Add(TrackWith("power", new PowerModel(100d, 1.35d, 5d)));
            profile.Tracks.Add(TrackWith("exponential", new ExponentialModel(50d, 1.15d)));
            profile.Tracks.Add(TrackWith("table", new TableModel(1d, 10d, 25d, 60d, 140d)));
            profile.Tracks.Add(TrackWith("curve",
                new CurveModel(AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), 200d, 1000d)));

            // Nested on purpose: a rename breaks references at any depth, not just the top.
            profile.Tracks.Add(TrackWith("piecewise", new PiecewiseModel(
                new PiecewiseSegment(1d, new LinearModel(10d)),
                new PiecewiseSegment(50d, new PiecewiseModel(
                    new PiecewiseSegment(50d, new PowerModel(2d, 2d)),
                    new PiecewiseSegment(120d, new ExponentialModel(3d, 1.05d)))))));

            // Derived from other tracks, which is also what makes the fixture exercise the
            // dependency graph rather than only the serialisation.
            profile.Tracks.Add(TrackWith("derived", new CompositeModel(
                CompositeOperation.Product,
                new TrackReferenceModel("linear"),
                new TrackReferenceModel("power", 0.001d, axisOffset: -1))));

            var chained = TrackWith("full_chain", new PowerModel(1000d, 1.2d));
            chained.Accumulate = true;
            chained.PostProcessors.Add(new RoundProcessor(RoundingMode.SignificantDigits, 3));
            chained.PostProcessors.Add(new MultiplierProcessor(1.1d, "difficulty"));
            chained.PostProcessors.Add(new MinDeltaProcessor(5d));
            chained.PostProcessors.Add(new SmoothProcessor(5));
            chained.PostProcessors.Add(ClampProcessor.AtLeast(1d));
            chained.Overrides.Add(new ManualOverride(42, 99999d, "Chapter boundary spike"));
            profile.Tracks.Add(chained);

            return profile;
        }

        private static BalanceTrack TrackWith(string id, GrowthModel model)
            => new BalanceTrack(id, "stage", 1, 60, model);
    }
}
