using System;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 派生ボタンが共有するクリック接続を一度だけ作り、破棄時に自分の接続を解除します。
    /// [KO] 파생 버튼이 공유하는 클릭 연결을 한 번만 만들고 파괴 시 자신의 연결을 해제합니다.
    /// [EN] Connects the shared click listener once for derived buttons and removes its own connection on destruction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public class CommonButtonViewBase : MonoBehaviour
    {
        /// <summary>
        /// 同じ GameObject でクリック判定とキーボード入力を処理する Unity Button。
        /// [KO] 같은 GameObject에서 클릭 판정과 키보드 입력을 처리할 Unity Button입니다.
        /// [EN] Unity Button on the same GameObject that handles click detection and keyboard input.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Button _button;

        /// <summary>
        /// データを再設定してもクリック接続を重複させないための状態。
        /// [KO] 데이터를 재설정해도 클릭 연결을 중복하지 않기 위한 상태입니다.
        /// [EN] Tracks whether the click listener is connected to avoid duplicating it on rebinding.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// 派生 View の設定時に Button 参照を確認し、コードからクリックを一度だけ接続します。
        /// [KO] 파생 View 설정 시 Button 참조를 확인하고 코드에서 클릭을 한 번만 연결합니다.
        /// [EN] Validates the Button and connects clicks once from code when a derived view is configured.
        /// </summary>
        protected void InitializeButton()
        {
            if (_button == null || _button.gameObject != gameObject)
                throw new InvalidOperationException("Button view requires the Unity Button on the same GameObject.");

            if (_button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Button clicks must be connected from code; Inspector On Click must be empty.");

            if (_isInitialized)
                return;

            // Button の有効なクリックを派生 View へ直接渡し、演出の完了を待たせない。
            _button.onClick.AddListener(HandleClicked);
            _isInitialized = true;
        }

        /// <summary>
        /// コードから接続されたクリックに対して、派生クラスがボタン固有の処理を実装します。
        /// [KO] 코드에서 연결한 클릭에 대해 파생 클래스가 버튼 고유 처리를 구현합니다.
        /// [EN] Extension point where derived classes implement button-specific behavior for code-connected clicks.
        /// </summary>
        protected virtual void HandleClicked()
        {
        }

        /// <summary>
        /// この View が登録したクリックだけを解除します。派生 View は続けて固有の通知を解放します。
        /// [KO] 이 View가 등록한 클릭만 해제하며 파생 View는 이어서 고유 알림을 해제합니다.
        /// [EN] Removes only this view's listener; derived views then release their own notifications.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_button != null && _isInitialized)
                _button.onClick.RemoveListener(HandleClicked);

            _isInitialized = false;
        }
    }
}
