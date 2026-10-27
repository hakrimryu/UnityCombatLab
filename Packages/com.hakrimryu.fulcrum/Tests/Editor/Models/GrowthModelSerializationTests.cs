using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fulcrum.Tests.Models
{
    /// <summary>
    /// A stand-in concrete model. Keeping it in the test assembly means these tests
    /// describe the polymorphic reference mechanism itself, not any shipped formula.
    /// </summary>
    [Serializable]
    internal sealed class ProbeModel : GrowthModel
    {
        [SerializeField] private double _scale = 1d;

        public ProbeModel() { }

        public ProbeModel(double scale) => _scale = scale;

        public double Scale => _scale;

        public override double Evaluate(double x, EvalContext context) => x * _scale;

        public override string Describe() => $"{_scale} · x";
    }

    /// <summary>A second type, so a round trip has to preserve which one was stored.</summary>
    [Serializable]
    internal sealed class OffsetProbeModel : GrowthModel
    {
        [SerializeField] private double _offset;

        public OffsetProbeModel() { }

        public OffsetProbeModel(double offset) => _offset = offset;

        public double Offset => _offset;

        public override double Evaluate(double x, EvalContext context) => x + _offset;

        public override string Describe() => $"x + {_offset}";
    }

    internal sealed class TrackHost : ScriptableObject
    {
        [SerializeField] private BalanceTrack _track = new BalanceTrack();

        public BalanceTrack Track
        {
            get => _track;
            set => _track = value;
        }
    }

    public sealed class GrowthModelSerializationTests
    {
        [Test]
        public void SerializeReference_PreservesConcreteTypeAndValues()
        {
            var source = ScriptableObject.CreateInstance<TrackHost>();
            var restored = ScriptableObject.CreateInstance<TrackHost>();
            try
            {
                source.Track = new BalanceTrack("monster_hp", new ProbeModel(2.5d));

                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);

                Assert.That(restored.Track.Model, Is.Not.Null,
                    "The polymorphic reference was lost across a round trip.");
                Assert.That(restored.Track.Model, Is.TypeOf<ProbeModel>());
                Assert.That(((ProbeModel)restored.Track.Model).Scale, Is.EqualTo(2.5d));
                Assert.That(restored.Track.Id, Is.EqualTo("monster_hp"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void SerializeReference_DistinguishesBetweenSubclasses()
        {
            var source = ScriptableObject.CreateInstance<TrackHost>();
            var restored = ScriptableObject.CreateInstance<TrackHost>();
            try
            {
                source.Track = new BalanceTrack("gold", new OffsetProbeModel(7d));

                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);

                Assert.That(restored.Track.Model, Is.TypeOf<OffsetProbeModel>());
                Assert.That(((OffsetProbeModel)restored.Track.Model).Offset, Is.EqualTo(7d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void SerializedJson_RecordsTheConcreteTypeName()
        {
            // The recorded type name is exactly what a rename would invalidate, so assert
            // it is really written out. This is the failure mode [MovedFrom] guards.
            var host = ScriptableObject.CreateInstance<TrackHost>();
            try
            {
                host.Track = new BalanceTrack("exp", new ProbeModel(1d));

                var json = EditorJsonUtility.ToJson(host);

                Assert.That(json, Does.Contain(nameof(ProbeModel)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MissingModel_IsReportedRatherThanAssumed()
        {
            var track = new BalanceTrack("unset");

            Assert.That(track.HasModel, Is.False);
            Assert.That(track.Model, Is.Null);
        }

        [Test]
        public void DisplayName_FallsBackToId()
        {
            var track = new BalanceTrack("monster_atk");

            Assert.That(track.DisplayName, Is.EqualTo("monster_atk"));

            track.DisplayName = "Monster ATK";
            Assert.That(track.DisplayName, Is.EqualTo("Monster ATK"));
        }
    }

    public sealed class EvalContextTests
    {
        [Test]
        public void Globals_DefaultToEmptyRatherThanNull()
        {
            var context = new EvalContext(0);

            Assert.That(context.Globals, Is.Not.Null);
            Assert.That(context.Globals.Count, Is.Zero);
        }

        [Test]
        public void GetGlobal_UsesFallbackWhenMissing()
        {
            var context = new EvalContext(3, new Dictionary<string, double> { ["difficulty"] = 1.2d });

            Assert.That(context.GetGlobal("difficulty"), Is.EqualTo(1.2d));
            Assert.That(context.GetGlobal("absent", 42d), Is.EqualTo(42d));
            Assert.That(context.GetGlobal(null, 9d), Is.EqualTo(9d));
        }

        [Test]
        public void AtIndex_KeepsGlobals()
        {
            var context = new EvalContext(0, new Dictionary<string, double> { ["k"] = 5d });

            var moved = context.AtIndex(7);

            Assert.That(moved.CurrentIndex, Is.EqualTo(7));
            Assert.That(moved.GetGlobal("k"), Is.EqualTo(5d));
        }

        [Test]
        public void Model_EvaluatesThroughContext()
        {
            GrowthModel model = new ProbeModel(3d);

            Assert.That(model.Evaluate(4d, new EvalContext(0)), Is.EqualTo(12d));
            Assert.That(model.Describe(), Is.EqualTo("3 · x"));
        }
    }
}
