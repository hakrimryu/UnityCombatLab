using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Models
{
    public sealed class LinearModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void Evaluate_MatchesHandCalculation()
        {
            var model = new LinearModel(slope: 2.5d, intercept: 10d);

            Assert.That(model.Evaluate(0d, Ctx), Is.EqualTo(10d));
            Assert.That(model.Evaluate(4d, Ctx), Is.EqualTo(20d));
            Assert.That(model.Evaluate(-2d, Ctx), Is.EqualTo(5d));
        }

        [Test]
        public void Describe_CollapsesUnitAndZeroTerms()
        {
            Assert.That(new LinearModel(2d, 5d).Describe(), Is.EqualTo("2 · x + 5"));
            Assert.That(new LinearModel(1d).Describe(), Is.EqualTo("x"));
            Assert.That(new LinearModel(3d, -7d).Describe(), Is.EqualTo("3 · x - 7"));
            Assert.That(new LinearModel(-1d, 0d).Describe(), Is.EqualTo("-x"));
        }
    }

    public sealed class PowerModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void Evaluate_MatchesHandCalculation()
        {
            var model = new PowerModel(scale: 100d, exponent: 2d, offset: 50d);

            Assert.That(model.Evaluate(1d, Ctx), Is.EqualTo(150d));
            Assert.That(model.Evaluate(3d, Ctx), Is.EqualTo(950d));
        }

        [Test]
        public void Evaluate_HandlesFractionalExponent()
        {
            var model = new PowerModel(scale: 100d, exponent: 0.5d);

            Assert.That(model.Evaluate(4d, Ctx), Is.EqualTo(200d).Within(1e-9));
            Assert.That(model.Evaluate(9d, Ctx), Is.EqualTo(300d).Within(1e-9));
        }

        [Test]
        public void Evaluate_ProducesNaN_ForNegativeBaseWithFractionalExponent()
        {
            // Documented rather than papered over: this is Math.Pow's contract, and the
            // clamp that hides it belongs in the post-processing chain.
            var model = new PowerModel(scale: 1d, exponent: 0.5d);

            Assert.That(double.IsNaN(model.Evaluate(-4d, Ctx)), Is.True);
        }

        [Test]
        public void Describe_ReadsAsAFormula()
        {
            Assert.That(new PowerModel(100d, 1.35d).Describe(), Is.EqualTo("100 · x^1.35"));
            Assert.That(new PowerModel(1d, 2d, 5d).Describe(), Is.EqualTo("x^2 + 5"));
        }
    }

    public sealed class ExponentialModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void Evaluate_MatchesHandCalculation()
        {
            var model = new ExponentialModel(scale: 100d, ratio: 2d);

            Assert.That(model.Evaluate(0d, Ctx), Is.EqualTo(100d));
            Assert.That(model.Evaluate(3d, Ctx), Is.EqualTo(800d));
            Assert.That(model.Evaluate(-1d, Ctx), Is.EqualTo(50d));
        }

        [Test]
        public void Evaluate_GrowsWithoutBound()
        {
            var model = new ExponentialModel(1d, 1.15d);

            Assert.That(model.Evaluate(200d, Ctx), Is.GreaterThan(1e11));
        }

        [Test]
        public void Describe_ReadsAsAFormula()
        {
            Assert.That(new ExponentialModel(100d, 1.15d).Describe(), Is.EqualTo("100 · 1.15^x"));
            Assert.That(new ExponentialModel(1d, 2d).Describe(), Is.EqualTo("2^x"));
        }
    }

    public sealed class TableModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void Evaluate_ReturnsExactEntriesAtWholeSteps()
        {
            var model = new TableModel(1d, 10d, 25d, 60d);

            Assert.That(model.Evaluate(1d, Ctx), Is.EqualTo(10d));
            Assert.That(model.Evaluate(2d, Ctx), Is.EqualTo(25d));
            Assert.That(model.Evaluate(3d, Ctx), Is.EqualTo(60d));
        }

        [Test]
        public void Evaluate_InterpolatesBetweenEntries()
        {
            var model = new TableModel(1d, 10d, 20d);

            Assert.That(model.Evaluate(1.5d, Ctx), Is.EqualTo(15d).Within(1e-9));
        }

        [Test]
        public void Evaluate_HoldsAtBothEndsRatherThanExtrapolating()
        {
            var model = new TableModel(1d, 10d, 25d, 60d);

            Assert.That(model.Evaluate(0d, Ctx), Is.EqualTo(10d));
            Assert.That(model.Evaluate(-100d, Ctx), Is.EqualTo(10d));
            Assert.That(model.Evaluate(4d, Ctx), Is.EqualTo(60d));
            Assert.That(model.Evaluate(999d, Ctx), Is.EqualTo(60d));
        }

        [Test]
        public void Evaluate_HandlesEmptyAndSingleEntryTables()
        {
            Assert.That(new TableModel().Evaluate(5d, Ctx), Is.EqualTo(0d));
            Assert.That(new TableModel(1d, 42d).Evaluate(999d, Ctx), Is.EqualTo(42d));
        }

        [Test]
        public void Describe_ReportsSize()
        {
            Assert.That(new TableModel().Describe(), Is.EqualTo("table (empty)"));
            Assert.That(new TableModel(1d, 5d).Describe(), Is.EqualTo("table (1 entry from x=1)"));
            Assert.That(new TableModel(3d, 5d, 6d).Describe(), Is.EqualTo("table (2 entries from x=3)"));
        }
    }

    public sealed class CurveModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        [Test]
        public void Evaluate_SamplesTheCurve()
        {
            var model = new CurveModel(AnimationCurve.Linear(0f, 0f, 10f, 100f));

            Assert.That(model.Evaluate(0d, Ctx), Is.EqualTo(0d).Within(1e-4));
            Assert.That(model.Evaluate(5d, Ctx), Is.EqualTo(50d).Within(1e-4));
            Assert.That(model.Evaluate(10d, Ctx), Is.EqualTo(100d).Within(1e-4));
        }

        [Test]
        public void Evaluate_ClampsBeyondTheLastKey()
        {
            var model = new CurveModel(AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.That(model.Evaluate(50d, Ctx), Is.EqualTo(1d).Within(1e-4));
        }

        [Test]
        public void TimeScale_MapsTheTrackRangeOntoTheCurve()
        {
            // Curve authored over 0..1, track running 0..200.
            var model = new CurveModel(AnimationCurve.Linear(0f, 0f, 1f, 1f), timeScale: 200d);

            Assert.That(model.Evaluate(100d, Ctx), Is.EqualTo(0.5d).Within(1e-4));
            Assert.That(model.Evaluate(200d, Ctx), Is.EqualTo(1d).Within(1e-4));
        }

        [Test]
        public void ValueScale_MultipliesTheSample()
        {
            var model = new CurveModel(AnimationCurve.Linear(0f, 0f, 1f, 1f), timeScale: 1d, valueScale: 250d);

            Assert.That(model.Evaluate(1d, Ctx), Is.EqualTo(250d).Within(1e-3));
        }

        [Test]
        public void Evaluate_TreatsMissingCurveAsZero()
        {
            Assert.That(new CurveModel(null).Evaluate(5d, Ctx), Is.EqualTo(0d));
            Assert.That(new CurveModel(new AnimationCurve()).Evaluate(5d, Ctx), Is.EqualTo(0d));
        }

        [Test]
        public void Describe_ReportsKeyCount()
        {
            Assert.That(new CurveModel(null).Describe(), Is.EqualTo("curve (empty)"));
            Assert.That(new CurveModel(AnimationCurve.Linear(0f, 0f, 1f, 1f)).Describe(),
                Is.EqualTo("curve (2 keys)"));
        }
    }

    public sealed class PiecewiseModelTests
    {
        private static readonly EvalContext Ctx = new EvalContext(0);

        private static PiecewiseModel ThreeSegments() => new PiecewiseModel(
            new PiecewiseSegment(1d, new LinearModel(10d)),
            new PiecewiseSegment(10d, new LinearModel(100d)),
            new PiecewiseSegment(50d, new LinearModel(1000d)));

        [Test]
        public void Evaluate_PicksTheSegmentGoverningX()
        {
            var model = ThreeSegments();

            Assert.That(model.Evaluate(5d, Ctx), Is.EqualTo(50d));
            Assert.That(model.Evaluate(20d, Ctx), Is.EqualTo(2000d));
            Assert.That(model.Evaluate(60d, Ctx), Is.EqualTo(60000d));
        }

        [Test]
        public void Evaluate_TreatsSegmentStartAsInclusive()
        {
            var model = ThreeSegments();

            Assert.That(model.Evaluate(10d, Ctx), Is.EqualTo(1000d));
            Assert.That(model.Evaluate(9.999d, Ctx), Is.EqualTo(99.99d).Within(1e-9));
        }

        [Test]
        public void Evaluate_IsIndependentOfSegmentOrder()
        {
            var shuffled = new PiecewiseModel(
                new PiecewiseSegment(50d, new LinearModel(1000d)),
                new PiecewiseSegment(1d, new LinearModel(10d)),
                new PiecewiseSegment(10d, new LinearModel(100d)));

            Assert.That(shuffled.Evaluate(20d, Ctx), Is.EqualTo(2000d));
        }

        [Test]
        public void Evaluate_FallsBackToEarliestSegmentBelowTheFirstBreakpoint()
        {
            var model = ThreeSegments();

            Assert.That(model.Evaluate(0d, Ctx), Is.EqualTo(0d));
            Assert.That(model.Evaluate(0.5d, Ctx), Is.EqualTo(5d));
        }

        [Test]
        public void Evaluate_HandlesEmptyAndNullMembers()
        {
            Assert.That(new PiecewiseModel().Evaluate(5d, Ctx), Is.EqualTo(0d));

            var withNulls = new PiecewiseModel(
                null,
                new PiecewiseSegment(1d, null),
                new PiecewiseSegment(10d, new LinearModel(2d)));

            Assert.That(withNulls.Evaluate(5d, Ctx), Is.EqualTo(0d));
            Assert.That(withNulls.Evaluate(20d, Ctx), Is.EqualTo(40d));
        }

        [Test]
        public void Nesting_Works()
        {
            var nested = new PiecewiseModel(
                new PiecewiseSegment(1d, new PiecewiseModel(
                    new PiecewiseSegment(1d, new LinearModel(1d)),
                    new PiecewiseSegment(5d, new LinearModel(2d)))));

            Assert.That(nested.Evaluate(3d, Ctx), Is.EqualTo(3d));
            Assert.That(nested.Evaluate(6d, Ctx), Is.EqualTo(12d));
        }

        [Test]
        public void Describe_ListsBreakpoints()
        {
            Assert.That(new PiecewiseModel().Describe(), Is.EqualTo("piecewise (empty)"));
            Assert.That(ThreeSegments().Describe(), Is.EqualTo("piecewise (3 segments from 1, 10, 50)"));
        }

        [Test]
        public void Describe_TruncatesLongBreakpointLists()
        {
            var many = new PiecewiseModel(
                new PiecewiseSegment(1d, new LinearModel(1d)),
                new PiecewiseSegment(2d, new LinearModel(1d)),
                new PiecewiseSegment(3d, new LinearModel(1d)),
                new PiecewiseSegment(4d, new LinearModel(1d)),
                new PiecewiseSegment(5d, new LinearModel(1d)));

            Assert.That(many.Describe(), Is.EqualTo("piecewise (5 segments from 1, 2, 3, 4, …)"));
        }
    }
}
