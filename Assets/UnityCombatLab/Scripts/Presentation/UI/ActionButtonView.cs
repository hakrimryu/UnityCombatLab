using System;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 画面から渡されたクリック処理を実行する、一般ボタン用の派生 View。
    /// [KO] 화면에서 전달받은 클릭 처리를 실행하는 일반 버튼용 파생 View입니다.
    /// [EN] Derived view for regular buttons that executes the click action supplied by its screen.
    /// </summary>
    public sealed class ActionButtonView : CommonButtonViewBase
    {
        /// <summary>
        /// このボタンを配置した画面が設定するクリック処理。
        /// [KO] 이 버튼을 배치한 화면이 설정할 클릭 처리입니다.
        /// [EN] Click action configured by the screen that owns this button.
        /// </summary>
        private Action _clickHandler;

        /// <summary>
        /// 画面のクリック処理を設定します。再設定時は前の処理を置き換え、接続は Base が一度だけ行います。
        /// [KO] 화면의 클릭 처리를 설정하며 재설정 시 이전 처리를 교체하고 연결은 Base가 한 번만 수행합니다.
        /// [EN] Sets or replaces the screen's click action while the base connects the listener only once.
        /// </summary>
        public void SetClickHandler(Action clickHandler)
        {
            if (clickHandler == null)
                throw new ArgumentNullException(nameof(clickHandler));

            InitializeButton();
            _clickHandler = clickHandler;
        }

        /// <summary>
        /// Base が受けたクリックから、配置先の画面が設定した処理を直ちに実行します。
        /// [KO] Base가 받은 클릭에서 배치된 화면이 설정한 처리를 즉시 실행합니다.
        /// [EN] Immediately executes the owning screen's action when the base receives a click.
        /// </summary>
        protected override void HandleClicked()
            => _clickHandler?.Invoke();

        /// <summary>
        /// Base のクリック接続を解除し、画面を参照する callback も解放します。
        /// [KO] Base 클릭 연결을 해제하고 화면을 참조하는 callback도 해제합니다.
        /// [EN] Removes the base listener and releases the callback referencing the owning screen.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            _clickHandler = null;
        }
    }
}
