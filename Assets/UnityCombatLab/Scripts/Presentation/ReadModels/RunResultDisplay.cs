using System;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.UI;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// Presenterが確定結果から作る表示内容。Viewには表示するイラストと文章だけを渡します。
    /// [KO] Presenter가 확정 결과로 만드는 표시 내용이며 View에는 표시할 일러스트와 문장만 전달합니다.
    /// [EN] Display content built by the presenter from a committed result, giving the view only artwork choice and text.
    /// </summary>
    public sealed class RunResultDisplay
    {
        /// <summary>
        /// 勝利イラストを表示するか。falseなら敗北イラストを使います。
        /// [KO] 승리 일러스트 표시 여부이며 false이면 패배 일러스트를 사용합니다.
        /// [EN] Whether to show the victory artwork; false selects the defeat artwork.
        /// </summary>
        public bool IsVictory { get; }

        /// <summary>
        /// イラストに添える終了理由。
        /// [KO] 일러스트에 덧붙일 종료 이유입니다.
        /// [EN] Outcome reason displayed with the artwork.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 難易度・戦闘数・経過時間を含む詳細。
        /// [KO] 난이도·전투 수·경과 시간을 포함한 상세입니다.
        /// [EN] Details including difficulty, battle count and elapsed time.
        /// </summary>
        public string Details { get; }

        /// <summary>
        /// 戦闘の参照を持たず、確定値を表示文字へ変換します。
        /// [KO] 전투 참조를 보관하지 않고 확정값을 표시 문자열로 변환합니다.
        /// [EN] Converts committed values to display text without retaining battle references.
        /// </summary>
        public RunResultDisplay(RunResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            IsVictory = result.Outcome == Define.BattleResult.Victory;
            Reason = GameText.Outcome(result.Outcome);
            Details = GameText.ResultDetails(result);
        }
    }
}
