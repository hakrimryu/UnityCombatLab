using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 強化一種類の基本増加量と選択上限を保存します。
    /// [KO] 강화 한 종류의 기본 증가량과 선택 상한을 보관합니다.
    /// [EN] Stores the base increment and stack limit for one upgrade kind.
    /// </summary>
    public sealed class RewardData
    {
        /// <summary>
        /// 効果の計算方法を選ぶ種類。
        /// [KO] 효과 계산 방법을 고르는 종류입니다.
        /// [EN] Kind selecting the effect calculation.
        /// </summary>
        public Define.RewardKind Kind { get; }

        /// <summary>
        /// 一回分の基本増加量。1500 は15%です。
        /// [KO] 1회 기본 증가량이며 1500은 15%입니다.
        /// [EN] Base increment per selection; 1500 denotes fifteen percent.
        /// </summary>
        public int BaseIncreaseBp { get; }

        /// <summary>
        /// 同じ種類を選択できる上限。
        /// [KO] 같은 종류의 선택 상한입니다.
        /// [EN] Maximum selections of this kind.
        /// </summary>
        public int MaxStacks { get; }

        /// <summary>
        /// 設定値を検査し、固定された強化定義を作ります。
        /// [KO] 설정값을 검사하고 고정된 강화 정의를 만듭니다.
        /// [EN] Validates settings and creates a fixed upgrade definition.
        /// </summary>
        public RewardData(Define.RewardKind kind, int baseIncreaseBp, int maxStacks = 3)
        {
            if ((uint)kind > (uint)Define.RewardKind.Ambush || baseIncreaseBp <= 0 || baseIncreaseBp > 10000 || maxStacks < 1 || maxStacks > 3)
                throw new ArgumentOutOfRangeException(nameof(kind), "Invalid upgrade kind, increment or stack limit.");

            Kind = kind;
            BaseIncreaseBp = baseIncreaseBp;
            MaxStacks = maxStacks;
        }

        /// <summary>
        /// ウェーブ倍率を掛け、正の端数を四捨五入した basis point を返します。
        /// [KO] 웨이브 배율을 곱하고 양수 소수 부분을 반올림한 basis point를 반환합니다.
        /// [EN] Multiplies by wave strength and rounds positive midpoint values away from zero.
        /// </summary>
        public int GetIncrease(int strengthBp)
        {
            if (strengthBp < 1000 || strengthBp > 30000)
                throw new ArgumentOutOfRangeException(nameof(strengthBp));

            // 1500 × 11000 / 10000 = 1650、すなわち15%の基本効果が16.5%になる。
            return (int)(((long)BaseIncreaseBp * strengthBp + Define.BasisPoints / 2) / Define.BasisPoints);
        }

        /// <summary>
        /// 抽選の並びと再現記録で使う固定 ID を返します。
        /// [KO] 추첨 순서와 재현 기록에 사용하는 고정 ID를 반환합니다.
        /// [EN] Returns the stable ID used for draw ordering and reproduction records.
        /// </summary>
        public static string GetId(Define.RewardKind kind)
            => kind switch
            {
                Define.RewardKind.Power => "power",
                Define.RewardKind.Vitality => "vitality",
                Define.RewardKind.Haste => "haste",
                Define.RewardKind.Barrier => "barrier",
                Define.RewardKind.Renewal => "renewal",
                Define.RewardKind.Ambush => "ambush",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
    }
}
