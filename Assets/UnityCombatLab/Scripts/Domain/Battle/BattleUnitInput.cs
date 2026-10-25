using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// ユニットの ID・配置と、戦闘開始時の能力値を保持します。
    /// [KO] 유닛의 ID·배치와 전투 시작 능력치를 보관합니다.
    /// [EN] Stores a unit's ID, position and starting combat stats.
    /// </summary>
    public sealed class BattleUnitInput
    {
        /// <summary>
        /// 戦闘内で一意の正数 ID。
        /// [KO] 전투 내 고유 양수 ID.
        /// [EN] Positive battle-unique ID.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 所属陣営。
        /// [KO] 소속 팀.
        /// [EN] Owning team.
        /// </summary>
        public Define.BattleTeam Team { get; }

        /// <summary>
        /// 固定位置。
        /// [KO] 고정 위치.
        /// [EN] Fixed position.
        /// </summary>
        public Define.BattleSlot Slot { get; }

        /// <summary>
        /// 攻撃規則を決める役割。
        /// [KO] 공격 규칙을 결정하는 역할.
        /// [EN] Role determining attack rules.
        /// </summary>
        public Define.UnitRole Role { get; }

        /// <summary>
        /// 最大 HP。
        /// [KO] 최대 HP.
        /// [EN] Maximum health.
        /// </summary>
        public int MaxHealth { get; }

        /// <summary>
        /// 戦闘開始時の HP。
        /// [KO] 전투 시작 시 HP입니다.
        /// [EN] Health at battle startup.
        /// </summary>
        public int Health { get; }

        /// <summary>
        /// 攻撃の被害計算に使う攻撃力。
        /// [KO] 공격 피해 계산에 사용할 공격력입니다.
        /// [EN] Attack power used to calculate damage.
        /// </summary>
        public int Attack { get; }

        /// <summary>
        /// 次の攻撃までの間隔。単位は tick です。
        /// [KO] 다음 공격까지의 간격이며 단위는 tick입니다.
        /// [EN] Interval until the next attack, measured in ticks.
        /// </summary>
        public int IntervalTicks { get; }

        /// <summary>
        /// 開始時の防御膜。
        /// [KO] 초기 방어막.
        /// [EN] Initial shield.
        /// </summary>
        public int Shield { get; }

        /// <summary>
        /// ID・陣営・能力値の範囲を確認して、戦闘開始データを作ります。
        /// [KO] ID·팀·능력치 범위를 확인해 전투 시작 데이터를 만듭니다.
        /// [EN] Checks the ID, team and stat ranges and creates starting combat data.
        /// </summary>
        public BattleUnitInput(int id, Define.BattleTeam team, Define.BattleSlot slot, Define.UnitRole role,
            int maxHealth, int attack, int intervalTicks, int shield = 0, int? health = null)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if ((uint)team >= Define.BattleTeamCount)
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            if ((uint)slot >= Define.SlotsPerTeam)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            if ((uint)role > (uint)Define.UnitRole.SkeletonCaptain)
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            var isAllyRole = role is Define.UnitRole.Knight or Define.UnitRole.Ranger or Define.UnitRole.Mage;
            if ((team == Define.BattleTeam.Ally) != isAllyRole)
            {
                throw new ArgumentException("Role must belong to its team.", nameof(role));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            }

            // 攻撃係数の変更時も、計算側と入力上限を一致させる。
            if (attack < 0 || attack > BattleCalculation.MaxAttackPower)
            {
                throw new ArgumentOutOfRangeException(nameof(attack));
            }

            if (intervalTicks < Define.MinimumAttackIntervalTicks || intervalTicks > Define.BattleTickLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalTicks));
            }

            if (shield < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shield));
            }

            var initialHealth = health ?? maxHealth;
            if (initialHealth < 0 || initialHealth > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health));
            }

            Id = id;
            Team = team;
            Slot = slot;
            Role = role;
            MaxHealth = maxHealth;
            Health = initialHealth;
            Attack = attack;
            IntervalTicks = intervalTicks;
            Shield = shield;
        }
    }
}
