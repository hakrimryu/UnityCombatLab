using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Scenes;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// 最小起動画面を初期化します。
    /// [KO] 최소 시작 화면을 초기화합니다.
    /// [EN] Initializes the minimal startup screen.
    /// </summary>
    public sealed class AppEntryPoint : IAsyncStartable
    {
        /// <summary>
        /// AppRoot から借用する共通表示先。
        /// [KO] AppRoot에서 빌려 쓰는 공통 표시 대상입니다.
        /// [EN] The common label borrowed from AppRoot.
        /// </summary>
        private readonly Text _statusText;

        /// <summary>
        /// App Scope が所有する共通シーン切替処理。
        /// [KO] App Scope가 소유하는 공통 씬 전환 처리입니다.
        /// [EN] The shared scene transition owned by the app scope.
        /// </summary>
        private readonly SceneTransitionManager _sceneTransition;

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
        /// AppRoot が所有し、この処理では破棄しない表示先。
        /// [KO] AppRoot가 소유하며 이 처리에서 해제하지 않는 표시 대상입니다.
        /// [EN] The AppRoot-owned label, which this entry point does not destroy.
        /// </param>
        /// <param name="sceneTransition">
        /// App Scope と同じ寿命の切替境界。
        /// [KO] App Scope와 같은 수명의 전환 경계입니다.
        /// [EN] The transition boundary sharing the app scope's lifetime.
        /// </param>
        public AppEntryPoint(Text statusText, SceneTransitionManager sceneTransition)
        {
            _statusText = statusText ?? throw new System.ArgumentNullException(nameof(statusText));
            _sceneTransition = sceneTransition ?? throw new System.ArgumentNullException(nameof(sceneTransition));
        }

        /// <summary>
        /// 共通ルートを維持し、最初の舞台へ移動します。
        /// [KO] 공통 루트를 유지하고 첫 무대로 이동합니다.
        /// [EN] Preserves the app root and opens the first stage.
        /// </summary>
        /// <param name="cancellation">
        /// VContainer が開始処理の寿命に合わせて取り消すトークン。
        /// [KO] VContainer가 시작 처리 수명에 맞춰 취소하는 토큰입니다.
        /// [EN] The token cancelled by VContainer for startup lifetime.
        /// </param>
        /// <returns>
        /// 最初のシーン切替を待つ処理。
        /// [KO] 첫 씬 전환을 기다리는 작업입니다.
        /// [EN] The operation awaiting the initial scene transition.
        /// </returns>
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();

            _statusText.text = "Unity Combat Lab";
            await _sceneTransition.LoadSceneAsync(Define.SceneType.Dungeon, cancellation);
            _statusText.gameObject.SetActive(false);
            IsStarted = true;
        }
    }
}
