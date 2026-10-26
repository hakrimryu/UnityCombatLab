using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Application.Run
{
    /// <summary>
    /// 共通サービスとして一つの RunSession を生成し、読込・戦闘・片付け・画面復帰を順番に実行します。
    /// [KO] 공통 서비스로 RunSession 하나를 생성하고 로딩·전투·정리·화면 복귀를 차례로 실행합니다.
    /// [EN] Creates one RunSession as a shared service and sequences loading, combat, cleanup and navigation.
    /// </summary>
    public sealed class RunFlowCoordinator : IDisposable
    {
        /// <summary>
        /// 非取消の Unity シーン読込を直列に実行する共通処理。
        /// [KO] 취소할 수 없는 Unity 씬 로드를 순차 실행하는 공통 처리입니다.
        /// [EN] Shared service serializing non-cancellable Unity scene loads.
        /// </summary>
        private readonly ISceneTransition _scenes;
        /// <summary>
        /// Run とは独立した共通切替表示。
        /// [KO] Run과 독립적인 공통 전환 표시입니다.
        /// [EN] Shared transition display independent of run lifetime.
        /// </summary>
        private readonly IRunTransitionView _loading;
        /// <summary>
        /// 出発ごとに独立した資産所有者を作ります。
        /// [KO] 출발마다 독립 자산 소유자를 만듭니다.
        /// [EN] Creates a separate resource owner for each departure.
        /// </summary>
        private readonly Func<IRunResources> _resourcesFactory;

        /// <summary>
        /// View破棄が完了する次のフレームまで待つ実行環境の処理。
        /// [KO] View 파괴가 완료되는 다음 프레임까지 기다리는 실행 환경 처리입니다.
        /// [EN] Runtime operation waiting until the next frame after view destruction.
        /// </summary>
        private readonly Func<UniTask> _nextFrame;

        /// <summary>
        /// 実行環境のログへ失敗を渡します。
        /// [KO] 실행 환경 로그로 실패를 전달합니다.
        /// [EN] Reports failures to the runtime log.
        /// </summary>
        private readonly Action<Exception> _reportFailure;

        /// <summary>
        /// 保存した共通音量を実際の音声出力へ反映します。
        /// [KO] 저장한 공통 음량을 실제 오디오 출력에 적용합니다.
        /// [EN] Applies remembered master volume to the actual audio output.
        /// </summary>
        private readonly Action<float> _applyVolume;

        /// <summary>
        /// 読込から片付けまで単独で所有する挑戦。
        /// [KO] 로딩부터 정리까지 단독 소유하는 도전입니다.
        /// [EN] Run exclusively owned from loading through cleanup.
        /// </summary>
        public RunSession ActiveSession { get; private set; }
        /// <summary>
        /// フレーム更新を受ける間も非同期切替を一つに保つロック。
        /// [KO] 프레임 갱신 중에도 비동기 전환을 하나로 유지하는 잠금입니다.
        /// [EN] Lock keeping asynchronous transitions exclusive across frame updates.
        /// </summary>
        private bool _isTransitioning;
        /// <summary>
        /// アプリ終了後の継続を拒否します。
        /// [KO] 앱 종료 후 이어지는 처리를 거부합니다.
        /// [EN] Rejects continuations after app shutdown.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 読込先の Frontend が入力接続と初回描画を完了したかを記録します。
        /// [KO] 로드한 Frontend가 입력 연결과 첫 표시를 마쳤는지 기록합니다.
        /// [EN] Records whether the loaded Frontend completed input setup and its first render.
        /// </summary>
        private bool _isFrontendReady;

        /// <summary>
        /// 読込先のEntryPointが登録する非同期UI準備。シーン読込後に一度だけ実行します。
        /// [KO] 로드 대상 EntryPoint가 등록할 비동기 UI 준비이며 씬 로드 후 한 번만 실행합니다.
        /// [EN] Async UI preparation registered by the destination entry point and run once after scene loading.
        /// </summary>
        private Func<CancellationToken, UniTask> _prepareFrontend;
        /// <summary>
        /// 画面入力が利用する純粋な状態遷移規則。
        /// [KO] 화면 입력에서 사용하는 순수 상태 전이 규칙입니다.
        /// [EN] Pure state-transition rules used by screen input.
        /// </summary>
        public RunController Controller { get; } = new();
        /// <summary>
        /// Frontend が表示する次の画面。
        /// [KO] Frontend에서 표시할 다음 화면입니다.
        /// [EN] Next screen displayed by Frontend.
        /// </summary>
        public Define.FrontendPage Page { get; private set; }
        /// <summary>
        /// 画面に通知する失敗箇所。
        /// [KO] 화면에 알릴 실패 지점입니다.
        /// [EN] Failure location reported to the screen.
        /// </summary>
        public Define.RunFailure Failure { get; private set; }
        /// <summary>
        /// 開始失敗なら同じ編成で再試行でき、進行中の失敗ではタイトル復帰だけを許可します。
        /// [KO] 시작 실패는 같은 편성으로 재시도할 수 있고 진행 중 실패는 타이틀 복귀만 허용합니다.
        /// [EN] Allows retry for startup failure and only title return for failure during combat.
        /// </summary>
        public bool CanRetry { get; private set; } = true;
        /// <summary>
        /// 片付け中または挑戦中には新しい出発を受けません。
        /// [KO] 정리 중이거나 도전 중이면 새 출발을 받지 않습니다.
        /// [EN] Rejects new departures during cleanup or an active run.
        /// </summary>
        public bool IsBusy => _isTransitioning || ActiveSession != null;
        /// <summary>
        /// アプリを閉じるまで維持する音量。
        /// [KO] 앱을 닫을 때까지 유지할 음량입니다.
        /// [EN] Volume retained until app shutdown.
        /// </summary>
        public float Volume { get; private set; } = 0.7f;

        /// <summary>
        /// 最後に完了した Run の開発用再現記録。
        /// [KO] 마지막으로 완료한 Run의 개발용 재현 기록입니다.
        /// [EN] Development replay record of the most recently completed run.
        /// </summary>
        public RunReplayRecord LastReplay { get; private set; }
        /// <summary>
        /// Frontend の所有者へ、画面と入力可否の変化を通知します。
        /// [KO] Frontend 소유자에게 화면과 입력 가능 상태의 변경을 알립니다.
        /// [EN] Notifies the Frontend owner of screen and input availability changes.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// App Scopeから実行環境の処理を受け取り、初期の表示と音量を設定します。
        /// [KO] App Scope에서 실행 환경 처리를 받아 초기 표시와 음량을 설정합니다.
        /// [EN] Receives runtime operations from App Scope and initializes display and volume.
        /// </summary>
        public RunFlowCoordinator(ISceneTransition scenes, IRunTransitionView loading,
            Func<IRunResources> resourcesFactory, Func<UniTask> nextFrame,
            Action<Exception> reportFailure, Action<float> applyVolume)
        {
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _loading = loading ?? throw new ArgumentNullException(nameof(loading));
            _resourcesFactory = resourcesFactory ?? throw new ArgumentNullException(nameof(resourcesFactory));
            _nextFrame = nextFrame ?? throw new ArgumentNullException(nameof(nextFrame));
            _reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
            _applyVolume = applyVolume ?? throw new ArgumentNullException(nameof(applyVolume));
            _loading.Hide();
            _applyVolume(Volume);
        }

        /// <summary>
        /// 実行中の挑戦がなければタイトルか編成へ移動します。
        /// [KO] 실행 중인 도전이 없으면 타이틀이나 편성으로 이동합니다.
        /// [EN] Navigates to title or formation while no run is active.
        /// </summary>
        public void Navigate(Define.FrontendPage page)
        {
            if (IsBusy || _isDisposed || (page != Define.FrontendPage.Title && page != Define.FrontendPage.Formation))
                return;

            Page = page;
            Failure = Define.RunFailure.None;
            Changed?.Invoke();
        }

        /// <summary>
        /// 共通マスター音量とスライダー表示を更新します。
        /// [KO] 공통 마스터 음량과 슬라이더 표시를 갱신합니다.
        /// [EN] Updates shared master volume and slider presentation.
        /// </summary>
        public void SetVolume(float value)
        {
            if (_isDisposed || float.IsNaN(value) || float.IsInfinity(value))
                return;

            Volume = Math.Max(0f, Math.Min(1f, value));
            _applyVolume(Volume);
        }

        /// <summary>
        /// Boot から Frontend を開き、画面の初期化に失敗した場合は共通表示で案内します。
        /// [KO] Boot에서 Frontend를 열고 화면 초기화 실패 시 공통 표시로 안내합니다.
        /// [EN] Opens Frontend from Boot and reports screen initialization failures through the shared display.
        /// </summary>
        public async UniTask<bool> StartFrontendAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isDisposed || IsBusy)
                return false;

            _isTransitioning = true;
            var returned = false;
            try
            {
                await LoadFrontendAsync(cancellationToken);
                returned = !_isDisposed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _reportFailure(exception);
            }
            finally
            {
                _isTransitioning = false;
                if (!_isDisposed && !cancellationToken.IsCancellationRequested)
                    returned = CompleteFrontendTransition(returned);
            }

            return returned;
        }

        /// <summary>
        /// Frontend EntryPoint が初回表示に成功した後に、読込側へ準備完了を伝えます。
        /// [KO] Frontend EntryPoint가 첫 표시에 성공한 뒤 로딩 측에 준비 완료를 알립니다.
        /// [EN] Called by the Frontend entry point after a successful first render to signal readiness to the loader.
        /// </summary>
        public void MarkFrontendReady()
        {
            if (!_isDisposed)
                _isFrontendReady = true;
        }

        /// <summary>
        /// FrontendのEntryPointから資産読込と初回描画を受け取り、共通ローディングの待機対象にします。
        /// [KO] Frontend EntryPoint의 자산 로딩·첫 표시를 받아 공통 로딩이 기다리도록 합니다.
        /// [EN] Accepts asset loading and initial rendering from Frontend so the shared transition waits for them.
        /// </summary>
        public void RegisterFrontendPreparation(Func<CancellationToken, UniTask> prepare)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RunFlowCoordinator));

            if (!_isTransitioning || _prepareFrontend != null || _isFrontendReady)
                throw new InvalidOperationException("Frontend preparation must be registered once during scene loading.");

            _prepareFrontend = prepare ?? throw new ArgumentNullException(nameof(prepare));
        }

        /// <summary>
        /// シーン・EntryPointの開始・非同期UI準備を待ち、利用可能な画面だけを成功とします。
        /// [KO] 씬·EntryPoint 시작·비동기 UI 준비를 기다리고 사용 가능한 화면만 성공으로 판단합니다.
        /// [EN] Waits for the scene, entry point and async UI preparation and accepts only a usable screen.
        /// </summary>
        private async UniTask LoadFrontendAsync(CancellationToken cancellationToken = default)
        {
            _isFrontendReady = false;
            _prepareFrontend = null;
            await _scenes.LoadSceneAsync(Define.SceneType.Frontend, cancellationToken);
            await _nextFrame();
            cancellationToken.ThrowIfCancellationRequested();

            var prepare = _prepareFrontend;
            _prepareFrontend = null;
            if (!_isDisposed && prepare != null)
                await prepare(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (!_isDisposed && !_isFrontendReady)
                throw new InvalidOperationException("Frontend did not finish input setup and its initial render.");
        }

        /// <summary>
        /// 出発を一度受け付け、資産と Dungeon を読み込んでから戦闘を開始します。
        /// [KO] 출발을 한 번 받고 자산과 Dungeon을 로드한 뒤 전투를 시작합니다.
        /// [EN] Accepts departure once, loads assets and Dungeon, then starts combat.
        /// </summary>
        public async UniTask StartRunAsync(RunConfiguration configuration)
        {
            if (_isDisposed || IsBusy)
                return;

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var resources = _resourcesFactory() ?? throw new InvalidOperationException("Run resource factory returned null.");

            if (!Controller.TryStart(configuration))
            {
                resources.Dispose();
                return;
            }

            _isTransitioning = true;
            var session = new RunSession(Controller.Current, resources);
            ActiveSession = session;
            // TryStart が前回結果を消した後は、結果画面を再描画せず読込表示の後ろを編成画面に戻す。
            Page = Define.FrontendPage.Formation;
            try
            {
                Changed?.Invoke();
                _loading.Show(Define.RunTransitionPhase.LoadingDungeon);
                await session.Resources.PrepareAsync(session.Token);
                await _scenes.LoadSceneAsync(Define.SceneType.Dungeon, session.Token);
                await _nextFrame();
                if (_isDisposed)
                    return;

                session.Token.ThrowIfCancellationRequested();

                if (session.Battle == null)
                    throw new InvalidOperationException("Dungeon did not attach a battle to the active run.");

                session.IsBattleAllowed = true;
                session.Battle.Start();

                if (!Controller.TryEnterBattle(session.InitialState.Id))
                    throw new InvalidOperationException("The initialized battle does not match the loading run.");

                _isTransitioning = false;
                _loading.Hide();
            }
            catch (OperationCanceledException) when (session.Token.IsCancellationRequested)
            {
                await EndRunAsync(session);
            }
            catch (Exception exception)
            {
                _reportFailure(exception);
                await EndRunAsync(session, exception);
            }
        }

        /// <summary>
        /// 最終イベントの呼出しが戻った後のフレームで、結果か失敗を終了処理へ渡します。
        /// [KO] 마지막 이벤트 호출이 반환된 뒤의 프레임에서 결과나 실패를 종료 처리에 전달합니다.
        /// [EN] Forwards results or failures to cleanup on a frame after final event delivery returns.
        /// </summary>
        public void Tick()
        {
            if (_isDisposed || _isTransitioning || ActiveSession == null)
                return;

            if (ActiveSession.PendingResult == null && ActiveSession.Failure == null)
                return;

            _isTransitioning = true;

            if (ActiveSession.Failure != null)
                _reportFailure(ActiveSession.Failure);

            EndRunAsync(ActiveSession, ActiveSession.Failure).Forget();
        }

        /// <summary>
        /// 一時停止中の終了確認を受け、勝敗を作らずタイトルへ戻します。
        /// [KO] 일시정지 중 종료 확인을 받아 승패를 만들지 않고 타이틀로 돌아갑니다.
        /// [EN] Accepts exit confirmation from pause and returns to Title without recording an outcome.
        /// </summary>
        public void RequestExitToTitle(Guid runId)
        {
            if (_isDisposed || _isTransitioning || ActiveSession?.InitialState.Id != runId ||
                !Controller.IsPaused || !Controller.IsExitConfirmationOpen)
                return;

            ActiveSession.ShouldReturnToTitle = true;
            EndRunAsync(ActiveSession).Forget();
        }

        /// <summary>
        /// Run を停止し、表示破棄の次フレームに資産を解放して Frontend へ戻ります。
        /// [KO] Run을 정지하고 표시 파괴 다음 프레임에 자산을 해제하여 Frontend로 돌아갑니다.
        /// [EN] Stops the run, releases assets the frame after destroying views and returns to Frontend.
        /// </summary>
        private async UniTask EndRunAsync(RunSession session, Exception failure = null)
        {
            if (_isDisposed || ActiveSession != session)
                return;

            _isTransitioning = true;
            var result = failure == null ? session.PendingResult : null;
            var failedDuringBattle = failure != null && Controller.Current?.Phase != Define.RunPhase.Loading;
            var returned = false;
            try
            {
                if (!Controller.TryBeginCleanup(session.InitialState.Id, result?.Outcome ?? Define.BattleResult.Ongoing, result?.Ticks ?? 0))
                {
                    failure = new InvalidOperationException("The queued result does not match the active run completion.");
                    _reportFailure(failure);
                    result = null;
                    failedDuringBattle = true;
                    Controller.TryBeginCleanup(session.InitialState.Id);
                }

                if (result != null)
                    LastReplay = session.Replay;

                session.Cancel();
                ShowTransitionSafely(Define.RunTransitionPhase.CleaningUp);
                session.Battle?.Dispose();
                // Destroyはフレーム末尾に完了する。HUD・キャラクター・子の効果プールが消えてから資産を解放する。
                await _nextFrame();
                if (_isDisposed)
                    return;

                session.Resources.Dispose();
                Page = failure != null ? Define.FrontendPage.Error : result != null ? Define.FrontendPage.Results :
                    session.ShouldReturnToTitle ? Define.FrontendPage.Title : Define.FrontendPage.Formation;
                CanRetry = !failedDuringBattle;
                Failure = failure == null ? Define.RunFailure.None : failedDuringBattle
                    ? Define.RunFailure.Battle
                    : Define.RunFailure.Startup;
                await LoadFrontendAsync();
                returned = !_isDisposed;
            }
            catch (Exception exception)
            {
                _reportFailure(exception);
                Page = Define.FrontendPage.Error;
                Failure = Define.RunFailure.Frontend;
            }
            finally
            {
                try
                {
                    session.Dispose();
                }
                catch (Exception exception)
                {
                    _reportFailure(exception);
                    returned = false;
                }
                finally
                {
                    Controller.TryCompleteCleanup(session.InitialState.Id);
                    if (ActiveSession == session)
                        ActiveSession = null;

                    _isTransitioning = false;

                    if (!_isDisposed)
                        CompleteFrontendTransition(returned);
                }
            }
        }

        /// <summary>
        /// 準備済み画面を更新してローディングを閉じ、画面処理の失敗は共通の再起動案内へ渡します。
        /// [KO] 준비된 화면을 갱신하고 로딩을 닫으며 화면 처리 실패는 공통 재시작 안내로 전달합니다.
        /// [EN] Refreshes the ready screen and hides loading, routing display failures to the shared restart message.
        /// </summary>
        private bool CompleteFrontendTransition(bool returned)
        {
            if (returned)
            {
                try
                {
                    Changed?.Invoke();
                    _loading.Hide();
                    return true;
                }
                catch (Exception exception)
                {
                    _reportFailure(exception);
                }
            }

            Page = Define.FrontendPage.Error;
            Failure = Define.RunFailure.Frontend;
            CanRetry = false;
            ShowTransitionSafely(Define.RunTransitionPhase.RecoveryFailed);
            return false;
        }

        /// <summary>
        /// 共通表示自体が壊れていても、失敗を記録して Run の片付けを続けます。
        /// [KO] 공통 표시 자체가 손상되어도 실패를 기록하고 Run 정리를 계속합니다.
        /// [EN] Logs failures of the shared display itself while allowing run cleanup to continue.
        /// </summary>
        private void ShowTransitionSafely(Define.RunTransitionPhase phase)
        {
            try
            {
                _loading.Show(phase);
            }
            catch (Exception exception)
            {
                _reportFailure(exception);
            }
        }

        /// <summary>
        /// アプリ終了時に進行中の挑戦を取り消し、戦闘表示と通知を解放します。
        /// [KO] 앱 종료 시 진행 중인 도전을 취소하고 전투 표시와 알림을 해제합니다.
        /// [EN] Cancels the active run and releases battle presentation and notifications on app shutdown.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _prepareFrontend = null;
            var session = ActiveSession;
            ActiveSession = null;
            session?.Cancel();
            session?.Battle?.Dispose();
            // HUD・キャラクターを破棄するフレームと資産の解放を分ける。
            DisposeSessionAsync(session).Forget();
            Changed = null;
        }

        /// <summary>
        /// アプリ終了でも生成済み HUD の破棄後に Run 資産を解放します。
        /// [KO] 앱 종료 시에도 생성된 HUD 파괴 후 Run 자산을 해제합니다.
        /// [EN] Releases run assets after generated HUD destruction during app shutdown as well.
        /// </summary>
        private async UniTask DisposeSessionAsync(RunSession session)
        {
            if (session == null)
                return;

            await _nextFrame();
            session.Dispose();
        }
    }
}
