using System.Linq;
using Fulcrum.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Fulcrum.Tests.Simulation
{
    internal static class SimFixture
    {
        /// <summary>
        /// A profile shaped like a small game: rising monsters, exp, gold, upgrades.
        /// </summary>
        /// <remarks>
        /// The defaults are tuned to be clearable by the standard preset, so a rule that
        /// fires here is reporting the change a test made rather than the fixture itself.
        /// </remarks>
        public static BalanceProfile Profile(double hpExponent = 1.05d)
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            profile.Axes.Add(new AxisDefinition("stage", 1, 100));

            profile.Tracks.Add(new BalanceTrack("monster_hp", "stage", 1, 100,
                new PowerModel(70d, hpExponent)));
            profile.Tracks.Add(new BalanceTrack("monster_atk", "stage", 1, 100,
                new PowerModel(6d, 0.9d)));
            profile.Tracks.Add(new BalanceTrack("stage_exp", "stage", 1, 100,
                new PowerModel(40d, 1.2d)));
            profile.Tracks.Add(new BalanceTrack("exp_required", "stage", 1, 100,
                new ExponentialModel(120d, 1.15d)));
            profile.Tracks.Add(new BalanceTrack("stage_gold", "stage", 1, 100,
                new PowerModel(30d, 1.1d)));
            profile.Tracks.Add(new BalanceTrack("upgrade_cost", "stage", 1, 200,
                new ExponentialModel(100d, 1.12d)));

            return profile;
        }

        public static TrackBinding Binding() => new TrackBinding
        {
            AxisId = "stage",
            MonsterHp = "monster_hp",
            MonsterAttack = "monster_atk",
            StageExp = "stage_exp",
            ExpRequired = "exp_required",
            StageGold = "stage_gold",
            UpgradeCost = "upgrade_cost",
        };
    }

    public sealed class BalanceSnapshotTests
    {
        [Test]
        public void Snapshot_CarriesEvaluatedValuesWithTheirRanges()
        {
            var profile = SimFixture.Profile();
            try
            {
                var snapshot = BalanceSnapshot.From(profile);

                Assert.That(snapshot.Contains("monster_hp"), Is.True);
                Assert.That(snapshot.TryGetRange("monster_hp", out var min, out var max), Is.True);
                Assert.That(min, Is.EqualTo(1));
                Assert.That(max, Is.EqualTo(100));

                var expected = TrackEvaluator.EvaluateProfile(profile)["monster_hp"];
                Assert.That(snapshot.Get("monster_hp", 1), Is.EqualTo(expected[0]));
                Assert.That(snapshot.Get("monster_hp", 100), Is.EqualTo(expected[99]));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ReadingPastTheEnd_ReportsTheFallback()
        {
            // A run walks until the player stalls, not until the data ends.
            var profile = SimFixture.Profile();
            try
            {
                var snapshot = BalanceSnapshot.From(profile);

                Assert.That(snapshot.TryGet("monster_hp", 500, out _), Is.False);
                Assert.That(snapshot.Get("monster_hp", 500, fallback: -1d), Is.EqualTo(-1d));
                Assert.That(snapshot.Get("no_such_track", 1, fallback: -1d), Is.EqualTo(-1d));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }

    public sealed class ProgressionSimulatorTests
    {
        private readonly ProgressionSimulator _simulator = new ProgressionSimulator();

        private ProgressionResult Run(BalanceProfile profile, PlayerProfile player, int to = 60)
        {
            var run = SimulationRun.Default(1, to);
            run.Iterations = 200;
            return _simulator.Simulate(BalanceSnapshot.From(profile), SimFixture.Binding(), player, run);
        }

        [Test]
        public void APlaythroughProducesLevelsExperienceAndTime()
        {
            var profile = SimFixture.Profile();
            try
            {
                var result = Run(profile, PlayerProfile.Standard());

                Assert.That(result.LevelAtStage[0], Is.EqualTo(1));
                Assert.That(result.LevelAtStage.Last(), Is.GreaterThan(1), "Experience should produce levels.");
                Assert.That(result.CumulativePlayTimeSec.Last(), Is.GreaterThan(0d));
                Assert.That(result.TotalHours, Is.GreaterThan(0d));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LevelsAndTimeOnlyEverIncrease()
        {
            var profile = SimFixture.Profile();
            try
            {
                var result = Run(profile, PlayerProfile.Standard());

                for (var i = 1; i < result.LevelAtStage.Length; i++)
                {
                    Assert.That(result.LevelAtStage[i], Is.GreaterThanOrEqualTo(result.LevelAtStage[i - 1]));
                    Assert.That(result.CumulativePlayTimeSec[i],
                        Is.GreaterThanOrEqualTo(result.CumulativePlayTimeSec[i - 1]));
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TheSameSeed_ReproducesThePlaythrough()
        {
            var profile = SimFixture.Profile();
            try
            {
                var a = Run(profile, PlayerProfile.Standard());
                var b = Run(profile, PlayerProfile.Standard());

                Assert.That(b.TotalHours, Is.EqualTo(a.TotalHours));
                Assert.That(b.LevelAtStage, Is.EqualTo(a.LevelAtStage));
                Assert.That(b.FirstWallStage, Is.EqualTo(a.FirstWallStage));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ASteeperCurve_MovesTheWallEarlier()
        {
            // The point of the whole simulator: a parameter change becomes a statement
            // about where players get stuck.
            var gentle = SimFixture.Profile(1.05d);
            var steep = SimFixture.Profile(1.5d);
            try
            {
                var a = Run(gentle, PlayerProfile.Standard(), 80);
                var b = Run(steep, PlayerProfile.Standard(), 80);

                Assert.That(b.ReachedStage, Is.LessThanOrEqualTo(a.ReachedStage));
                Assert.That(b.WinRateAtStage.Last(), Is.LessThanOrEqualTo(a.WinRateAtStage.Last()));
            }
            finally
            {
                Object.DestroyImmediate(gentle);
                Object.DestroyImmediate(steep);
            }
        }

        [Test]
        public void WeakerPlayersStallNoLaterThanStrongerOnes()
        {
            // Three curves overlaid is how "which layer hits the wall" becomes visible.
            var profile = SimFixture.Profile(1.4d);
            try
            {
                var casual = Run(profile, PlayerProfile.Casual(), 80);
                var standard = Run(profile, PlayerProfile.Standard(), 80);
                var optimizer = Run(profile, PlayerProfile.Optimizer(), 80);

                Assert.That(casual.ReachedStage, Is.LessThanOrEqualTo(standard.ReachedStage));
                Assert.That(standard.ReachedStage, Is.LessThanOrEqualTo(optimizer.ReachedStage));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RetriesAreCountedIntoPlaytime()
        {
            // A stage won one time in five costs five times as long, which is exactly what
            // a playtime estimate exists to expose.
            var profile = SimFixture.Profile(1.35d);
            try
            {
                var patient = PlayerProfile.Standard();
                patient.MaxAttempts = 10;

                var impatient = PlayerProfile.Standard();
                impatient.MaxAttempts = 1;

                Assert.That(Run(profile, patient, 70).TotalHours,
                    Is.GreaterThan(Run(profile, impatient, 70).TotalHours));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AnUnwinnableStage_EndsTheRun()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("monster_hp", "stage", 1, 10,
                    new PiecewiseModel(
                        new PiecewiseSegment(1d, new LinearModel(0d, 100d)),
                        new PiecewiseSegment(5d, new LinearModel(0d, 1e12d)))));
                profile.Tracks.Add(new BalanceTrack("monster_atk", "stage", 1, 10,
                    new LinearModel(0d, 10000d)));

                var run = SimulationRun.Default(1, 10);
                run.Iterations = 20;
                var result = _simulator.Simulate(
                    BalanceSnapshot.From(profile), SimFixture.Binding(), PlayerProfile.Standard(), run);

                Assert.That(result.ReachedStage, Is.LessThan(10));
                Assert.That(result.FirstWallStage, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AnEmptyRange_ProducesNothing()
        {
            var result = _simulator.Simulate(null, null, PlayerProfile.Standard(), SimulationRun.Default(5, 1));

            Assert.That(result.LevelAtStage, Is.Null);
            Assert.That(result.TotalHours, Is.EqualTo(0d));
        }
    }

    public sealed class EconomySimulatorTests
    {
        private readonly EconomySimulator _simulator = new EconomySimulator();

        private EconomyResult Run(BalanceProfile profile, PlayerProfile player, int to = 60)
            => _simulator.Simulate(
                BalanceSnapshot.From(profile), SimFixture.Binding(), player, SimulationRun.Default(1, to));

        [Test]
        public void IncomeIsSpentAndTheBalanceNeverGoesNegative()
        {
            var profile = SimFixture.Profile();
            try
            {
                var result = Run(profile, PlayerProfile.Standard());

                Assert.That(result.IncomePerStage.Sum(), Is.GreaterThan(0d));
                Assert.That(result.SpendPerStage.Sum(), Is.GreaterThan(0d));
                Assert.That(result.NetBalance.All(v => v >= 0d), Is.True);
                Assert.That(result.UpgradesAtStage.Last(), Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SpendingLessLeavesMoreAndBuysFewerUpgrades()
        {
            var profile = SimFixture.Profile();
            try
            {
                var thrifty = Run(profile, PlayerProfile.Casual());
                var spender = Run(profile, PlayerProfile.Standard());

                Assert.That(thrifty.UpgradesAtStage.Last(),
                    Is.LessThanOrEqualTo(spender.UpgradesAtStage.Last()));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void NothingToBuy_ReadsAsInflation()
        {
            // Income with no sink is a currency that stopped being a decision.
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("stage_gold", "stage", 1, 40, new LinearModel(0d, 100d)));

                var result = Run(profile, PlayerProfile.Standard(), 40);

                Assert.That(result.InflationStartStage, Is.EqualTo(1));
                Assert.That(result.FinalBalance, Is.EqualTo(4000d));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void UpgradesOutrunningIncome_ReadsAsDeflation()
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfile>();
            try
            {
                profile.Tracks.Add(new BalanceTrack("stage_gold", "stage", 1, 40, new LinearModel(0d, 100d)));
                profile.Tracks.Add(new BalanceTrack("upgrade_cost", "stage", 1, 40,
                    new ExponentialModel(100d, 2d)));

                var result = Run(profile, PlayerProfile.Standard(), 40);

                Assert.That(result.DeflationStartStage, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ABalancedEconomy_FlagsNeither()
        {
            var profile = SimFixture.Profile();
            try
            {
                var result = Run(profile, PlayerProfile.Standard());

                Assert.That(result.InflationStartStage, Is.EqualTo(-1));
                Assert.That(result.DeflationStartStage, Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }

    public sealed class PlayerProfileTests
    {
        [Test]
        public void PresetsAreOrderedWeakestFirst()
        {
            var presets = PlayerProfile.Presets();

            Assert.That(presets.Length, Is.EqualTo(3));
            Assert.That(presets[0].SkillMultiplier, Is.LessThan(presets[1].SkillMultiplier));
            Assert.That(presets[1].SkillMultiplier, Is.LessThan(presets[2].SkillMultiplier));
            Assert.That(presets[0].SpendRatio, Is.LessThan(presets[1].SpendRatio));
        }

        [Test]
        public void StatsGrowWithLevel()
        {
            var player = PlayerProfile.Standard();

            Assert.That(player.HpAtLevel(1), Is.EqualTo(player.BaseHp));
            Assert.That(player.HpAtLevel(10), Is.GreaterThan(player.HpAtLevel(1)));
            Assert.That(player.AttackAtLevel(10), Is.GreaterThan(player.AttackAtLevel(1)));
        }
    }
}
