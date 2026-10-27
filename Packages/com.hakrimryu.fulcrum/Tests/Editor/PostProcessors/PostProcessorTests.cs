using System.Collections.Generic;
using NUnit.Framework;

namespace Fulcrum.Tests.PostProcessors
{
    public sealed class RoundProcessorTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        private static double[] Run(PostProcessor processor, params double[] values)
        {
            processor.Process(values, Ctx);
            return values;
        }

        [Test]
        public void SignificantDigits_KeepsLeadingDigitsAcrossMagnitudes()
        {
            var processor = new RoundProcessor(RoundingMode.SignificantDigits, 3);

            Assert.That(Run(processor, 12847d)[0], Is.EqualTo(12800d));
            Assert.That(Run(processor, 1284.7d)[0], Is.EqualTo(1280d));
            Assert.That(Run(processor, 98765432d)[0], Is.EqualTo(98800000d));
            Assert.That(Run(processor, 7d)[0], Is.EqualTo(7d));
        }

        [Test]
        public void SignificantDigits_HandlesZeroAndNegatives()
        {
            var processor = new RoundProcessor(RoundingMode.SignificantDigits, 3);

            Assert.That(Run(processor, 0d)[0], Is.EqualTo(0d));
            Assert.That(Run(processor, -12847d)[0], Is.EqualTo(-12800d));
        }

        [Test]
        public void Nearest_RoundsHalvesAwayFromZero()
        {
            // Banker's rounding would give 2 here, which reads as a bug in a stat table.
            var processor = new RoundProcessor(RoundingMode.Nearest);

            Assert.That(Run(processor, 2.5d)[0], Is.EqualTo(3d));
            Assert.That(Run(processor, 3.5d)[0], Is.EqualTo(4d));
            Assert.That(Run(processor, -2.5d)[0], Is.EqualTo(-3d));
        }

        [Test]
        public void Decimals_SupportNegativeScalesForTensAndHundreds()
        {
            Assert.That(Run(new RoundProcessor(RoundingMode.Nearest, -2), 12847d)[0], Is.EqualTo(12800d));
            Assert.That(Run(new RoundProcessor(RoundingMode.Nearest, 1), 1.26d)[0], Is.EqualTo(1.3d).Within(1e-9));
        }

        [Test]
        public void FloorAndCeiling_RoundInTheStatedDirection()
        {
            Assert.That(Run(new RoundProcessor(RoundingMode.Floor), 9.9d)[0], Is.EqualTo(9d));
            Assert.That(Run(new RoundProcessor(RoundingMode.Ceiling), 9.1d)[0], Is.EqualTo(10d));
            Assert.That(Run(new RoundProcessor(RoundingMode.Floor), -9.1d)[0], Is.EqualTo(-10d));
        }

        [Test]
        public void NonFiniteValues_PassThroughUnchanged()
        {
            var processor = new RoundProcessor(RoundingMode.SignificantDigits, 3);

            Assert.That(double.IsNaN(Run(processor, double.NaN)[0]), Is.True);
            Assert.That(double.IsPositiveInfinity(Run(processor, double.PositiveInfinity)[0]), Is.True);
        }

        [Test]
        public void Describe_NamesTheMode()
        {
            Assert.That(new RoundProcessor(RoundingMode.SignificantDigits, 3).Describe(), Is.EqualTo("round 3sf"));
            Assert.That(new RoundProcessor(RoundingMode.Nearest).Describe(), Is.EqualTo("round"));
            Assert.That(new RoundProcessor(RoundingMode.Floor, 2).Describe(), Is.EqualTo("floor 2dp"));
        }
    }

    public sealed class ClampProcessorTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void AtLeast_RaisesValuesBelowTheFloor()
        {
            var values = new[] { -5d, 0d, 1d, 500d };
            ClampProcessor.AtLeast(1d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 1d, 1d, 1d, 500d }));
        }

        [Test]
        public void Between_HoldsProbabilitiesInsideZeroToOne()
        {
            var values = new[] { -0.2d, 0.5d, 1.4d };
            ClampProcessor.Between(0d, 1d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 0d, 0.5d, 1d }));
        }

        [Test]
        public void NaN_SnapsToABoundRatherThanSurviving()
        {
            // NaN compares false against everything, so an ordinary clamp lets it through.
            var values = new[] { double.NaN };
            ClampProcessor.AtLeast(1d).Process(values, Ctx);

            Assert.That(values[0], Is.EqualTo(1d));

            var ceilingOnly = new[] { double.NaN };
            ClampProcessor.AtMost(10d).Process(ceilingOnly, Ctx);
            Assert.That(ceilingOnly[0], Is.EqualTo(10d));
        }

        [Test]
        public void Describe_ShowsWhichBoundsApply()
        {
            Assert.That(ClampProcessor.AtLeast(1d).Describe(), Is.EqualTo("clamp ≥ 1"));
            Assert.That(ClampProcessor.AtMost(100d).Describe(), Is.EqualTo("clamp ≤ 100"));
            Assert.That(ClampProcessor.Between(0d, 1d).Describe(), Is.EqualTo("clamp 0..1"));
        }
    }

    public sealed class MultiplierProcessorTests
    {
        [Test]
        public void Factor_ScalesEveryValue()
        {
            var values = new[] { 10d, 20d };
            new MultiplierProcessor(1.5d).Process(values, new EvalContext(0));

            Assert.That(values, Is.EqualTo(new[] { 15d, 30d }));
        }

        [Test]
        public void GlobalKey_MultipliesOnTopOfTheLiteralFactor()
        {
            var context = new EvalContext(0, new Dictionary<string, double> { ["difficulty"] = 1.3d });
            var values = new[] { 100d };

            new MultiplierProcessor(2d, "difficulty").Process(values, context);

            Assert.That(values[0], Is.EqualTo(260d).Within(1e-9));
        }

        [Test]
        public void MissingGlobal_ResolvesToOneRatherThanZero()
        {
            // Zeroing a whole track would look like a broken formula rather than an
            // unset constant, and half-configured profiles are normal while authoring.
            var values = new[] { 100d };
            new MultiplierProcessor(2d, "not_defined").Process(values, new EvalContext(0));

            Assert.That(values[0], Is.EqualTo(200d));
        }

        [Test]
        public void Describe_MentionsTheGlobal()
        {
            Assert.That(new MultiplierProcessor(1.5d).Describe(), Is.EqualTo("× 1.5"));
            Assert.That(new MultiplierProcessor(1d, "difficulty").Describe(), Is.EqualTo("× 1 × difficulty"));
        }
    }

    public sealed class MinDeltaProcessorTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void FlatRuns_AreSpreadApart()
        {
            var values = new[] { 100d, 100d, 100d, 100d };
            new MinDeltaProcessor(1d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 100d, 101d, 102d, 103d }));
        }

        [Test]
        public void ValuesAlreadyRisingEnough_AreLeftAlone()
        {
            var values = new[] { 100d, 200d, 400d };
            new MinDeltaProcessor(1d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 100d, 200d, 400d }));
        }

        [Test]
        public void ARaiseCascadesForward()
        {
            // Lifting index 1 pushes index 2 up as well, even though 105 was already
            // above the original neighbour.
            var values = new[] { 100d, 100d, 105d };
            new MinDeltaProcessor(10d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 100d, 110d, 120d }));
        }

        [Test]
        public void DecreasingSeries_IsForcedUpward()
        {
            var values = new[] { 100d, 50d, 10d };
            new MinDeltaProcessor(5d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 100d, 105d, 110d }));
        }

        [Test]
        public void NaN_DoesNotPoisonTheRunningFloor()
        {
            var values = new[] { 100d, double.NaN, 100d };
            new MinDeltaProcessor(1d).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 100d, 101d, 102d }));
        }

        [Test]
        public void ShortSeries_AreUntouched()
        {
            var single = new[] { 5d };
            new MinDeltaProcessor(100d).Process(single, Ctx);

            Assert.That(single, Is.EqualTo(new[] { 5d }));
        }
    }

    public sealed class SmoothProcessorTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void CentredAverage_UsesTheOriginalNeighbours()
        {
            // Computed from a snapshot: index 2 averages 10, 100, 10 — not the already
            // smoothed value at index 1.
            var values = new[] { 10d, 10d, 100d, 10d, 10d };
            new SmoothProcessor(3).Process(values, Ctx);

            Assert.That(values[2], Is.EqualTo(40d).Within(1e-9));
            Assert.That(values[1], Is.EqualTo(40d).Within(1e-9));
            Assert.That(values[3], Is.EqualTo(40d).Within(1e-9));
        }

        [Test]
        public void WindowShrinksAtTheEnds()
        {
            var values = new[] { 0d, 30d, 60d };
            new SmoothProcessor(3).Process(values, Ctx);

            Assert.That(values[0], Is.EqualTo(15d).Within(1e-9));
            Assert.That(values[1], Is.EqualTo(30d).Within(1e-9));
            Assert.That(values[2], Is.EqualTo(45d).Within(1e-9));
        }

        [Test]
        public void SmoothingPreservesTheMean()
        {
            var values = new[] { 1d, 2d, 3d, 4d, 5d };
            new SmoothProcessor(3).Process(values, Ctx);

            Assert.That(values[2], Is.EqualTo(3d).Within(1e-9));
        }

        [Test]
        public void WindowsBelowThree_DisableSmoothing()
        {
            var values = new[] { 10d, 100d, 10d };
            new SmoothProcessor(1).Process(values, Ctx);

            Assert.That(values, Is.EqualTo(new[] { 10d, 100d, 10d }));
        }

        [Test]
        public void EvenWindows_AreTreatedAsTheNextOddSize()
        {
            var even = new[] { 10d, 10d, 100d, 10d, 10d };
            var odd = new[] { 10d, 10d, 100d, 10d, 10d };

            new SmoothProcessor(4).Process(even, Ctx);
            new SmoothProcessor(5).Process(odd, Ctx);

            Assert.That(even, Is.EqualTo(odd));
            Assert.That(new SmoothProcessor(4).Describe(), Is.EqualTo("smooth 5"));
        }
    }

    public sealed class PostProcessorChainTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void StepsRunInTheStoredOrder()
        {
            var values = new[] { 10d };
            var chain = new List<PostProcessor>
            {
                new MultiplierProcessor(3d),
                ClampProcessor.AtMost(25d),
            };

            PostProcessorChain.Apply(chain, values, Ctx);

            Assert.That(values[0], Is.EqualTo(25d));
        }

        [Test]
        public void OrderChangesTheResult()
        {
            // The same two steps, swapped. Clamping first caps the input before it is
            // scaled; scaling first caps the output. Both are legitimate intentions,
            // which is why the chain is never reordered for convenience.
            var clampThenScale = new[] { 10d };
            PostProcessorChain.Apply(
                new List<PostProcessor> { ClampProcessor.AtMost(25d), new MultiplierProcessor(3d) },
                clampThenScale, Ctx);

            var scaleThenClamp = new[] { 10d };
            PostProcessorChain.Apply(
                new List<PostProcessor> { new MultiplierProcessor(3d), ClampProcessor.AtMost(25d) },
                scaleThenClamp, Ctx);

            Assert.That(clampThenScale[0], Is.EqualTo(30d));
            Assert.That(scaleThenClamp[0], Is.EqualTo(25d));
            Assert.That(clampThenScale[0], Is.Not.EqualTo(scaleThenClamp[0]));
        }

        [Test]
        public void RoundingAfterMinDeltaCanUndoIt()
        {
            // Documents why a minimum-step rule belongs near the end of a chain.
            var minDeltaLast = new[] { 12847d, 12848d, 12849d };
            PostProcessorChain.Apply(
                new List<PostProcessor>
                {
                    new RoundProcessor(RoundingMode.SignificantDigits, 3),
                    new MinDeltaProcessor(1d),
                },
                minDeltaLast, Ctx);

            var roundLast = new[] { 12847d, 12848d, 12849d };
            PostProcessorChain.Apply(
                new List<PostProcessor>
                {
                    new MinDeltaProcessor(1d),
                    new RoundProcessor(RoundingMode.SignificantDigits, 3),
                },
                roundLast, Ctx);

            Assert.That(minDeltaLast, Is.EqualTo(new[] { 12800d, 12801d, 12802d }));
            Assert.That(roundLast, Is.EqualTo(new[] { 12800d, 12800d, 12800d }),
                "Rounding placed after the minimum-step rule flattens the gaps it created.");
        }

        [Test]
        public void NullEntries_AreSkipped()
        {
            var values = new[] { 10d };
            var chain = new List<PostProcessor> { null, new MultiplierProcessor(2d), null };

            Assert.DoesNotThrow(() => PostProcessorChain.Apply(chain, values, Ctx));
            Assert.That(values[0], Is.EqualTo(20d));
        }

        [Test]
        public void Describe_JoinsTheSteps()
        {
            var chain = new List<PostProcessor>
            {
                new RoundProcessor(RoundingMode.SignificantDigits, 3),
                ClampProcessor.AtLeast(1d),
            };

            Assert.That(PostProcessorChain.Describe(chain), Is.EqualTo("round 3sf → clamp ≥ 1"));
            Assert.That(PostProcessorChain.Describe(null), Is.Empty);
            Assert.That(PostProcessorChain.Describe(new List<PostProcessor>()), Is.Empty);
        }

        [Test]
        public void TrackExposesAChain()
        {
            var track = new BalanceTrack("monster_hp", new LinearModel(100d));
            track.PostProcessors.Add(new RoundProcessor(RoundingMode.SignificantDigits, 3));
            track.PostProcessors.Add(ClampProcessor.AtLeast(1d));

            Assert.That(PostProcessorChain.Describe(track.PostProcessors),
                Is.EqualTo("round 3sf → clamp ≥ 1"));
        }
    }
}
