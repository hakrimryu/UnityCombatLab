using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Fulcrum.Tests
{
    /// <summary>
    /// Guards the assembly boundaries described in the architecture notes.
    ///
    /// The primary enforcement is the asmdef files themselves: Fulcrum.Core lists no
    /// Fulcrum references, and Fulcrum.Simulation sets noEngineReferences. These tests
    /// are a second net that fails loudly if someone widens an asmdef to unblock a
    /// compile error, which is the usual way a layering rule quietly dies.
    ///
    /// Note that GetReferencedAssemblies only reports assemblies actually used, so a
    /// violation is caught once offending code exists, not merely once a reference is
    /// added. That is the case we care about.
    /// </summary>
    public sealed class AssemblyBoundaryTests
    {
        private const string Core = "Fulcrum.Core";
        private const string Simulation = "Fulcrum.Simulation";
        private const string Editor = "Fulcrum.Editor";

        [Test]
        public void AllAssemblies_AreLoadable()
        {
            Assert.That(Load(Core), Is.Not.Null);
            Assert.That(Load(Simulation), Is.Not.Null);
            Assert.That(Load(Editor), Is.Not.Null);
        }

        [Test]
        public void Core_DoesNotDependOnSimulationOrEditor()
        {
            var refs = ReferenceNames(Core);
            Assert.That(refs, Does.Not.Contain(Simulation), "Core must not depend on Simulation.");
            Assert.That(refs, Does.Not.Contain(Editor), "Core must not depend on Editor.");
        }

        [Test]
        public void Simulation_DoesNotDependOnEditor()
        {
            Assert.That(ReferenceNames(Simulation), Does.Not.Contain(Editor),
                "Simulation must stay runnable outside the editor.");
        }

        [Test]
        public void Simulation_DoesNotDependOnUnityEngine()
        {
            var engineRefs = ReferenceNames(Simulation)
                .Where(n => n.StartsWith("UnityEngine", StringComparison.Ordinal)
                         || n.StartsWith("UnityEditor", StringComparison.Ordinal))
                .ToArray();

            Assert.That(engineRefs, Is.Empty,
                "Simulation is plain C# so it stays fast to test and portable. "
                + "Found: " + string.Join(", ", engineRefs));
        }

        [Test]
        public void Core_DoesNotDependOnUnityEditor()
        {
            var editorRefs = ReferenceNames(Core)
                .Where(n => n.StartsWith("UnityEditor", StringComparison.Ordinal))
                .ToArray();

            Assert.That(editorRefs, Is.Empty,
                "Core ships at runtime. Found: " + string.Join(", ", editorRefs));
        }

        private static Assembly Load(string name)
        {
            var found = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            Assert.That(found, Is.Not.Null, $"Assembly '{name}' was not compiled.");
            return found;
        }

        private static string[] ReferenceNames(string name)
        {
            return Load(name).GetReferencedAssemblies().Select(a => a.Name).ToArray();
        }
    }
}
