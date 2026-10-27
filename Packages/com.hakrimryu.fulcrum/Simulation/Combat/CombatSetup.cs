using System;

namespace Fulcrum.Simulation
{
    /// <summary>
    /// One side of a fight.
    /// </summary>
    public struct Combatant
    {
        public double Hp;
        public double Attack;
        public double Defense;

        /// <summary>Chance of a critical hit, 0..1.</summary>
        public double CritChance;

        /// <summary>Damage multiplier applied on a critical hit.</summary>
        public double CritMultiplier;

        /// <summary>Chance of avoiding an incoming hit, 0..1.</summary>
        public double DodgeChance;

        public static Combatant Basic(double hp, double attack, double defense) => new Combatant
        {
            Hp = hp,
            Attack = attack,
            Defense = defense,
            CritChance = 0d,
            CritMultiplier = 2d,
            DodgeChance = 0d,
        };
    }

    /// <summary>
    /// A fight to simulate: two combatants and the rules connecting them.
    /// </summary>
    /// <remarks>
    /// The damage formula lives here as a parameter rather than as a fixed rule, because
    /// the question a designer actually asks is "what happens to time-to-kill across every
    /// stage if I change the defense constant". Until formulas are authored as data, the
    /// shape is fixed and the constant is exposed.
    /// </remarks>
    public struct CombatSetup
    {
        public Combatant Attacker;
        public Combatant Defender;

        /// <summary>
        /// Softening constant in <c>attack · (1 − defense / (defense + k))</c>.
        /// </summary>
        /// <remarks>
        /// Larger values make defense matter less. The form is chosen so defense has
        /// diminishing returns and damage never reaches zero, which keeps a fight from
        /// becoming unwinnable rather than merely long.
        /// </remarks>
        public double DefenseConstant;

        /// <summary>Turns after which the fight is abandoned as a loss.</summary>
        /// <remarks>
        /// Without a cap, a match-up where neither side can kill the other runs forever.
        /// Treating it as a loss is right: a fight the player cannot finish is a wall.
        /// </remarks>
        public int TurnLimit;

        /// <summary>Seconds per turn, used to turn turn counts into playtime.</summary>
        public double SecondsPerTurn;

        public static CombatSetup Default(Combatant attacker, Combatant defender) => new CombatSetup
        {
            Attacker = attacker,
            Defender = defender,
            DefenseConstant = 100d,
            TurnLimit = 200,
            SecondsPerTurn = 3d,
        };

        /// <summary>
        /// Damage one hit deals before critical or dodge rolls.
        /// </summary>
        public double BaseDamage(double attack, double defense)
        {
            var k = DefenseConstant;
            var denominator = defense + k;

            // Both at zero would divide by zero; with no defense and no softening the
            // reduction is simply none.
            if (denominator <= 0d) return attack;

            return attack * (1d - defense / denominator);
        }

        /// <summary>A readable form of the damage rule, for reports.</summary>
        public string DescribeFormula()
            => $"atk · (1 − def / (def + {DefenseConstant}))";
    }
}
