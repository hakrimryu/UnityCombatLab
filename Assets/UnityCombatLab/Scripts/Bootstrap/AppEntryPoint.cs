using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Scenes;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// 共通サービスの初期化後、最初のシーンへの切替を開始します。
    /// [KO] 공통 서비스 초기화 후 첫 씬으로 전환을 시작합니다.
    /// [EN] Starts the initial scene transition after shared services are initialized.
    /// </summary>
    public sealed class AppEntryPoint : IAsyncStartable
    {
        /// <summary>
        /// App Scope が所有する共通シーン切替処理。
        /// [KO] App Scope가 소유하는 공통 씬 전환 처리입니다.
        /// [EN] The shared scene transition owned by the app scope.
        /// </summary>
        private readonly SceneTransitionManager _sceneTransition;

        /// <summary>
        /// 最初の Dungeon 読込が完了したかを示します。
        /// [KO] 첫 Dungeon 로드가 완료됐는지 나타냅니다.
        /// [EN] Whether the initial Dungeon load has completed.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// App Scope が所有するシーン切替を受け取り、起動時の Dungeon 読込に使います。
        /// [KO] App Scope 소유 씬 전환을 전달받아 시작 시 Dungeon을 로드하는 데 사용합니다.
        /// [EN] Receives app-owned scene transitions for loading Dungeon at startup.
        /// </summary>
        public AppEntryPoint(SceneTransitionManager sceneTransition)
        {
            _sceneTransition = sceneTransition ?? throw new System.ArgumentNullException(nameof(sceneTransition));
        }

        /// <summary>
        /// 起動要求の取消を確認して Dungeon を読み込み、完了後に IsStarted を設定します。
        /// [KO] 시작 요청의 취소를 확인하고 Dungeon을 로드한 뒤 IsStarted를 설정합니다.
        /// [EN] Checks startup cancellation, loads Dungeon and sets IsStarted after loading.
        /// </summary>
        /// <param name="cancellation">
        /// VContainer が開始処理の寿命に合わせて取り消すトークン。
        /// [KO] VContainer가 시작 처리 수명에 맞춰 취소하는 토큰입니다.
        /// [EN] The token cancelled by VContainer for startup lifetime.
        /// </param>
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();

            await _sceneTransition.LoadSceneAsync(Define.SceneType.Dungeon, cancellation);
            IsStarted = true;
        }
    }
}
