using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fulcrum
{
    /// <summary>
    /// Which track fills each role a simulator needs.
    /// </summary>
    /// <remarks>
    /// The simulators need to know where monster health comes from, but hard-coding
    /// <c>monster_hp</c> would make them work only for projects that happen to use that
    /// name. Naming is part of a project's vocabulary; a package that dictates it is a
    /// package that has to be forked to be used.
    ///
    /// An empty role is normal. A profile that has not defined upgrade costs yet still
    /// simulates combat; the economy run simply reports nothing for that part.
    /// </remarks>
    [Serializable]
    public sealed class TrackBinding
    {
        [SerializeField] private string _axisId = "stage";
        [SerializeField] private string _monsterHp = "monster_hp";
        [SerializeField] private string _monsterAttack = "monster_atk";
        [SerializeField] private string _monsterDefense;
        [SerializeField] private string _stageExp;
        [SerializeField] private string _expRequired;
        [SerializeField] private string _stageGold;
        [SerializeField] private string _upgradeCost;

        /// <summary>Axis the simulation walks along.</summary>
        public string AxisId
        {
            get => _axisId;
            set => _axisId = value;
        }

        public string MonsterHp
        {
            get => _monsterHp;
            set => _monsterHp = value;
        }

        public string MonsterAttack
        {
            get => _monsterAttack;
            set => _monsterAttack = value;
        }

        public string MonsterDefense
        {
            get => _monsterDefense;
            set => _monsterDefense = value;
        }

        /// <summary>Experience granted by clearing a position.</summary>
        public string StageExp
        {
            get => _stageExp;
            set => _stageExp = value;
        }

        /// <summary>Experience needed for one level, indexed by level.</summary>
        public string ExpRequired
        {
            get => _expRequired;
            set => _expRequired = value;
        }

        /// <summary>Currency granted by clearing a position.</summary>
        public string StageGold
        {
            get => _stageGold;
            set => _stageGold = value;
        }

        /// <summary>Cost of one upgrade, indexed by how many are already owned.</summary>
        public string UpgradeCost
        {
            get => _upgradeCost;
            set => _upgradeCost = value;
        }

        /// <summary>
        /// Names the slots that have to be filled before a simulation means anything.
        /// </summary>
        /// <remarks>
        /// An unfilled slot resolves to a track that does not exist, which every simulator
        /// reads as a series of zeroes. Monsters with no health are beaten every time, so an
        /// unbound profile does not produce no result — it produces a confident report about
        /// a game nobody described: a 95 % win rate, nothing at stake, a predicted playtime.
        ///
        /// Refusing to run and saying what is missing is the only honest answer. Defence is
        /// left out because a zero there is a real value, not an absent one.
        /// </remarks>
        public List<string> MissingBindings(BalanceProfile profile)
        {
            var missing = new List<string>();
            if (profile == null) return missing;

            if (string.IsNullOrEmpty(_axisId) || !profile.TryGetAxis(_axisId, out _))
            {
                missing.Add("Axis");
            }

            Require(profile, missing, _monsterHp, "Monster HP");
            Require(profile, missing, _monsterAttack, "Monster attack");
            Require(profile, missing, _stageExp, "Experience per position");
            Require(profile, missing, _expRequired, "Experience per level");
            Require(profile, missing, _stageGold, "Currency per position");
            Require(profile, missing, _upgradeCost, "Upgrade cost");

            return missing;
        }

        /// <summary>A slot counts as missing when it is blank or points at nothing.</summary>
        private static void Require(
            BalanceProfile profile, List<string> missing, string trackId, string label)
        {
            if (!string.IsNullOrEmpty(trackId) && profile.TryGetTrack(trackId, out _)) return;

            missing.Add(label);
        }
    }
}
