using System;
using System.Linq;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 状態を変更せず、固定戦闘規則の整数計算を行います。
    /// [KO] 상태를 변경하지 않고 고정 전투 규칙의 정수 계산을 수행합니다.
    /// [EN] Performs integer calculations for fixed combat rules without changing state.
    /// </summary>
    internal static class BattleCalculation
    {
        /// <summary>
        /// 固定攻撃規則の中で最大の百分率。
        /// [KO] 고정 공격 규칙 중 최대 백분율.
        /// [EN] Largest percentage among the fixed attack rules.
        /// </summary>
        private static readonly int _maximumDamagePercent = new[]
        {
            Define.PercentScale,
            Define.RangerSpecialDamagePercent,
            Define.MageAreaDamagePercent,
            Define.CaptainAreaDamagePercent
        }.Max();

        /// <summary>
        /// 最大の攻撃倍率を適用しても、被害が int の範囲に収まる攻撃力の上限。
        /// [KO] 최대 공격 배율을 적용해도 피해가 int 범위에 들어오는 공격력 상한입니다.
        /// [EN] Maximum attack power whose damage fits in an int at the largest attack multiplier.
        /// </summary>
        internal static readonly int MaxAttackPower =
            (int)((long)int.MaxValue * Define.PercentScale / _maximumDamagePercent);

        /// <summary>
        /// 実行する攻撃が通常攻撃を置換する範囲攻撃か判定します。
        /// [KO] 실행할 공격이 일반 공격을 대체하는 광역 공격인지 판정합니다.
        /// [EN] Determines whether an executed attack replaces the normal attack with area damage.
        /// </summary>
        internal static bool IsAreaAttack(Define.UnitRole role, int attackCount)
        {
            return role switch
            {
                Define.UnitRole.Mage => attackCount % Define.MageAreaAttackPeriod == 0,
                Define.UnitRole.SkeletonCaptain => attackCount % Define.CaptainAreaAttackPeriod == 0,
                _ => false
            };
        }

        /// <summary>
        /// 攻撃者の周期係数と対象の軽減を順番に切り捨て、最小被害1を保証します。
        /// [KO] 공격자의 주기 계수와 대상의 경감을 순서대로 내림하고 최소 피해 1을 보장합니다.
        /// [EN] Floors attack scaling and target mitigation separately, then enforces minimum damage.
        /// </summary>
        internal static int CalculateDamage(BattleUnitInput attacker, int attackCount, Define.UnitRole targetRole)
        {
            var damagePercent = GetDamagePercent(attacker.Role, attackCount);
            var damage = (long)attacker.Attack * ToBasisPoints(damagePercent) / Define.BasisPoints;
            if (targetRole == Define.UnitRole.Knight)
            {
                var receivedDamagePercent = Define.PercentScale - Define.KnightDamageReductionPercent;
                damage = damage * ToBasisPoints(receivedDamagePercent) / Define.BasisPoints;
            }

            return (int)Math.Max(Define.MinimumDamage, damage);
        }

        /// <summary>
        /// 役割と実行回数から攻撃力に対する百分率を求めます。
        /// [KO] 역할과 실제 실행 횟수로 공격력 대비 백분율을 구합니다.
        /// [EN] Resolves damage percentage from role and executed attack count.
        /// </summary>
        private static int GetDamagePercent(Define.UnitRole role, int attackCount)
        {
            if (IsAreaAttack(role, attackCount))
            {
                return role == Define.UnitRole.Mage
                    ? Define.MageAreaDamagePercent
                    : Define.CaptainAreaDamagePercent;
            }

            return role == Define.UnitRole.Ranger && attackCount % Define.RangerSpecialAttackPeriod == 0
                ? Define.RangerSpecialDamagePercent
                : Define.PercentScale;
        }

        /// <summary>
        /// 固定規則の百分率をbasis pointへ変換します。
        /// [KO] 고정 규칙의 백분율을 basis point로 변환합니다.
        /// [EN] Converts fixed-rule percentages to basis points.
        /// </summary>
        private static int ToBasisPoints(int percent) => percent * Define.BasisPoints / Define.PercentScale;
    }
}
