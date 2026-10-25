using System;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.ReadModels;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// FrontendPresenterが必要とする入力通知と表示操作。テストではUnityの画面を作らず置き換えられます。
    /// [KO] FrontendPresenter에 필요한 입력 알림과 표시 조작이며 테스트에서는 Unity 화면 없이 대체할 수 있습니다.
    /// [EN] Input and display operations required by FrontendPresenter, replaceable without creating a Unity screen in tests.
    /// </summary>
    public interface IFrontendView
    {
        /// <summary>
        /// 画面切替の要求。
        /// [KO] 화면 전환 요청입니다.
        /// [EN] Page navigation request.
        /// </summary>
        event Action<Define.FrontendPage> PageRequested;

        /// <summary>
        /// 英雄カードの選択。
        /// [KO] 영웅 카드 선택입니다.
        /// [EN] Hero-card selection.
        /// </summary>
        event Action<Define.UnitRole> HeroSelected;

        /// <summary>
        /// 配置先の選択。
        /// [KO] 배치할 위치 선택입니다.
        /// [EN] Destination-slot selection.
        /// </summary>
        event Action<Define.BattleSlot> SlotSelected;

        /// <summary>
        /// 難易度の変更。
        /// [KO] 난이도 변경입니다.
        /// [EN] Difficulty change.
        /// </summary>
        event Action<Define.Difficulty> DifficultySelected;

        /// <summary>
        /// 出発要求。
        /// [KO] 출발 요청입니다.
        /// [EN] Departure request.
        /// </summary>
        event Action DepartureRequested;

        /// <summary>
        /// 失敗した読み込みの再試行。
        /// [KO] 실패한 로딩 재시도입니다.
        /// [EN] Retry of a failed load.
        /// </summary>
        event Action RetryRequested;

        /// <summary>
        /// 音量の変更要求。
        /// [KO] 음량 변경 요청입니다.
        /// [EN] Volume-change request.
        /// </summary>
        event Action<float> VolumeRequested;

        /// <summary>
        /// 指定画面だけを表示し、結果は独立した値から描画します。
        /// [KO] 지정한 화면만 표시하고 결과는 독립된 값에서 그립니다.
        /// [EN] Shows only the requested page and renders results from independent values.
        /// </summary>
        void ShowPage(Define.FrontendPage page, RunResultDisplay result, string error);

        /// <summary>
        /// 実データのカード文字と、交換後の三つの配置を表示します。
        /// [KO] 실제 데이터 카드 글자와 교환 후 세 배치를 표시합니다.
        /// [EN] Displays data-backed hero cards and the three slots after swapping.
        /// </summary>
        void ShowFormation(FormationDisplay display);

        /// <summary>
        /// 調整側が処理中の間、全てのページ入力を止めます。
        /// [KO] 조정자가 처리 중인 동안 모든 페이지 입력을 멈춥니다.
        /// [EN] Disables all page input while the coordinator is busy.
        /// </summary>
        void SetInteractable(bool value);

        /// <summary>
        /// 開始失敗にだけ再試行ボタンを表示します。
        /// [KO] 시작 실패에만 재시도 버튼을 표시합니다.
        /// [EN] Shows the retry button only for startup failures.
        /// </summary>
        void SetErrorRetry(bool value);

        /// <summary>
        /// 通知を再発行せず音量表示を合わせます。
        /// [KO] 알림을 다시 발행하지 않고 음량 표시를 맞춥니다.
        /// [EN] Synchronizes volume display without re-emitting input.
        /// </summary>
        void SetVolume(float value);
    }
}
