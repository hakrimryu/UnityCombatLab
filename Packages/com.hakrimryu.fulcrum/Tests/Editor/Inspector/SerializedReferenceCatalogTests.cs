using System.Linq;
using Fulcrum.Editor.Inspector;
using NUnit.Framework;

namespace Fulcrum.Tests.Inspector
{
    /// <summary>
    /// The dropdown is built from type discovery rather than a fixed list, which is what
    /// makes the documented extension points work from a user's own project.
    /// </summary>
    public sealed class SerializedReferenceCatalogTests
    {
        [Test]
        public void Models_IncludeEveryShippedType()
        {
            var types = SerializedReferenceCatalog.Implementations<GrowthModel>();

            Assert.That(types, Contains.Item(typeof(LinearModel)));
            Assert.That(types, Contains.Item(typeof(PowerModel)));
            Assert.That(types, Contains.Item(typeof(ExponentialModel)));
            Assert.That(types, Contains.Item(typeof(TableModel)));
            Assert.That(types, Contains.Item(typeof(CurveModel)));
            Assert.That(types, Contains.Item(typeof(PiecewiseModel)));
        }

        [Test]
        public void Processors_IncludeEveryShippedType()
        {
            var types = SerializedReferenceCatalog.Implementations<PostProcessor>();

            Assert.That(types, Contains.Item(typeof(RoundProcessor)));
            Assert.That(types, Contains.Item(typeof(ClampProcessor)));
            Assert.That(types, Contains.Item(typeof(MinDeltaProcessor)));
            Assert.That(types, Contains.Item(typeof(MultiplierProcessor)));
            Assert.That(types, Contains.Item(typeof(SmoothProcessor)));
        }

        [Test]
        public void AbstractTypes_AreExcluded()
        {
            var types = SerializedReferenceCatalog.Implementations<PostProcessor>();

            Assert.That(types.Contains(typeof(PointwisePostProcessor)), Is.False);
            Assert.That(types.All(t => !t.IsAbstract), Is.True);
        }

        [Test]
        public void EveryOfferedType_CanBeConstructed()
        {
            // The dropdown creates an instance when picked, so an entry it cannot
            // construct would be an option that throws.
            foreach (var type in SerializedReferenceCatalog.Implementations<GrowthModel>())
            {
                Assert.That(System.Activator.CreateInstance(type), Is.InstanceOf<GrowthModel>());
            }

            foreach (var type in SerializedReferenceCatalog.Implementations<PostProcessor>())
            {
                Assert.That(System.Activator.CreateInstance(type), Is.InstanceOf<PostProcessor>());
            }
        }

        [Test]
        public void DisplayName_PrefersTheDeclaredLabel()
        {
            // The class name assumes the reader already knows what a power curve does.
            Assert.That(SerializedReferenceCatalog.DisplayName(typeof(PowerModel), "Model"),
                Is.EqualTo("Gets steeper"));
            Assert.That(SerializedReferenceCatalog.DisplayName(typeof(MinDeltaProcessor), "Processor"),
                Is.EqualTo("Always increase"));
            Assert.That(SerializedReferenceCatalog.DisplayName(null, "Model"), Is.EqualTo("(none)"));
        }

        [Test]
        public void DisplayName_FallsBackToTheClassName()
        {
            // A model written elsewhere without the attribute still gets a readable label.
            Assert.That(SerializedReferenceCatalog.DisplayName(typeof(Fulcrum.Tests.Models.ProbeModel), "Model"),
                Is.EqualTo("Probe"));
            Assert.That(SerializedReferenceCatalog.DisplayName(typeof(Fulcrum.Tests.Models.OffsetProbeModel), "Model"),
                Is.EqualTo("Offset Probe"));
        }

        [Test]
        public void EveryShippedType_ExplainsItself()
        {
            // The label alone is not enough; the dropdown shows a summary beside it.
            foreach (var type in SerializedReferenceCatalog.Implementations<GrowthModel>())
            {
                if (type.Assembly != typeof(GrowthModel).Assembly) continue;
                Assert.That(SerializedReferenceCatalog.Summary(type), Is.Not.Empty, type.Name);
            }

            foreach (var type in SerializedReferenceCatalog.Implementations<PostProcessor>())
            {
                if (type.Assembly != typeof(PostProcessor).Assembly) continue;
                Assert.That(SerializedReferenceCatalog.Summary(type), Is.Not.Empty, type.Name);
            }
        }
    }
}
