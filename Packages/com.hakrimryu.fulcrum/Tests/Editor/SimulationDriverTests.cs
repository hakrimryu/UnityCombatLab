using System;
using System.Reflection;
using System.Threading;
using Fulcrum.Editor.Windows;
using Fulcrum.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulcrum.Tests
{
    /// <summary>Checks cancellation at request, profile and window lifetime boundaries.</summary>
    public sealed class SimulationDriverTests
    {
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        // An existing worker's token lets the test exercise replacement during debounce
        // without relying on how fast a background simulation happens to finish.
        private static CancellationToken AddRunningRequest(SimulationDriver driver)
        {
            var cancellation = new CancellationTokenSource();
            typeof(SimulationDriver).GetField("_cancellation", PrivateInstance).SetValue(driver, cancellation);
            return cancellation.Token;
        }

        [Test]
        public void ReplacementCancelsThePreviousRequestBeforeDebounceEnds()
        {
            using (var driver = new SimulationDriver(new VisualElement()))
            {
                var previous = AddRunningRequest(driver);
                driver.Request(new BalanceSnapshot(), new TrackBinding(), SimulationRun.Default(1, 1));
                Assert.That(previous.IsCancellationRequested, Is.True);
                Assert.That(driver.IsRunning, Is.False);
            }
        }

        [Test]
        public void DisposeCancelsAndPreventsAQueuedRequestFromStartingAgain()
        {
            var driver = new SimulationDriver(new VisualElement());
            var previous = AddRunningRequest(driver);
            driver.Dispose();
            Assert.That(previous.IsCancellationRequested, Is.True);
            Assert.DoesNotThrow(driver.Dispose);
            Assert.Throws<ObjectDisposedException>(() => driver.Request(new BalanceSnapshot(), new TrackBinding(), SimulationRun.Default(1, 1)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullOrUnconfiguredProfileCancelsThePreviousAnalysis(bool unconfigured)
        {
            var window = ScriptableObject.CreateInstance<FulcrumWindow>();
            var profile = unconfigured ? ScriptableObject.CreateInstance<BalanceProfile>() : null;
            using (var driver = new SimulationDriver(new VisualElement()))
            {
                try
                {
                    typeof(FulcrumWindow).GetField("_driver", PrivateInstance).SetValue(window, driver);
                    var previous = AddRunningRequest(driver);
                    typeof(FulcrumWindow).GetField("_profile", PrivateInstance).SetValue(window, profile);
                    typeof(FulcrumWindow).GetMethod("RequestSimulation", PrivateInstance).Invoke(window, null);
                    Assert.That(previous.IsCancellationRequested, Is.True);
                    Assert.That(driver.IsRunning, Is.False);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(window);
                    if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
                }
            }
        }
    }
}
