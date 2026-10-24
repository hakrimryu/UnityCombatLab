using UnityEngine.UI;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// 最小起動画面を初期化します。
    /// [KO] 최소 시작 화면을 초기화합니다.
    /// [EN] Initializes the minimal startup screen.
    /// </summary>
    public sealed class AppEntryPoint : IStartable
    {
        /// <summary>
        /// App Scope から借用するシーンの表示先。
        /// [KO] App Scope에서 빌려 쓰는 씬의 표시 대상입니다.
        /// [EN] The scene label borrowed from the app scope.
        /// </summary>
        private readonly Text _statusText;

        /// <summary>
        /// 起動処理が実行されたかを示します。
        /// [KO] 시작 처리가 실행됐는지 나타냅니다.
        /// [EN] Indicates whether startup has run.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// シーンの表示先を受け取ります。
        /// [KO] 씬의 표시 대상을 받습니다.
        /// [EN] Receives the scene's startup label.
        /// </summary>
        /// <param name="statusText">
        /// シーンが所有し、この処理では破棄しない表示先。
        /// [KO] 씬이 소유하며 이 처리에서 해제하지 않는 표시 대상입니다.
        /// [EN] The scene-owned label, which this entry point does not destroy.
        /// </param>
        public AppEntryPoint(Text statusText)
        {
            _statusText = statusText ?? throw new System.ArgumentNullException(nameof(statusText));
        }

        /// <summary>
        /// VContainer の起動完了を画面へ反映します。
        /// [KO] VContainer 시작 완료를 화면에 반영합니다.
        /// [EN] Displays successful VContainer startup.
        /// </summary>
        public void Start()
        {
            _statusText.text = "Unity Combat Lab\nReady";
            IsStarted = true;
        }
    }
}
