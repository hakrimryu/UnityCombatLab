using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Fulcrum.Tests.Models
{
    /// <summary>
    /// Every shipped model has to survive a serialisation round trip, and every one of them
    /// has to carry <c>[MovedFrom]</c>. Both are checked here rather than left to review,
    /// because a model added later would otherwise pass unnoticed until an asset broke.
    /// </summary>
    public sealed class ConcreteModelSerializationTests
    {
        private static GrowthModel[] AllModels() => new GrowthModel[]
        {
            new LinearModel(2d, 5d),
            new PowerModel(100d, 1.35d, 10d),
            new ExponentialModel(50d, 1.15d),
            new TableModel(1d, 10d, 25d, 60d),
            new CurveModel(AnimationCurve.Linear(0f, 0f, 10f, 100f), 2d, 3d),
            new PiecewiseModel(
                new PiecewiseSegment(1d, new LinearModel(10d)),
                new PiecewiseSegment(10d, new PowerModel(2d, 2d))),
        };

        private static TrackHost RoundTrip(GrowthModel model, out TrackHost source)
        {
            source = ScriptableObject.CreateInstance<TrackHost>();
            source.Track = new BalanceTrack("probe", model);

            var restored = ScriptableObject.CreateInstance<TrackHost>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);
            return restored;
        }

        [Test]
        public void EveryModel_SurvivesARoundTripWithItsValues()
        {
            foreach (var model in AllModels())
            {
                var restored = RoundTrip(model, out var source);
                try
                {
                    Assert.That(restored.Track.Model, Is.Not.Null,
                        $"{model.GetType().Name} lost its reference.");
                    Assert.That(restored.Track.Model.GetType(), Is.EqualTo(model.GetType()));

                    // Describe() folds every serialised parameter into one string, so
                    // comparing it catches a field that failed to persist.
                    Assert.That(restored.Track.Model.Describe(), Is.EqualTo(model.Describe()),
                        $"{model.GetType().Name} lost parameter values.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                    UnityEngine.Object.DestroyImmediate(restored);
                }
            }
        }

        [Test]
        public void NestedPiecewiseModels_SurviveARoundTrip()
        {
            var nested = new PiecewiseModel(
                new PiecewiseSegment(1d, new PiecewiseModel(
                    new PiecewiseSegment(1d, new LinearModel(3d)),
                    new PiecewiseSegment(5d, new ExponentialModel(2d, 2d)))));

            var restored = RoundTrip(nested, out var source);
            try
            {
                var outer = restored.Track.Model as PiecewiseModel;
                Assert.That(outer, Is.Not.Null);

                var inner = outer.Segments[0].Model as PiecewiseModel;
                Assert.That(inner, Is.Not.Null, "The nested reference was lost.");
                Assert.That(inner.Segments.Count, Is.EqualTo(2));
                Assert.That(inner.Segments[1].Model, Is.TypeOf<ExponentialModel>());

                var ctx = new EvalContext(0);
                Assert.That(restored.Track.Model.Evaluate(3d, ctx), Is.EqualTo(nested.Evaluate(3d, ctx)));
                Assert.That(restored.Track.Model.Evaluate(6d, ctx), Is.EqualTo(nested.Evaluate(6d, ctx)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void EverySerializedType_CarriesMovedFrom()
        {
            // The attribute has to be present from the day a type ships. Adding it after a
            // rename is too late, so this fails the moment a new model forgets it.
            var types = typeof(GrowthModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => typeof(GrowthModel).IsAssignableFrom(t)
                         || typeof(PostProcessor).IsAssignableFrom(t)
                         || t == typeof(PiecewiseSegment))
                .ToArray();

            Assert.That(types, Is.Not.Empty, "No serialized types were discovered.");

            var missing = types
                .Where(t => Attribute.GetCustomAttribute(t, typeof(MovedFromAttribute)) == null)
                .Select(t => t.Name)
                .ToArray();

            Assert.That(missing, Is.Empty,
                "These serialized types are missing [MovedFrom]: " + string.Join(", ", missing));
        }

        [Test]
        public void MovedFrom_RecordsTheCurrentLocation()
        {
            // Pre-filling the attribute with where the type lives today is what makes a
            // future move recoverable — the old coordinates are already written down.
            //
            // Unity keeps those coordinates in a non-public field, so this reads them by
            // reflection. If a future Unity reshapes that field the test fails with a clear
            // message rather than a null reference, and only this assertion needs revisiting.
            var attribute = (MovedFromAttribute)Attribute.GetCustomAttribute(
                typeof(PowerModel), typeof(MovedFromAttribute));
            Assert.That(attribute, Is.Not.Null);

            var dataField = typeof(MovedFromAttribute).GetField(
                "data",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(dataField, Is.Not.Null,
                "MovedFromAttribute no longer exposes its data field under that name.");

            var data = dataField.GetValue(attribute);
            Assert.That(data, Is.Not.Null);

            var type = data.GetType();
            Assert.That(type.GetField("nameSpace").GetValue(data), Is.EqualTo("Fulcrum"));
            Assert.That(type.GetField("assembly").GetValue(data), Is.EqualTo("Fulcrum.Core"));
            Assert.That(type.GetField("className").GetValue(data), Is.EqualTo(nameof(PowerModel)));
        }
    }
}
