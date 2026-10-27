using System;
using System.Collections.Generic;

namespace Fulcrum.Simulation
{
    /// <summary>What a simulated playthrough produced, one entry per position reached.</summary>
    public struct ProgressionResult
    {
        /// <summary>First axis position simulated.</summary>
        public int RangeMin;

        /// <summary>Character level on arriving at each position.</summary>
        public int[] LevelAtStage;

        /// <summary>Total experience held on arriving at each position.</summary>
        public double[] CumulativeExp;

        /// <summary>Elapsed play seconds on arriving at each position, retries included.</summary>
        public double[] CumulativePlayTimeSec;

        /// <summary>Win rate at each position.</summary>
        public double[] WinRateAtStage;

        /// <summary>Turns per winning fight at each position.</summary>
        public double[] TurnsAtStage;

        /// <summary>Upgrades owned on arriving at each position.</summary>
        public int[] UpgradesAtStage;

        /// <summary>
        /// First position whose win rate fell below the wall threshold, or -1.
        /// </summary>
        public int FirstWallStage;

        /// <summary>Last position actually reached.</summary>
        public int ReachedStage;

        /// <summary>Total predicted playtime in hours.</summary>
        public double TotalHours
            => CumulativePlayTimeSec == null || CumulativePlayTimeSec.Length == 0
                ? 0d
                : CumulativePlayTimeSec[CumulativePlayTimeSec.Length - 1] / 3600d;
    }

    /// <summary>
    /// Walks a player through the axis, fighting, levelling and spending as they go.
    /// </summary>
    /// <remarks>
    /// The output worth reading is the last line: "reaching stage 50 takes about fourteen
    /// hours". A stat table is an argument about numbers; a duration is an argument about
    /// the game, and it is the form a balance discussion can actually be had in.
    /// </remarks>
    public sealed class ProgressionSimulator
    {
        private readonly CombatSimulator _combat = new CombatSimulator();

        /// <summary>Win rate below which a position counts as a wall.</summary>
        public double WallWinRate { get; set; } = 0.15d;

        public ProgressionResult Simulate(
            BalanceSnapshot balance,
            TrackBinding binding,
            PlayerProfile player,
            SimulationRun run)
        {
            var result = new ProgressionResult { FirstWallStage = -1, RangeMin = run.From };

            var count = run.To - run.From + 1;
            if (balance == null || binding == null || count <= 0) return result;

            result.LevelAtStage = new int[count];
            result.CumulativeExp = new double[count];
            result.CumulativePlayTimeSec = new double[count];
            result.WinRateAtStage = new double[count];
            result.TurnsAtStage = new double[count];
            result.UpgradesAtStage = new int[count];

            var level = 1;
            var exp = 0d;
            var gold = 0d;
            var upgrades = 0;
            var elapsed = 0d;
            var simulated = 0;

            for (var i = 0; i < count; i++)
            {
                simulated = i + 1;
                var stage = run.From + i;

                result.LevelAtStage[i] = level;
                result.CumulativeExp[i] = exp;
                result.UpgradesAtStage[i] = upgrades;

                var setup = BuildSetup(balance, binding, player, level, upgrades, stage, run);
                var fight = _combat.Simulate(setup, run.Iterations, run.Seed + stage);

                result.WinRateAtStage[i] = fight.WinRate;
                result.TurnsAtStage[i] = fight.AverageTurns;

                if (result.FirstWallStage < 0 && fight.WinRate < WallWinRate)
                {
                    result.FirstWallStage = stage;
                }

                elapsed += TimeSpentOn(fight, player, run);
                result.CumulativePlayTimeSec[i] = elapsed;
                result.ReachedStage = stage;

                // A position that cannot be cleared ends the run: everything past it is a
                // prediction about a game the player never sees.
                if (fight.WinRate <= 0d) break;

                exp += balance.Get(binding.StageExp, stage);
                gold += balance.Get(binding.StageGold, stage) * player.SpendRatio;

                level = LevelUp(balance, binding, level, ref exp);
                upgrades = BuyUpgrades(balance, binding, upgrades, ref gold);
            }

            // A run that stalled describes only the positions it reached. Leaving the rest
            // as zeros would read as "level 0, no time spent" rather than "never got here".
            if (simulated < count) Truncate(ref result, simulated);

            return result;
        }

        private static void Truncate(ref ProgressionResult result, int length)
        {
            Array.Resize(ref result.LevelAtStage, length);
            Array.Resize(ref result.CumulativeExp, length);
            Array.Resize(ref result.CumulativePlayTimeSec, length);
            Array.Resize(ref result.WinRateAtStage, length);
            Array.Resize(ref result.TurnsAtStage, length);
            Array.Resize(ref result.UpgradesAtStage, length);
        }

        /// <summary>
        /// Seconds spent on one position, including failed attempts.
        /// </summary>
        /// <remarks>
        /// Expected attempts is 1/p capped at the player's patience. Ignoring retries would
        /// make a 20%-win-rate stage look as cheap as a certain one, which is precisely the
        /// stage a playtime estimate exists to expose.
        /// </remarks>
        private static double TimeSpentOn(CombatResult fight, PlayerProfile player, SimulationRun run)
        {
            var attempts = fight.WinRate > 0d
                ? Math.Min(1d / fight.WinRate, Math.Max(1, player.MaxAttempts))
                : Math.Max(1, player.MaxAttempts);

            var turns = fight.AverageTurns > 0d ? fight.AverageTurns : run.TurnLimit;
            return attempts * turns * run.SecondsPerTurn;
        }

        private static CombatSetup BuildSetup(
            BalanceSnapshot balance, TrackBinding binding, PlayerProfile player,
            int level, int upgrades, int stage, SimulationRun run)
        {
            var attacker = new Combatant
            {
                Hp = player.HpAtLevel(level),
                Attack = (player.AttackAtLevel(level) + upgrades * player.AttackPerUpgrade)
                         * player.SkillMultiplier,
                Defense = player.DefenseAtLevel(level),
                CritChance = player.CritChance,
                CritMultiplier = player.CritMultiplier <= 0d ? 2d : player.CritMultiplier,
                DodgeChance = player.DodgeChance,
            };

            var defender = new Combatant
            {
                Hp = balance.Get(binding.MonsterHp, stage, 1d),
                Attack = balance.Get(binding.MonsterAttack, stage),
                Defense = balance.Get(binding.MonsterDefense, stage),
                CritMultiplier = 2d,
            };

            var setup = CombatSetup.Default(attacker, defender);
            setup.DefenseConstant = run.DefenseConstant;
            setup.TurnLimit = run.TurnLimit;
            setup.SecondsPerTurn = run.SecondsPerTurn;
            return setup;
        }

        /// <summary>
        /// Spends experience on levels while there is enough for the next one.
        /// </summary>
        private static int LevelUp(BalanceSnapshot balance, TrackBinding binding, int level, ref double exp)
        {
            if (string.IsNullOrEmpty(binding.ExpRequired)) return level;

            // Bounded so a required-experience track of zero cannot loop forever.
            for (var guard = 0; guard < 1000; guard++)
            {
                if (!balance.TryGet(binding.ExpRequired, level, out var needed)) break;
                if (needed <= 0d || exp < needed) break;

                exp -= needed;
                level++;
            }

            return level;
        }

        private static int BuyUpgrades(BalanceSnapshot balance, TrackBinding binding, int upgrades, ref double gold)
        {
            if (string.IsNullOrEmpty(binding.UpgradeCost)) return upgrades;

            for (var guard = 0; guard < 1000; guard++)
            {
                if (!balance.TryGet(binding.UpgradeCost, upgrades + 1, out var cost)) break;
                if (cost <= 0d || gold < cost) break;

                gold -= cost;
                upgrades++;
            }

            return upgrades;
        }
    }
}
