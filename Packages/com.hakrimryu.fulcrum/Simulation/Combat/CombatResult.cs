using System;

namespace Fulcrum.Simulation
{
    /// <summary>
    /// What a batch of simulated fights produced.
    /// </summary>
    /// <remarks>
    /// Averages alone hide the shape of a fight. A 60% win rate reached by winning
    /// comfortably most of the time is a different design from one reached by winning
    /// narrowly every time, and the percentiles are what tell them apart.
    /// </remarks>
    public struct CombatResult
    {
        /// <summary>Fraction of fights the attacker won, 0..1.</summary>
        public double WinRate;

        /// <summary>Mean turns to finish, counting only wins. Time to kill.</summary>
        public double AverageTurns;

        /// <summary>Spread of the turn count across wins.</summary>
        public double TurnsStdDev;

        /// <summary>
        /// Mean fraction of the attacker's health left after a win, 0..1.
        /// </summary>
        /// <remarks>
        /// How close the fight was. A high win rate with almost nothing left is a stage
        /// that will feel unfair to anyone slightly below average.
        /// </remarks>
        public double AverageRemainingHpRatio;

        /// <summary>Mean number of hits the attacker took.</summary>
        public double AverageHitsTaken;

        /// <summary>Turn counts at the 5th, 50th and 95th percentile of wins.</summary>
        public double P05Turns, P50Turns, P95Turns;

        /// <summary>Fights that hit the turn limit without either side dying.</summary>
        public int Stalemates;

        /// <summary>How many fights were simulated.</summary>
        public int Iterations;

        /// <summary>Whether anything was simulated at all.</summary>
        public bool IsEmpty => Iterations == 0;

        public override string ToString()
            => $"win {WinRate:P1}, {AverageTurns:F1} turns (p05 {P05Turns:F0} / p95 {P95Turns:F0})";
    }
}
