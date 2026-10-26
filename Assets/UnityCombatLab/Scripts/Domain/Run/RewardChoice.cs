using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 選択したウェーブ・強化種類・確定増加量を記録します。
    /// [KO] 선택한 웨이브·강화 종류·확정 증가량을 기록합니다.
    /// [EN] Records the cleared wave, selected upgrade and committed increment.
    /// </summary>
    public readonly struct RewardChoice
    {
        /// <summary>
        /// この選択を提示したクリア済みウェーブ。
        /// [KO] 이 선택을 제시한 클리어 웨이브입니다.
        /// [EN] Cleared wave that offered this choice.
        /// </summary>
        public int Wave { get; }

        /// <summary>
        /// 選択した強化。
        /// [KO] 선택한 강화입니다.
        /// [EN] Selected upgrade.
        /// </summary>
        public Define.RewardKind Kind { get; }

        /// <summary>
        /// 選択時の倍率を適用済みの増加量。
        /// [KO] 선택 시점의 배율을 적용한 증가량입니다.
        /// [EN] Increment after applying strength at selection time.
        /// </summary>
        public int IncreaseBp { get; }

        /// <summary>
        /// 再現と結果表示に必要な確定値だけを受け取ります。
        /// [KO] 재현과 결과 표시에 필요한 확정값만 받습니다.
        /// [EN] Receives only committed values needed for reproduction and results.
        /// </summary>
        public RewardChoice(int wave, Define.RewardKind kind, int increaseBp)
        {
            if (wave < 1 || wave >= Define.WavesPerRun || (uint)kind > (uint)Define.RewardKind.Ambush || increaseBp <= 0)
                throw new ArgumentOutOfRangeException(nameof(wave));

            Wave = wave;
            Kind = kind;
            IncreaseBp = increaseBp;
        }
    }
}
