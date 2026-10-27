namespace Fulcrum.Simulation
{
    /// <summary>
    /// The parameters of one simulation pass.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="PlayerProfile"/> because these describe how the
    /// measurement is taken, not who is playing. Lowering the iteration count while a
    /// slider is being dragged should not change the player being simulated.
    /// </remarks>
    public struct SimulationRun
    {
        /// <summary>First axis position to simulate.</summary>
        public int From;

        /// <summary>Last axis position to simulate, inclusive.</summary>
        public int To;

        /// <summary>Fights per position.</summary>
        public int Iterations;

        /// <summary>Base seed; each position offsets it so positions stay independent.</summary>
        public int Seed;

        /// <summary>Softening constant in the damage formula.</summary>
        public double DefenseConstant;

        /// <summary>Turns after which a fight is abandoned.</summary>
        public int TurnLimit;

        /// <summary>Seconds one turn represents.</summary>
        public double SecondsPerTurn;

        public static SimulationRun Default(int from, int to) => new SimulationRun
        {
            From = from,
            To = to,
            Iterations = 1000,
            Seed = 20260804,
            DefenseConstant = 100d,
            TurnLimit = 200,
            SecondsPerTurn = 3d,
        };

        /// <summary>
        /// The same run with fewer fights, for use while a value is still being dragged.
        /// </summary>
        public SimulationRun Coarse(int iterations = 100)
        {
            var copy = this;
            copy.Iterations = iterations;
            return copy;
        }
    }
}
