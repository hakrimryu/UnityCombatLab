using System;
using System.Threading;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Application.Ports;

namespace UnityCombatLab.Application.Run
{
    /// <summary>
    /// ロード開始から終了処理まで、一回の挑戦の取消・資産・戦闘接続を所有します。
    /// [KO] 로딩 시작부터 종료 처리까지 한 번의 도전 취소·자산·전투 연결을 소유합니다.
    /// [EN] Owns one run's cancellation, assets and combat connection from loading through cleanup.
    /// </summary>
    public sealed class RunSession : IDisposable
    {
        /// <summary>
        /// この挑戦の読み込みを取り消す所有元。
        /// [KO] 이번 도전의 로딩을 취소하는 소유자입니다.
        /// [EN] Owned cancellation source for this run's loads.
        /// </summary>
        private readonly CancellationTokenSource _cancellation = new();

        /// <summary>
        /// 取消元の破棄後も継続処理が取消状態を読めるよう、開始時に保存するトークン。
        /// [KO] 취소 원본 해제 뒤에도 이어지는 처리에서 취소 상태를 읽도록 시작 시 저장하는 토큰입니다.
        /// [EN] Token cached at startup so late continuations can inspect cancellation after source disposal.
        /// </summary>
        private readonly CancellationToken _token;

        /// <summary>
        /// 出発時の識別子と不変設定。
        /// [KO] 출발 시 식별자와 불변 설정입니다.
        /// [EN] Identity and immutable configuration fixed at departure.
        /// </summary>
        public RunState InitialState { get; }

        /// <summary>
        /// この挑戦に必要な資産を読み込み、終了時に解放する所有者。
        /// [KO] 이번 도전에 필요한 자산을 로드하고 종료 시 해제할 소유자입니다.
        /// [EN] Owner that loads and releases resources for this run.
        /// </summary>
        public IRunResources Resources { get; }

        /// <summary>
        /// 読み込みに渡す取消トークン。
        /// [KO] 로딩에 전달할 취소 토큰입니다.
        /// [EN] Cancellation token passed to loading operations.
        /// </summary>
        public CancellationToken Token => _token;

        /// <summary>
        /// 検証とシーン読込を終えて、戦闘開始が許可されたか。
        /// [KO] 검증과 씬 로드를 마치고 전투 시작이 허용됐는지 나타냅니다.
        /// [EN] Whether validation and scene loading have permitted combat to start.
        /// </summary>
        public bool IsBattleAllowed { get; internal set; }

        /// <summary>
        /// Dungeon が生成し、このセッションへ一度接続する戦闘。
        /// [KO] Dungeon이 생성하고 이 세션에 한 번 연결하는 전투입니다.
        /// [EN] Battle created by Dungeon and attached to this session once.
        /// </summary>
        public IRunBattle Battle { get; private set; }

        /// <summary>
        /// 最後の演出を終えた後、次の調整フレームへ渡す確定結果。
        /// [KO] 마지막 연출 완료 후 다음 조정 프레임에 전달할 확정 결과입니다.
        /// [EN] Committed result queued for the coordinator after final presentation completes.
        /// </summary>
        public RunResult PendingResult { get; private set; }

        /// <summary>
        /// 終了時にコピーした再現記録。Unity 資産参照は持ちません。
        /// [KO] 종료 시 복사한 재현 기록이며 Unity 자산 참조가 없습니다.
        /// [EN] Replay record copied at completion without Unity asset references.
        /// </summary>
        public RunReplayRecord Replay { get; private set; }

        /// <summary>
        /// 明示終了後の復帰先をタイトルにする要求。
        /// [KO] 명시 종료 후 복귀 위치를 타이틀로 지정하는 요청입니다.
        /// [EN] Request to return to Title after explicit exit.
        /// </summary>
        public bool ShouldReturnToTitle { get; internal set; }

        /// <summary>
        /// 戦闘更新の失敗を終了処理へ渡します。
        /// [KO] 전투 갱신 실패를 종료 처리에 전달합니다.
        /// [EN] Combat-update failure queued for cleanup.
        /// </summary>
        public Exception Failure { get; private set; }

        /// <summary>
        /// 二重破棄と古い通知を拒否します。
        /// [KO] 이중 파괴와 이전 알림을 거부합니다.
        /// [EN] Rejects duplicate disposal and stale notifications.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// RunController が作った開始状態を受け取ります。
        /// [KO] RunController가 만든 시작 상태를 받습니다.
        /// [EN] Receives the initial state created by RunController.
        /// </summary>
        public RunSession(RunState initialState, IRunResources resources)
        {
            InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _token = _cancellation.Token;
        }

        /// <summary>
        /// Dungeon の生成した戦闘を同じ挑戦へ接続します。
        /// [KO] Dungeon에서 생성한 전투를 같은 도전에 연결합니다.
        /// [EN] Attaches the Dungeon-created battle to this run.
        /// </summary>
        public void AttachBattle(IRunBattle battle)
        {
            if (_isDisposed || Battle != null)
                throw new InvalidOperationException("A run can attach one battle.");

            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        /// <summary>
        /// 同期イベントの外で終了できるよう、計算結果だけを保存します。
        /// [KO] 동기 이벤트 밖에서 종료할 수 있도록 계산 결과만 저장합니다.
        /// [EN] Stores only result values so cleanup can run outside synchronous event delivery.
        /// </summary>
        public void QueueResult(Define.BattleResult outcome, int ticks)
        {
            if (_isDisposed || !IsBattleAllowed || PendingResult != null || Failure != null)
                return;

            PendingResult = new RunResult(InitialState.Id, InitialState.Configuration, outcome, ticks);
        }

        /// <summary>
        /// 全ウェーブ集計済みの結果と再現記録を一度だけ受け取ります。
        /// [KO] 모든 웨이브를 집계한 결과·재현 기록을 한 번만 받습니다.
        /// [EN] Accepts the fully aggregated result and replay record once.
        /// </summary>
        public void QueueResult(RunResult result, RunReplayRecord replay)
        {
            if (_isDisposed || !IsBattleAllowed || PendingResult != null || Failure != null)
                return;

            if (result == null || replay == null || result.RunId != InitialState.Id || replay.Result != result)
                throw new ArgumentException("Result and replay must belong to this run.");

            PendingResult = result;
            Replay = replay;
        }

        /// <summary>
        /// 継続できない失敗を Run の終了処理へ通知します。
        /// [KO] 진행할 수 없는 실패를 Run 종료 처리에 알립니다.
        /// [EN] Reports an unrecoverable failure to run cleanup.
        /// </summary>
        public void QueueFailure(Exception exception)
        {
            if (!_isDisposed)
                Failure ??= exception;
        }

        /// <summary>
        /// 新しい戦闘開始を止め、内部ロードに取消を通知します。
        /// [KO] 새 전투 시작을 막고 내부 로드에 취소를 알립니다.
        /// [EN] Blocks combat startup and requests loading cancellation.
        /// </summary>
        public void Cancel()
        {
            IsBattleAllowed = false;
            if (!_isDisposed)
                _cancellation.Cancel();
        }

        /// <summary>
        /// 戦闘と HUD の破棄後に、資産と取消元を解放します。
        /// [KO] 전투와 HUD 파괴 후 자산과 취소 원본을 해제합니다.
        /// [EN] Releases assets and the cancellation source after battle and HUD destruction.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            Cancel();
            _isDisposed = true;

            try
            {
                Resources.Dispose();
            }
            finally
            {
                _cancellation.Dispose();
                Battle = null;
            }
        }
    }
}
