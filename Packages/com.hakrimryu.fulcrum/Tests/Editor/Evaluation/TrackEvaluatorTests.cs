using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Evaluation
{
    public sealed class TrackEvaluatorTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        private static BalanceTrack Track(int min, int max, GrowthModel model)
            => new BalanceTrack("t", "stage", min, max, model);

        [Test]
        public void Evaluate_WalksTheRangeUsingAxisPositions()
        {
            // The model sees stage numbers, not array offsets: a track starting at 10
            // evaluates f(10) into slot 0.
            var track = Track(10, 13, new LinearModel(1d));

            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 10d, 11d, 12d, 13d }));
        }

        [Test]
        public void Evaluate_ReturnsEmptyForAnInvertedRange()
        {
            Assert.That(TrackEvaluator.Evaluate(Track(50, 10, new LinearModel(1d)), Ctx), Is.Empty);
        }

        [Test]
        public void Evaluate_ReturnsZerosWhenNoModelIsSet()
        {
            // A track without a curve is a normal authoring state, not a failure.
            var track = Track(1, 3, null);

            Assert.That(TrackEvaluator.Evaluate(track, Ctx), Is.EqualTo(new[] { 0d, 0d, 0d }));
        }

        [Test]
        public void Evaluate_ToleratesANullTrackOrContext()
        {
            Assert.That(TrackEvaluator.Evaluate(null, Ctx), Is.Empty);
            Assert.That(TrackEvaluator.Evaluate(Track(1, 2, new LinearModel(5d)), null),
                Is.EqualTo(new[] { 5d, 10d }));
        }

        [Test]
        public void PostProcessors_RunBeforeAccumulation()
        {
            // Each step is rounded to 3 significant digits and the total is the sum of
            // those chosen numbers. Accumulating first would give 3846 rounded to 3850 —
            // a total whose individual steps are whatever the arithmetic left over.
            var track = Track(1, 3, new PowerModel(1000d, 0.5d));
            track.PostProcessors.Add(new RoundProcessor(RoundingMode.SignificantDigits, 3));
            track.Accumulate = true;

            // 1000, 1414.21…, 1732.05… → rounded 1000, 1410, 1730 → totals below.
            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 1000d, 2410d, 4140d }));
        }

        [Test]
        public void Accumulate_TurnsPerStepValuesIntoRunningTotals()
        {
            var track = Track(1, 4, new LinearModel(0d, 100d));
            track.Accumulate = true;

            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 100d, 200d, 300d, 400d }));
        }

        [Test]
        public void Overrides_ReplaceASinglePositionOnAnOrdinaryTrack()
        {
            var track = Track(10, 14, new LinearModel(1d));
            track.Overrides.Add(new ManualOverride(12, 999d, "boss stage"));

            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 10d, 11d, 999d, 13d, 14d }));
        }

        [Test]
        public void Overrides_UseAxisPositionsNotArrayOffsets()
        {
            var track = Track(10, 14, new LinearModel(1d));
            track.Overrides.Add(new ManualOverride(2, 999d));

            // Position 2 is outside stages 10..14, so nothing changes.
            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 10d, 11d, 12d, 13d, 14d }));
        }

        [Test]
        public void Overrides_OutsideTheRangeAreIgnoredRatherThanRejected()
        {
            var track = Track(10, 12, new LinearModel(1d));
            track.Overrides.Add(new ManualOverride(500, 1d));
            track.Overrides.Add(new ManualOverride(-3, 1d));
            track.Overrides.Add(null);

            Assert.DoesNotThrow(() => TrackEvaluator.Evaluate(track, Ctx));
            Assert.That(TrackEvaluator.Evaluate(track, Ctx), Is.EqualTo(new[] { 10d, 11d, 12d }));
        }

        [Test]
        public void Overrides_CarryForwardOnACumulativeTrack()
        {
            // Steps of 100 give totals 100, 200, 300, 400, 500. Pinning the total at
            // stage 3 to 1000 raises everything after it by the same 700, so the step
            // between stages stays 100 instead of collapsing to close the gap.
            var track = Track(1, 5, new LinearModel(0d, 100d));
            track.Accumulate = true;
            track.Overrides.Add(new ManualOverride(3, 1000d));

            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 100d, 200d, 1000d, 1100d, 1200d }));
        }

        [Test]
        public void CumulativeOverrides_ComposeInAxisOrder()
        {
            var track = Track(1, 6, new LinearModel(0d, 100d));
            track.Accumulate = true;
            track.Overrides.Add(new ManualOverride(5, 5000d));
            track.Overrides.Add(new ManualOverride(2, 1000d));

            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 100d, 1000d, 1100d, 1200d, 5000d, 5100d }));
        }

        [Test]
        public void ACumulativeOverrideCanLowerTheRemainder()
        {
            var track = Track(1, 4, new LinearModel(0d, 100d));
            track.Accumulate = true;
            track.Overrides.Add(new ManualOverride(2, 50d));

            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 100d, 50d, 150d, 250d }));
        }

        [Test]
        public void RepeatedOverridesOnOnePosition_KeepTheLastEntry()
        {
            var track = Track(1, 3, new LinearModel(1d));
            track.Overrides.Add(new ManualOverride(2, 10d));
            track.Overrides.Add(new ManualOverride(2, 20d));

            Assert.That(TrackEvaluator.Evaluate(track, Ctx), Is.EqualTo(new[] { 1d, 20d, 3d }));
        }

        [Test]
        public void Evaluate_AppliesTheWholePipelineInOrder()
        {
            // model → post-processing → accumulation → overrides, on one track.
            var track = Track(1, 4, new LinearModel(0d, 12847d));
            track.PostProcessors.Add(new RoundProcessor(RoundingMode.SignificantDigits, 3));
            track.Accumulate = true;
            track.Overrides.Add(new ManualOverride(3, 100000d));

            // 12847 → 12800 each step; totals 12800, 25600, 38400 → pinned to 100000 at
            // stage 3, lifting stage 4 by the same amount.
            Assert.That(TrackEvaluator.Evaluate(track, Ctx),
                Is.EqualTo(new[] { 12800d, 25600d, 100000d, 112800d }));
        }
    }

    public sealed class TrackEvaluatorProfileTests
    {
        private static BalanceProfile BuildProfile()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Globals.Add(new GlobalConstant("difficulty", 2d));

            var hp = new BalanceTrack("monster_hp", "stage", 1, 3, new LinearModel(0d, 100d));
            hp.PostProcessors.Add(new MultiplierProcessor(1d, "difficulty"));
            profile.Tracks.Add(hp);

            profile.Tracks.Add(new BalanceTrack("gold", "stage", 1, 3, new LinearModel(10d)));
            profile.Tracks.Add(new BalanceTrack(null, "stage", 1, 3, new LinearModel(1d)));

            return profile;
        }

        [Test]
        public void EvaluateWithProfile_SuppliesTheProfileConstants()
        {
            var profile = BuildProfile();
            try
            {
                profile.TryGetTrack("monster_hp", out var hp);

                Assert.That(TrackEvaluator.Evaluate(profile, hp),
                    Is.EqualTo(new[] { 200d, 200d, 200d }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EvaluateAll_KeysBySourceTrackAndSkipsUnnamedOnes()
        {
            var profile = BuildProfile();
            try
            {
                var all = TrackEvaluator.EvaluateAll(profile);

                Assert.That(all.Count, Is.EqualTo(2));
                Assert.That(all["monster_hp"], Is.EqualTo(new[] { 200d, 200d, 200d }));
                Assert.That(all["gold"], Is.EqualTo(new[] { 10d, 20d, 30d }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EvaluateAll_ToleratesANullProfile()
        {
            Assert.That(TrackEvaluator.EvaluateAll(null), Is.Empty);
        }
    }

    public sealed class BalanceTrackIndexingTests
    {
        [Test]
        public void AxisPositionAndIndex_RoundTrip()
        {
            var track = new BalanceTrack("t", "stage", 10, 50);

            Assert.That(track.IndexOfAxisPosition(10), Is.Zero);
            Assert.That(track.IndexOfAxisPosition(42), Is.EqualTo(32));
            Assert.That(track.AxisPositionAt(32), Is.EqualTo(42));
        }

        [Test]
        public void IndexOfAxisPosition_ReportsPositionsOutsideTheRange()
        {
            var track = new BalanceTrack("t", "stage", 10, 50);

            Assert.That(track.IndexOfAxisPosition(9), Is.EqualTo(-1));
            Assert.That(track.IndexOfAxisPosition(51), Is.EqualTo(-1));
        }
    }
}
