using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests
{
    /// <summary>
    /// The guard that stops an unbound profile from being reported on.
    /// </summary>
    public sealed class BindingGateTests
    {
        private static BalanceProfile Bound()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Axes.Add(new AxisDefinition("stage", 1, 20, "Stage"));

            foreach (var id in new[]
                     { "hp", "atk", "exp", "exp_req", "gold", "cost" })
            {
                profile.Tracks.Add(new BalanceTrack(id, "stage", 1, 20, new LinearModel(1d)));
            }

            var binding = profile.SimulationBinding;
            binding.AxisId = "stage";
            binding.MonsterHp = "hp";
            binding.MonsterAttack = "atk";
            binding.StageExp = "exp";
            binding.ExpRequired = "exp_req";
            binding.StageGold = "gold";
            binding.UpgradeCost = "cost";

            return profile;
        }

        [Test]
        public void FullyBoundProfileReportsNothingMissing()
        {
            var profile = Bound();

            Assert.That(profile.SimulationBinding.MissingBindings(profile), Is.Empty);

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ProfileThatIsNotAStageGameReportsEverySlot()
        {
            // A pity curve: a legitimate profile that will never fill these slots.
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Axes.Add(new AxisDefinition("pull", 1, 90, "Pull"));
            profile.Tracks.Add(new BalanceTrack("ssr_rate", "pull", 1, 90, new LinearModel(0d, 0.006d)));
            profile.SimulationBinding.AxisId = "pull";

            var missing = profile.SimulationBinding.MissingBindings(profile);

            Assert.That(missing, Does.Contain("Monster HP"));
            Assert.That(missing.Count, Is.EqualTo(6), "every combat and economy slot");
            Assert.That(missing, Does.Not.Contain("Axis"), "the axis itself is bound");

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void BindingPointingAtADeletedTrackCountsAsMissing()
        {
            var profile = Bound();
            profile.Tracks.RemoveAt(0);

            Assert.That(profile.SimulationBinding.MissingBindings(profile), Does.Contain("Monster HP"));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void UnknownAxisCountsAsMissing()
        {
            var profile = Bound();
            profile.SimulationBinding.AxisId = "nope";

            Assert.That(profile.SimulationBinding.MissingBindings(profile), Does.Contain("Axis"));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ExportedNumbersCarryNoBinaryArtefacts()
        {
            // Three sixths of a percent summed used to leave 0.018000000000000002 in the file.
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Axes.Add(new AxisDefinition("pull", 1, 3, "Pull"));

            var track = new BalanceTrack("rate", "pull", 1, 3, new LinearModel(0d, 0.006d));
            track.Accumulate = true;
            profile.Tracks.Add(track);

            var csv = CsvExporter.Export(profile);

            Assert.That(csv, Does.Not.Contain("0.018000000000000002"));
            Assert.That(csv, Does.Contain("0.018"));

            // And the round trip still lands on the same numbers.
            var before = TrackEvaluator.EvaluateAll(profile)["rate"];
            CsvImporter.Apply(profile, CsvImporter.Parse(csv));
            Assert.That(TrackEvaluator.EvaluateAll(profile)["rate"], Is.EqualTo(before).Within(1e-9));

            Object.DestroyImmediate(profile);
        }
    }
}
