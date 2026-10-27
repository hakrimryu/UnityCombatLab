using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fulcrum.Tests.PostProcessors
{
    internal sealed class ChainHost : ScriptableObject
    {
        [SerializeField] private BalanceTrack _track = new BalanceTrack();

        public BalanceTrack Track
        {
            get => _track;
            set => _track = value;
        }
    }

    public sealed class PostProcessorSerializationTests
    {
        private static PostProcessor[] AllProcessors() => new PostProcessor[]
        {
            new RoundProcessor(RoundingMode.SignificantDigits, 3),
            ClampProcessor.Between(0d, 1d),
            new MinDeltaProcessor(5d),
            new MultiplierProcessor(1.3d, "difficulty"),
            new SmoothProcessor(5),
        };

        [Test]
        public void EveryProcessor_SurvivesARoundTripWithItsValues()
        {
            var source = ScriptableObject.CreateInstance<ChainHost>();
            var restored = ScriptableObject.CreateInstance<ChainHost>();
            try
            {
                source.Track = new BalanceTrack("monster_hp", new LinearModel(100d));
                source.Track.PostProcessors.AddRange(AllProcessors());

                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);

                var chain = restored.Track.PostProcessors;
                Assert.That(chain.Count, Is.EqualTo(source.Track.PostProcessors.Count));

                for (var i = 0; i < chain.Count; i++)
                {
                    var expected = source.Track.PostProcessors[i];
                    Assert.That(chain[i], Is.Not.Null, $"Step {i} lost its reference.");
                    Assert.That(chain[i].GetType(), Is.EqualTo(expected.GetType()));

                    // Describe folds every serialised parameter into one string, so a
                    // field that failed to persist shows up here.
                    Assert.That(chain[i].Describe(), Is.EqualTo(expected.Describe()),
                        $"{expected.GetType().Name} lost parameter values.");
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void ChainOrder_IsPreservedAcrossARoundTrip()
        {
            var source = ScriptableObject.CreateInstance<ChainHost>();
            var restored = ScriptableObject.CreateInstance<ChainHost>();
            try
            {
                source.Track = new BalanceTrack("gold", new LinearModel(1d));
                source.Track.PostProcessors.Add(new MultiplierProcessor(3d));
                source.Track.PostProcessors.Add(ClampProcessor.AtMost(25d));

                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);

                // Order is configuration, so a round trip that shuffles it changes results.
                var values = new[] { 10d };
                PostProcessorChain.Apply(restored.Track.PostProcessors, values, new EvalContext(0));

                Assert.That(values[0], Is.EqualTo(25d));
                Assert.That(PostProcessorChain.Describe(restored.Track.PostProcessors),
                    Is.EqualTo("× 3 → clamp ≤ 25"));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(restored);
            }
        }
    }
}
