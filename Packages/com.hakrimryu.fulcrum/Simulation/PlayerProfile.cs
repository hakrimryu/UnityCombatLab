namespace Fulcrum.Simulation
{
    /// <summary>
    /// The player a simulation assumes.
    /// </summary>
    /// <remarks>
    /// A simulated result is only meaningful relative to who is playing. The same curve is
    /// comfortable for someone who upgrades optimally and a wall for someone who does not,
    /// and a single "average player" hides exactly the layer that gets stuck.
    /// </remarks>
    public struct PlayerProfile
    {
        public string Name;

        public double BaseHp;
        public double BaseAttack;
        public double BaseDefense;

        public double HpPerLevel;
        public double AttackPerLevel;
        public double DefensePerLevel;

        /// <summary>
        /// Damage multiplier standing in for execution: positioning, timing, ability use.
        /// </summary>
        public double SkillMultiplier;

        /// <summary>Fraction of earned currency actually spent on upgrades, 0..1.</summary>
        public double SpendRatio;

        /// <summary>Attack gained per upgrade purchased.</summary>
        public double AttackPerUpgrade;

        public double CritChance;
        public double CritMultiplier;
        public double DodgeChance;

        /// <summary>
        /// Attempts made at a position before moving on.
        /// </summary>
        /// <remarks>
        /// Retrying converts a low win rate into more elapsed time rather than a hard stop,
        /// which is what players actually do and what makes predicted playtime believable.
        /// </remarks>
        public int MaxAttempts;

        public double HpAtLevel(int level) => BaseHp + HpPerLevel * (level - 1);
        public double AttackAtLevel(int level) => BaseAttack + AttackPerLevel * (level - 1);
        public double DefenseAtLevel(int level) => BaseDefense + DefensePerLevel * (level - 1);

        /// <summary>Spends little, plays loosely, gives up sooner.</summary>
        public static PlayerProfile Casual() => new PlayerProfile
        {
            Name = "Casual",
            BaseHp = 500d, BaseAttack = 50d, BaseDefense = 10d,
            HpPerLevel = 45d, AttackPerLevel = 5d, DefensePerLevel = 1.5d,
            SkillMultiplier = 0.85d,
            SpendRatio = 0.5d,
            AttackPerUpgrade = 2d,
            CritChance = 0.05d, CritMultiplier = 1.5d, DodgeChance = 0.02d,
            MaxAttempts = 2,
        };

        /// <summary>Spends everything earned, plays to the expected standard.</summary>
        public static PlayerProfile Standard() => new PlayerProfile
        {
            Name = "Standard",
            BaseHp = 500d, BaseAttack = 50d, BaseDefense = 10d,
            HpPerLevel = 45d, AttackPerLevel = 5d, DefensePerLevel = 1.5d,
            SkillMultiplier = 1d,
            SpendRatio = 1d,
            AttackPerUpgrade = 2d,
            CritChance = 0.1d, CritMultiplier = 1.75d, DodgeChance = 0.05d,
            MaxAttempts = 3,
        };

        /// <summary>Spends everything, executes well, retries until it works.</summary>
        public static PlayerProfile Optimizer() => new PlayerProfile
        {
            Name = "Optimizer",
            BaseHp = 500d, BaseAttack = 50d, BaseDefense = 10d,
            HpPerLevel = 45d, AttackPerLevel = 5d, DefensePerLevel = 1.5d,
            SkillMultiplier = 1.15d,
            SpendRatio = 1d,
            AttackPerUpgrade = 2d,
            CritChance = 0.18d, CritMultiplier = 2d, DodgeChance = 0.08d,
            MaxAttempts = 6,
        };

        /// <summary>The three presets, weakest first.</summary>
        public static PlayerProfile[] Presets() => new[] { Casual(), Standard(), Optimizer() };
    }
}
