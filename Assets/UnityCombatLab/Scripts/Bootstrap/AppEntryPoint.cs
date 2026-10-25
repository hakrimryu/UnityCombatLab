using UnityCombatLab.Application.Run;
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
    public sealed class AppEntryPoint : IAsyncStartable, ITickable
    {
        /// <summary>
        /// 毎フレーム、保留中の戦闘結果を処理するApplicationの調整役。
        /// [KO] 매 프레임 대기 중 전투 결과를 처리할 Application 조정자입니다.
        /// [EN] Application coordinator that processes pending battle results each frame.
        /// </summary>
        private readonly RunFlowCoordinator _flow;

        /// <summary>
        /// 最初の Frontend が入力接続と初回描画まで完了したかを示します。
        /// [KO] 첫 Frontend가 입력 연결과 첫 표시까지 완료했는지 나타냅니다.
        /// [EN] Whether the initial Frontend completed input setup and its first render.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// App Scope の調整役を受け取り、起動とフレーム更新を接続します。
        /// [KO] App Scope의 조정자를 전달받아 시작과 프레임 갱신을 연결합니다.
        /// [EN] Receives the app-owned coordinator and connects startup and frame updates.
        /// </summary>
        public AppEntryPoint(RunFlowCoordinator flow)
        {
            _flow = flow ?? throw new System.ArgumentNullException(nameof(flow));
        }

        /// <summary>
        /// 起動要求の取消を確認して Frontend を読み込み、完了後に IsStarted を設定します。
        /// [KO] 시작 요청의 취소를 확인하고 Frontend를 로드한 뒤 IsStarted를 설정합니다.
        /// [EN] Checks startup cancellation, loads Frontend and sets IsStarted after loading.
        /// </summary>
        /// <param name="cancellation">
        /// VContainer が開始処理の寿命に合わせて取り消すトークン。
        /// [KO] VContainer가 시작 처리 수명에 맞춰 취소하는 토큰입니다.
        /// [EN] The token cancelled by VContainer for startup lifetime.
        /// </param>
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();

            // 六人分の攻撃と被害数字で既定の Sequence 50 枠を超えるため、戦闘前に容量を確保する。
            DG.Tweening.DOTween.SetTweensCapacity(200, 125);
            IsStarted = await _flow.StartFrontendAsync(cancellation);
        }
        /// <summary>
        /// VContainerのフレーム通知をApplicationのRun処理へ渡します。
        /// [KO] VContainer의 프레임 알림을 Application Run 처리로 넘깁니다.
        /// [EN] Forwards VContainer frame notifications to application run processing.
        /// </summary>
        public void Tick()
            => _flow.Tick();
    }
}
