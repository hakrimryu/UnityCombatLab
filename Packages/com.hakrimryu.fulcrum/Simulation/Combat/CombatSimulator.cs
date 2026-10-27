using System;

namespace Fulcrum.Simulation
{
    /// <summary>
    /// Runs a fight many times and reports how it went.
    /// </summary>
    /// <remarks>
    /// Deterministic by construction: the same setup, iteration count and seed always
    /// produce the same numbers. A win rate that moves between two runs of an unchanged
    /// profile is not a measurement, and a designer cannot tell whether their edit or the
    /// dice caused a difference.
    ///
    /// This is plain C# with no engine dependency, so a whole track's worth of stages can
    /// be simulated off the main thread while the editor keeps drawing.
    /// </remarks>
    public sealed class CombatSimulator
    {
        /// <summary>
        /// Simulates <paramref name="iterations"/> fights.
        /// </summary>
        /// <param name="setup">The match-up and its rules.</param>
        /// <param name="iterations">How many fights to run.</param>
        /// <param name="seed">Fixed seed; the same value reproduces the same result.</param>
        public CombatResult Simulate(CombatSetup setup, int iterations, int seed)
        {
            var result = new CombatResult();
            if (iterations <= 0) return result;

            result.Iterations = iterations;

            var random = new Random(seed);
            var turnsPerWin = new double[iterations];
            var wins = 0;
            var remainingSum = 0d;
            var hitsTakenSum = 0d;

            for (var i = 0; i < iterations; i++)
            {
                var outcome = RunOnce(setup, random);

                hitsTakenSum += outcome.HitsTaken;

                if (outcome.Won)
                {
                    turnsPerWin[wins++] = outcome.Turns;
                    remainingSum += outcome.RemainingHpRatio;
                }
                else if (outcome.Stalemate)
                {
                    result.Stalemates++;
                }
            }

            result.WinRate = wins / (double)iterations;
            result.AverageHitsTaken = hitsTakenSum / iterations;

            if (wins == 0) return result;

            Array.Sort(turnsPerWin, 0, wins);

            var sum = 0d;
            for (var i = 0; i < wins; i++) sum += turnsPerWin[i];
            result.AverageTurns = sum / wins;

            var varianceSum = 0d;
            for (var i = 0; i < wins; i++)
            {
                var delta = turnsPerWin[i] - result.AverageTurns;
                varianceSum += delta * delta;
            }
            result.TurnsStdDev = Math.Sqrt(varianceSum / wins);

            result.AverageRemainingHpRatio = remainingSum / wins;
            result.P05Turns = Percentile(turnsPerWin, wins, 0.05d);
            result.P50Turns = Percentile(turnsPerWin, wins, 0.50d);
            result.P95Turns = Percentile(turnsPerWin, wins, 0.95d);

            return result;
        }

        private struct Outcome
        {
            public bool Won;
            public bool Stalemate;
            public int Turns;
            public int HitsTaken;
            public double RemainingHpRatio;
        }

        private static Outcome RunOnce(CombatSetup setup, Random random)
        {
            var outcome = new Outcome();

            var attackerHp = setup.Attacker.Hp;
            var defenderHp = setup.Defender.Hp;
            var attackerMaxHp = setup.Attacker.Hp;

            var limit = setup.TurnLimit > 0 ? setup.TurnLimit : 1;

            for (var turn = 1; turn <= limit; turn++)
            {
                defenderHp -= Strike(setup, setup.Attacker, setup.Defender, random);

                if (defenderHp <= 0d)
                {
                    outcome.Won = true;
                    outcome.Turns = turn;
                    outcome.RemainingHpRatio = attackerMaxHp > 0d
                        ? Math.Max(0d, attackerHp) / attackerMaxHp
                        : 0d;
                    return outcome;
                }

                // The defender only retaliates if it survived, so a one-shot kill costs
                // nothing. Simultaneous exchanges would make first strike meaningless.
                var incoming = Strike(setup, setup.Defender, setup.Attacker, random);
                if (incoming > 0d) outcome.HitsTaken++;
                attackerHp -= incoming;

                if (attackerHp <= 0d)
                {
                    outcome.Turns = turn;
                    return outcome;
                }
            }

            outcome.Stalemate = true;
            outcome.Turns = limit;
            return outcome;
        }

        private static double Strike(CombatSetup setup, Combatant source, Combatant target, Random random)
        {
            if (target.DodgeChance > 0d && random.NextDouble() < target.DodgeChance) return 0d;

            var damage = setup.BaseDamage(source.Attack, target.Defense);
            if (damage < 0d) damage = 0d;

            if (source.CritChance > 0d && random.NextDouble() < source.CritChance)
            {
                damage *= source.CritMultiplier;
            }

            return damage;
        }

        /// <summary>
        /// Nearest-rank percentile over an already sorted span.
        /// </summary>
        /// <remarks>
        /// Nearest-rank rather than interpolated: turn counts are whole numbers, and a
        /// reported "7.3 turns at the 95th percentile" would describe a fight nobody had.
        /// </remarks>
        private static double Percentile(double[] sorted, int count, double fraction)
        {
            if (count == 0) return 0d;

            var rank = (int)Math.Ceiling(fraction * count) - 1;
            if (rank < 0) rank = 0;
            if (rank >= count) rank = count - 1;
            return sorted[rank];
        }
    }
}
