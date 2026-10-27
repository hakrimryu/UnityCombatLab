using System.Diagnostics;
using Fulcrum.Simulation;
using NUnit.Framework;

namespace Fulcrum.Tests.Simulation
{
    public sealed class CombatSimulatorTests
    {
        private readonly CombatSimulator _simulator = new CombatSimulator();

        private static CombatSetup Even()
            => CombatSetup.Default(Combatant.Basic(1000d, 100d, 0d), Combatant.Basic(1000d, 100d, 0d));

        [Test]
        public void TheSameSeed_ReproducesTheSameResult()
        {
            // A win rate that moves between runs of an unchanged profile is not a
            // measurement, and hides whether an edit or the dice caused a difference.
            var setup = Even();
            setup.Attacker.CritChance = 0.25d;
            setup.Defender.DodgeChance = 0.2d;

            var a = _simulator.Simulate(setup, 500, 12345);
            var b = _simulator.Simulate(setup, 500, 12345);

            Assert.That(b.WinRate, Is.EqualTo(a.WinRate));
            Assert.That(b.AverageTurns, Is.EqualTo(a.AverageTurns));
            Assert.That(b.TurnsStdDev, Is.EqualTo(a.TurnsStdDev));
            Assert.That(b.P95Turns, Is.EqualTo(a.P95Turns));
            Assert.That(b.AverageRemainingHpRatio, Is.EqualTo(a.AverageRemainingHpRatio));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentDraws()
        {
            var setup = Even();
            setup.Attacker.CritChance = 0.5d;

            var a = _simulator.Simulate(setup, 500, 1);
            var b = _simulator.Simulate(setup, 500, 2);

            Assert.That(b.AverageTurns, Is.Not.EqualTo(a.AverageTurns));
        }

        [Test]
        public void WithoutRandomness_TurnCountIsExact()
        {
            // 1000 HP against 100 damage per turn is ten turns, every time.
            var setup = CombatSetup.Default(
                Combatant.Basic(10000d, 100d, 0d),
                Combatant.Basic(1000d, 1d, 0d));

            var result = _simulator.Simulate(setup, 10, 1);

            Assert.That(result.WinRate, Is.EqualTo(1d));
            Assert.That(result.AverageTurns, Is.EqualTo(10d));
            Assert.That(result.TurnsStdDev, Is.EqualTo(0d));
            Assert.That(result.P05Turns, Is.EqualTo(10d));
            Assert.That(result.P95Turns, Is.EqualTo(10d));
        }

        [Test]
        public void DefenseFollowsTheStatedFormula()
        {
            var setup = CombatSetup.Default(Combatant.Basic(1d, 100d, 0d), Combatant.Basic(1d, 0d, 100d));
            setup.DefenseConstant = 100d;

            // 100 · (1 − 100 / 200) = 50
            Assert.That(setup.BaseDamage(100d, 100d), Is.EqualTo(50d));
            Assert.That(setup.BaseDamage(100d, 0d), Is.EqualTo(100d));
            Assert.That(setup.BaseDamage(100d, 300d), Is.EqualTo(25d));
        }

        [Test]
        public void DefenseHasDiminishingReturnsAndNeverReachesZeroDamage()
        {
            var setup = CombatSetup.Default(Combatant.Basic(1d, 1d, 0d), Combatant.Basic(1d, 1d, 0d));

            var atHundred = setup.BaseDamage(100d, 100d);
            var atThousand = setup.BaseDamage(100d, 1000d);
            var atMillion = setup.BaseDamage(100d, 1000000d);

            Assert.That(atThousand, Is.LessThan(atHundred));
            Assert.That(atMillion, Is.GreaterThan(0d), "A fight must stay winnable, only slower.");
        }

        [Test]
        public void ADefenderThatCannotBeKilled_CountsAsAStalemateLoss()
        {
            // A fight the player cannot finish is a wall, not a draw.
            var setup = CombatSetup.Default(
                Combatant.Basic(100d, 0d, 0d),
                Combatant.Basic(1000d, 0d, 0d));
            setup.TurnLimit = 50;

            var result = _simulator.Simulate(setup, 10, 1);

            Assert.That(result.WinRate, Is.EqualTo(0d));
            Assert.That(result.Stalemates, Is.EqualTo(10));
        }

        [Test]
        public void AKillOnTheFirstStrike_CostsNoHealth()
        {
            var setup = CombatSetup.Default(
                Combatant.Basic(100d, 9999d, 0d),
                Combatant.Basic(10d, 500d, 0d));

            var result = _simulator.Simulate(setup, 5, 1);

            Assert.That(result.AverageTurns, Is.EqualTo(1d));
            Assert.That(result.AverageRemainingHpRatio, Is.EqualTo(1d));
            Assert.That(result.AverageHitsTaken, Is.EqualTo(0d));
        }

        [Test]
        public void RemainingHealthShowsHowCloseTheFightWas()
        {
            var comfortable = CombatSetup.Default(
                Combatant.Basic(1000d, 500d, 0d), Combatant.Basic(1000d, 10d, 0d));
            var narrow = CombatSetup.Default(
                Combatant.Basic(1000d, 500d, 0d), Combatant.Basic(1000d, 400d, 0d));

            var a = _simulator.Simulate(comfortable, 20, 1);
            var b = _simulator.Simulate(narrow, 20, 1);

            Assert.That(a.WinRate, Is.EqualTo(1d));
            Assert.That(b.WinRate, Is.EqualTo(1d));
            Assert.That(b.AverageRemainingHpRatio, Is.LessThan(a.AverageRemainingHpRatio),
                "Two identical win rates should still be distinguishable.");
        }

        [Test]
        public void CritsShortenFightsAndDodgesLengthenThem()
        {
            // The attacker is comfortably ahead so that both effects change the length of
            // the fight without flipping who wins, which would make turn counts
            // incomparable.
            CombatSetup Favoured() => CombatSetup.Default(
                Combatant.Basic(5000d, 100d, 0d),
                Combatant.Basic(1000d, 20d, 0d));

            var critical = Favoured();
            critical.Attacker.CritChance = 0.5d;
            critical.Attacker.CritMultiplier = 3d;

            var evasive = Favoured();
            evasive.Defender.DodgeChance = 0.5d;

            var baseline = _simulator.Simulate(Favoured(), 300, 7);
            var withCrits = _simulator.Simulate(critical, 300, 7);
            var withDodges = _simulator.Simulate(evasive, 300, 7);

            Assert.That(baseline.WinRate, Is.EqualTo(1d));
            Assert.That(withCrits.WinRate, Is.EqualTo(1d));
            Assert.That(withDodges.WinRate, Is.EqualTo(1d));

            Assert.That(withCrits.AverageTurns, Is.LessThan(baseline.AverageTurns));
            Assert.That(withDodges.AverageTurns, Is.GreaterThan(baseline.AverageTurns));
        }

        [Test]
        public void PercentilesBracketTheAverage()
        {
            var setup = Even();
            setup.Attacker.CritChance = 0.3d;

            var result = _simulator.Simulate(setup, 1000, 99);

            Assert.That(result.P05Turns, Is.LessThanOrEqualTo(result.P50Turns));
            Assert.That(result.P50Turns, Is.LessThanOrEqualTo(result.P95Turns));
            Assert.That(result.AverageTurns, Is.InRange(result.P05Turns, result.P95Turns));
        }

        [Test]
        public void ZeroIterations_ProducesAnEmptyResult()
        {
            var result = _simulator.Simulate(Even(), 0, 1);

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.WinRate, Is.EqualTo(0d));
        }

        [Test]
        public void AFullTrackOfStages_SimulatesQuickly()
        {
            // 200 stages at 1,000 fights each is the interactive case; it has to stay
            // comfortably inside a debounce interval.
            var watch = Stopwatch.StartNew();

            for (var stage = 1; stage <= 200; stage++)
            {
                var setup = CombatSetup.Default(
                    Combatant.Basic(2000d, 150d, 20d),
                    Combatant.Basic(100d * System.Math.Pow(stage, 1.35d), 12d * stage, 5d * stage));
                setup.Attacker.CritChance = 0.15d;
                _simulator.Simulate(setup, 1000, stage);
            }

            watch.Stop();
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(4000),
                $"200 stages x 1,000 iterations took {watch.ElapsedMilliseconds} ms.");
        }
    }
}
