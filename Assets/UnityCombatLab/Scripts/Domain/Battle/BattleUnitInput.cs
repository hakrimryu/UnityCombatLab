using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 外部データから確定した不変の戦闘入力。
    /// [KO] 외부 데이터로 확정한 불변 전투 입력.
    /// [EN] Immutable resolved combat input.
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
        /// 開始 HP。0 は境界検証用。
        /// [KO] 시작 HP, 0은 경계 검증용.
        /// [EN] Initial health; zero supports boundary validation.
        /// </summary>
        public int Health { get; }

        /// <summary>
        /// 解決済み攻撃力。
        /// [KO] 확정 공격력.
        /// [EN] Resolved attack power.
        /// </summary>
        public int Attack { get; }

        /// <summary>
        /// 整数の攻撃間隔。
        /// [KO] 정수 공격 간격.
        /// [EN] Integer attack interval.
        /// </summary>
        public int IntervalTicks { get; }

        /// <summary>
        /// 開始時の防御膜。
        /// [KO] 초기 방어막.
        /// [EN] Initial shield.
        /// </summary>
        public int Shield { get; }

        /// <summary>
        /// 入力の範囲を検証します。
        /// [KO] 입력 범위를 검증합니다.
        /// [EN] Validates input ranges.
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
