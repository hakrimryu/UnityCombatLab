using System;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Common;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 共通シーン切替中の入力を遮り、処理段階と待機表示を示します。
    /// [KO] 공통 씬 전환 중 입력을 막고 처리 단계와 대기 표시를 보여줍니다.
    /// [EN] Blocks input during shared scene transitions and displays the phase and waiting indicator.
    /// </summary>
    public sealed class LoadingView : MonoBehaviour, IRunTransitionView
    {
        /// <summary>
        /// 読込・終了・失敗の段階を表示する文字。
        /// [KO] 로딩·종료·실패 단계를 표시할 글자입니다.
        /// [EN] Text displaying loading, cleanup or failure phases.
        /// </summary>
        [SerializeField] private TMP_Text _message;

        /// <summary>
        /// 未完了の処理を示す回転表示。
        /// [KO] 미완료 처리를 나타내는 회전 표시입니다.
        /// [EN] Spinner indicating unfinished processing.
        /// </summary>
        [SerializeField] private RectTransform _spinner;

        /// <summary>
        /// この表示だけが停止する回転 Tween。
        /// [KO] 이 표시만 정지할 회전 Tween입니다.
        /// [EN] Rotation tween stopped only by this view.
        /// </summary>
        private Tween _rotation;

        /// <summary>
        /// App Scope が初期化時に呼び、案内と回転表示の必須参照を確認します。
        /// [KO] App Scope가 초기화 때 호출하여 안내와 회전 표시의 필수 참조를 확인합니다.
        /// [EN] Called by App Scope during initialization to validate the required message and spinner references.
        /// </summary>
        public void Validate()
        {
            if (_message == null)
                throw new InvalidOperationException("Loading view requires its message label.");

            if (_spinner == null)
                throw new InvalidOperationException("Loading view requires its spinner.");
        }

        /// <summary>
        /// 処理段階を表示し、画面復帰に失敗した場合は待機表示を止めます。
        /// [KO] 처리 단계를 표시하고 화면 복귀에 실패하면 대기 표시를 멈춥니다.
        /// [EN] Displays the phase and stops the waiting indicator when screen recovery fails.
        /// </summary>
        public void Show(Define.RunTransitionPhase phase)
        {
            Validate();
            gameObject.SetActive(true);
            _message.text = GameText.Transition(phase);
            var hasFailed = phase == Define.RunTransitionPhase.RecoveryFailed;
            _spinner.gameObject.SetActive(!hasFailed);
            _rotation?.Kill();
            _rotation = hasFailed ? null : UiTweenUtility.RotateContinuously(_spinner, 1.2f);
        }

        /// <summary>
        /// 完了した切替表示を閉じます。
        /// [KO] 완료된 전환 표시를 닫습니다.
        /// [EN] Hides the completed transition display.
        /// </summary>
        public void Hide()
            => gameObject.SetActive(false);

        /// <summary>
        /// 再表示や破棄で回転を残さないようにします。
        /// [KO] 다시 표시하거나 파괴할 때 회전을 남기지 않습니다.
        /// [EN] Stops rotation when hidden or destroyed.
        /// </summary>
        private void OnDisable()
        {
            _rotation?.Kill();
            _rotation = null;
        }
    }
}
