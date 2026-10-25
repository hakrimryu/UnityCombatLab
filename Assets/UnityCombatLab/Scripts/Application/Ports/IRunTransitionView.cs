using UnityCombatLab.Common;

namespace UnityCombatLab.Application.Ports
{
    /// <summary>
    /// シーンをまたぐ読込・片付け・失敗の表示を切り替える境界。
    /// [KO] 씬을 넘는 로딩·정리·실패 표시를 전환하는 경계입니다.
    /// [EN] Boundary for displaying loading, cleanup and failure across scenes.
    /// </summary>
    public interface IRunTransitionView
    {
        /// <summary>
        /// 進行段階または失敗の案内を表示します。
        /// [KO] 진행 단계 또는 실패 안내를 표시합니다.
        /// [EN] Displays the current phase or failure guidance.
        /// </summary>
        void Show(Define.RunTransitionPhase phase);

        /// <summary>
        /// 切替が終わった表示を閉じます。
        /// [KO] 전환이 끝난 표시를 닫습니다.
        /// [EN] Closes the display after transition completion.
        /// </summary>
        void Hide();
    }
}
