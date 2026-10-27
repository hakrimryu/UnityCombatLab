using System;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// How the simulators should be run for this profile.
    /// </summary>
    /// <remarks>
    /// Only the settings the simulators cannot work without are here; the rest arrive with
    /// the simulators themselves.
    ///
    /// The seed is stored rather than generated because a balance figure that changes
    /// between two runs of the same profile is not a measurement. Reproducibility is what
    /// makes a simulated win rate something a designer can act on.
    /// </remarks>
    [Serializable]
    public sealed class SimulationSettings
    {
        [SerializeField] private int _iterations = 1000;
        [SerializeField] private int _seed = 20260804;

        public SimulationSettings() { }

        public SimulationSettings(int iterations, int seed)
        {
            _iterations = iterations;
            _seed = seed;
        }

        /// <summary>Monte Carlo runs per evaluated position.</summary>
        public int Iterations
        {
            get => _iterations;
            set => _iterations = value;
        }

        /// <summary>Fixed seed, so repeated runs of one profile agree.</summary>
        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }
    }
}
