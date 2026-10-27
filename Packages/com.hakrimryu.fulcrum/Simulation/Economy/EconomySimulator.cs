using System;

namespace Fulcrum.Simulation
{
    /// <summary>Where the currency goes, position by position.</summary>
    public struct EconomyResult
    {
        public int RangeMin;

        /// <summary>Currency earned at each position.</summary>
        public double[] IncomePerStage;

        /// <summary>Currency spent at each position.</summary>
        public double[] SpendPerStage;

        /// <summary>Running balance after each position.</summary>
        public double[] NetBalance;

        /// <summary>Upgrades owned after each position.</summary>
        public int[] UpgradesAtStage;

        /// <summary>
        /// First position where the balance starts growing without being spent, or -1.
        /// </summary>
        /// <remarks>
        /// Surplus that keeps growing means the currency has stopped being a decision.
        /// </remarks>
        public int InflationStartStage;

        /// <summary>
        /// First position where the player can no longer afford the next upgrade, or -1.
        /// </summary>
        public int DeflationStartStage;

        public double FinalBalance
            => NetBalance == null || NetBalance.Length == 0 ? 0d : NetBalance[NetBalance.Length - 1];
    }

    /// <summary>
    /// Follows the currency: what comes in, what it buys, and what is left over.
    /// </summary>
    /// <remarks>
    /// Both failure modes are quiet ones. A surplus that only grows means upgrades stopped
    /// being a choice; a shortfall that never closes means progression stopped depending on
    /// play. Neither is visible in the income curve alone — only in the balance over time.
    /// </remarks>
    public sealed class EconomySimulator
    {
        /// <summary>
        /// Consecutive positions of unspent growth before a surplus counts as inflation.
        /// </summary>
        /// <remarks>
        /// A run rather than a single position: saving up for the next upgrade is normal,
        /// and flagging every gap between purchases would report nothing useful.
        /// </remarks>
        public int InflationRun { get; set; } = 10;

        public EconomyResult Simulate(BalanceSnapshot balance, TrackBinding binding, PlayerProfile player, SimulationRun run)
        {
            var result = new EconomyResult
            {
                RangeMin = run.From,
                InflationStartStage = -1,
                DeflationStartStage = -1,
            };

            var count = run.To - run.From + 1;
            if (balance == null || binding == null || count <= 0) return result;

            result.IncomePerStage = new double[count];
            result.SpendPerStage = new double[count];
            result.NetBalance = new double[count];
            result.UpgradesAtStage = new int[count];

            var gold = 0d;
            var upgrades = 0;
            var growthRun = 0;
            var growthRunStart = -1;

            for (var i = 0; i < count; i++)
            {
                var stage = run.From + i;

                var income = balance.Get(binding.StageGold, stage) * player.SpendRatio;
                gold += income;
                result.IncomePerStage[i] = income;

                var spent = 0d;
                for (var guard = 0; guard < 1000; guard++)
                {
                    if (!balance.TryGet(binding.UpgradeCost, upgrades + 1, out var cost)) break;
                    if (cost <= 0d || gold < cost) break;

                    gold -= cost;
                    spent += cost;
                    upgrades++;
                }

                result.SpendPerStage[i] = spent;
                result.NetBalance[i] = gold;
                result.UpgradesAtStage[i] = upgrades;

                if (spent > 0d)
                {
                    growthRun = 0;
                    growthRunStart = -1;
                }
                else if (income > 0d)
                {
                    if (growthRunStart < 0) growthRunStart = stage;
                    growthRun++;

                    if (result.InflationStartStage < 0 && growthRun >= InflationRun)
                    {
                        result.InflationStartStage = growthRunStart;
                    }
                }

                // Affordability is what a player actually feels, not a negative balance:
                // the currency never goes below zero, the next upgrade just stays locked.
                if (result.DeflationStartStage < 0
                    && balance.TryGet(binding.UpgradeCost, upgrades + 1, out var next)
                    && next > 0d
                    && income > 0d
                    && income * 20d < next)
                {
                    result.DeflationStartStage = stage;
                }
            }

            return result;
        }
    }
}
