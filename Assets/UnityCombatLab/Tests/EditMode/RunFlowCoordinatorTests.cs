using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// Runの失敗・取消・終了を実行環境のテスト用実装で確認します。
    /// [KO] 테스트 실행 환경으로 Run 실패·취소·종료를 확인합니다.
    /// [EN] Verifies run failure, cancellation and cleanup with a controlled runtime.
    /// </summary>
    public sealed class RunFlowCoordinatorTests
    {
        /// <summary>
        /// 終了時にView、次フレーム、資産、Frontendの順序を維持します。
        /// [KO] 종료 시 View·다음 프레임·자산·Frontend 순서를 유지하는지 확인합니다.
        /// [EN] Checks cleanup order: battle views, next frame, resources, then Frontend.
        /// </summary>
        [Test]
        public void OutcomeCleanupPreservesLifetimeOrder()
        {
            var rig = new FlowRig();
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            Assert.That(rig.Battle.Starts, Is.EqualTo(1));
            rig.Events.Clear();
            flow.ActiveSession.QueueResult(Define.BattleResult.Victory, 140);
            flow.Tick();
            Assert.That(rig.Events, Is.EqualTo(new[] { "battle-dispose", "frame", "assets-dispose", "frontend", "frame" }));
            Assert.That(flow.Page, Is.EqualTo(Define.FrontendPage.Results));
            Assert.That(flow.Controller.Result.Ticks, Is.EqualTo(140));
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.IsBusy, Is.False);
        }
        /// <summary>
        /// 開始失敗は資産を解放し、再試行を許可して新しいRunを作ります。
        /// [KO] 시작 실패는 자산을 해제하고 재시도를 허용해 새 Run을 만드는지 확인합니다.
        /// [EN] Checks startup failure releases resources and permits a fresh run on retry.
        /// </summary>
        [Test]
        public void StartupFailureCanRetryWithFreshResources()
        {
            var rig = new FlowRig { ShouldFailPreparation = true };
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Startup));
            Assert.That(flow.CanRetry, Is.True);
            Assert.That(rig.Resources.IsDisposed, Is.True);
            var previous = rig.Resources;
            rig.ShouldFailPreparation = false;
            flow.StartRunAsync(flow.Controller.LastConfiguration).GetAwaiter().GetResult();
            Assert.That(rig.Resources, Is.Not.SameAs(previous));
            Assert.That(flow.Controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
        }
        /// <summary>
        /// 取消の直後には次の出発を許可せず、保留中ロードの完了後に片付けます。
        /// [KO] 취소 직후 다음 출발을 허용하지 않고 대기 중 로드 완료 후 정리하는지 확인합니다.
        /// [EN] Keeps departure locked after cancellation until the pending load really completes.
        /// </summary>
        [Test]
        public void CancellationWaitsForPendingLoadAndRejectsDuplicateDeparture()
        {
            var rig = new FlowRig { Preparation = new UniTaskCompletionSource() };
            using var flow = rig.Create();
            var start = flow.StartRunAsync(RunConfiguration.CreateDefault());
            var session = flow.ActiveSession;
            session.Cancel();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            Assert.That(flow.ActiveSession, Is.SameAs(session));
            Assert.That(rig.Resources.IsDisposed, Is.False);
            rig.Preparation.TrySetResult();
            start.GetAwaiter().GetResult();
            Assert.That(rig.Battle, Is.Null);
            Assert.That(rig.Resources.IsDisposed, Is.True);
            Assert.That(flow.Page, Is.EqualTo(Define.FrontendPage.Formation));
        }
        /// <summary>
        /// 復帰ロードを待つ間に終了しても表示を再操作しません。
        /// [KO] 복귀 로드 대기 중 종료해도 표시를 다시 조작하지 않는지 확인합니다.
        /// [EN] Does not touch transition UI when disposed during a pending Frontend return.
        /// </summary>
        [Test]
        public void FrontendReturnAfterDisposalDoesNotTouchView()
        {
            var rig = new FlowRig { FrontendLoad = new UniTaskCompletionSource() };
            var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            flow.ActiveSession.QueueResult(Define.BattleResult.Victory, 140);
            flow.Tick();
            var calls = rig.View.Calls;
            flow.Dispose();
            rig.FrontendLoad.TrySetResult();
            Assert.That(rig.View.Calls, Is.EqualTo(calls));
            Assert.That(flow.ActiveSession, Is.Null);
        }
        /// <summary>
        /// 復帰失敗でもRunを解放し、待機ではなく失敗の案内を残します。
        /// [KO] 복귀 실패 시에도 Run을 해제하고 대기 대신 실패 안내를 남기는지 확인합니다.
        /// [EN] Checks that a failed return releases the run and leaves failure guidance instead of loading.
        /// </summary>
        [Test]
        public void FrontendReturnFailureReleasesRunAndShowsFailure()
        {
            var rig = new FlowRig { ShouldFailFrontend = true };
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            flow.ActiveSession.QueueResult(Define.BattleResult.Victory, 140);
            flow.Tick();
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Frontend));
            Assert.That(rig.View.LastPhase, Is.EqualTo(Define.RunTransitionPhase.RecoveryFailed));
            Assert.That(rig.Resources.IsDisposed, Is.True);
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.IsBusy, Is.False);
        }
        /// <summary>
        /// 戦闘中の失敗は結果を作らず再試行を禁止します。
        /// [KO] 전투 중 실패는 결과를 만들지 않고 재시도를 금지하는지 확인합니다.
        /// [EN] A combat failure produces no result and disables direct retry.
        /// </summary>
        [Test]
        public void BattleFailureDoesNotOfferStartupRetry()
        {
            var rig = new FlowRig();
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            flow.ActiveSession.QueueFailure(new InvalidOperationException("battle"));
            flow.Tick();
            Assert.That(flow.CanRetry, Is.False);
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Battle));
            Assert.That(flow.Controller.Result, Is.Null);
        }
        /// <summary>
        /// 共通表示が開始と片付けの両方で失敗しても、Run と入力ロックを残しません。
        /// [KO] 공통 표시가 시작과 정리 모두에서 실패해도 Run과 입력 잠금을 남기지 않습니다.
        /// [EN] Leaves no run or input lock when the shared display fails during both startup and cleanup.
        /// </summary>
        [Test]
        public void BrokenLoadingDisplayCannotPreventCleanup()
        {
            var rig = new FlowRig();
            using var flow = rig.Create();
            rig.View.ShouldFailShow = true;
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            Assert.That(rig.Resources.IsDisposed, Is.True);
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.Controller.Current, Is.Null);
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Startup));
        }

        /// <summary>
        /// 戦闘の初期化例外を開始失敗とし、新しい資産での再試行を許可します。
        /// [KO] 전투 초기화 예외를 시작 실패로 분류하고 새 자원으로 재시도를 허용합니다.
        /// [EN] Classifies battle initialization exceptions as startup failures and allows a fresh-resource retry.
        /// </summary>
        [Test]
        public void BattleStartFailureCanRetry()
        {
            var rig = new FlowRig { ShouldFailBattleStart = true };
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Startup));
            Assert.That(flow.CanRetry, Is.True);
            Assert.That(rig.Resources.IsDisposed, Is.True);
            Assert.That(flow.Controller.Current, Is.Null);
            var previous = rig.Resources;
            rig.ShouldFailBattleStart = false;
            flow.StartRunAsync(flow.Controller.LastConfiguration).GetAwaiter().GetResult();
            Assert.That(rig.Resources, Is.Not.SameAs(previous));
            Assert.That(flow.Controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
        }

        /// <summary>
        /// 起動と戦闘後の両方で、シーン読込だけでは画面準備成功と判断しません。
        /// [KO] 시작과 전투 후 모두 씬 로드만으로 화면 준비 성공으로 판단하지 않습니다.
        /// [EN] Requires screen readiness beyond scene loading at both startup and post-battle return.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void FrontendMustReportSuccessfulInitialization(bool isStartup)
        {
            var rig = new FlowRig { ShouldSkipFrontendReady = true };
            using var flow = rig.Create();
            if (isStartup)
                Assert.That(flow.StartFrontendAsync().GetAwaiter().GetResult(), Is.False);
            else
            {
                flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
                flow.ActiveSession.QueueResult(Define.BattleResult.Victory, 140);
                flow.Tick();
                Assert.That(rig.Resources.IsDisposed, Is.True);
            }

            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Frontend));
            Assert.That(rig.View.LastPhase, Is.EqualTo(Define.RunTransitionPhase.RecoveryFailed));
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.Controller.Current, Is.Null);
            Assert.That(flow.IsBusy, Is.False);
        }

        /// <summary>
        /// 復帰後の画面更新に失敗しても、共通の失敗案内を表示してロックを解放します。
        /// [KO] 복귀 후 화면 갱신에 실패해도 공통 실패 안내를 표시하고 잠금을 해제합니다.
        /// [EN] Shows shared failure feedback and releases the lock when post-return screen refresh fails.
        /// </summary>
        [Test]
        public void FinalFrontendRefreshFailureDoesNotLeaveRunLocked()
        {
            var rig = new FlowRig();
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            flow.Changed += () => throw new InvalidOperationException("frontend-render");
            flow.ActiveSession.QueueResult(Define.BattleResult.Victory, 140);
            flow.Tick();
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Frontend));
            Assert.That(rig.View.LastPhase, Is.EqualTo(Define.RunTransitionPhase.RecoveryFailed));
        }

        /// <summary>
        /// 資産解放の例外でも Run 状態とセッションを残しません。
        /// [KO] 자원 해제 예외가 발생해도 Run 상태와 세션을 남기지 않습니다.
        /// [EN] Clears run state and session even if resource disposal throws.
        /// </summary>
        [Test]
        public void ResourceDisposalFailureStillClearsRunState()
        {
            var rig = new FlowRig { ShouldFailDisposal = true };
            using var flow = rig.Create();
            flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            flow.ActiveSession.QueueResult(Define.BattleResult.Victory, 140);
            flow.Tick();
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.Controller.Current, Is.Null);
            Assert.That(flow.Failure, Is.EqualTo(Define.RunFailure.Frontend));
        }

        /// <summary>
        /// Unityを使わず、シーン接続と資産解放の順序を記録する実行環境。
        /// [KO] Unity 없이 씬 연결과 자산 해제 순서를 기록하는 실행 환경입니다.
        /// [EN] Test runtime recording scene attachment and resource release without Unity.
        /// </summary>
        private sealed class FlowRig : ISceneTransition
        {
            public readonly List<string> Events = new();
            public readonly FakeTransitionView View = new();
            public FakeResources Resources;
            public FakeBattle Battle;
            public bool ShouldFailPreparation;
            public bool ShouldFailFrontend;

            /// <summary>
            /// 資産読込後の戦闘初期化を失敗させます。
            /// [KO] 자원 로드 후 전투 초기화를 실패시킵니다.
            /// [EN] Fails battle initialization after resource loading.
            /// </summary>
            public bool ShouldFailBattleStart;

            /// <summary>
            /// シーンは読めても画面が準備できない状態を再現します。
            /// [KO] 씬은 로드했지만 화면 준비가 되지 않은 상태를 재현합니다.
            /// [EN] Represents a loaded scene whose screen is not ready.
            /// </summary>
            public bool ShouldSkipFrontendReady;

            /// <summary>
            /// 資産解放の例外が Run のロック解除を妨げないかを確認します。
            /// [KO] 자원 해제 예외가 Run 잠금 해제를 막지 않는지 확인합니다.
            /// [EN] Exercises resource-disposal failure while checking that the run lock is released.
            /// </summary>
            public bool ShouldFailDisposal;
            public UniTaskCompletionSource Preparation;
            public UniTaskCompletionSource FrontendLoad;
            private RunFlowCoordinator _flow;
            /// <summary>
            /// 記録用の依存先を接続したRun調整役を作ります。
            /// [KO] 기록용 의존 대상을 연결한 Run 조정자를 만듭니다.
            /// [EN] Creates the run coordinator with recording dependencies.
            /// </summary>
            public RunFlowCoordinator Create()
            {
                _flow = new RunFlowCoordinator
                (this,
                    View,
                    () => Resources = new FakeResources(this),
                    () =>
                    {
                        Events.Add("frame");
                        return UniTask.CompletedTask;
                    },
                    _ => { },
                    _ => { });

                return _flow;
            }
            /// <summary>
            /// Dungeonなら戦闘を接続し、Frontendなら指定された完了待機を返します。
            /// [KO] Dungeon이면 전투를 연결하고 Frontend이면 지정된 완료 대기를 반환합니다.
            /// [EN] Attaches combat for Dungeon or returns the controlled completion wait for Frontend.
            /// </summary>
            public UniTask LoadSceneAsync(Define.SceneType scene, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scene == Define.SceneType.Dungeon)
                {
                    Battle = new FakeBattle(Events, ShouldFailBattleStart);
                    _flow.ActiveSession.AttachBattle(Battle);
                }
                else
                {
                    Events.Add("frontend");
                    if (ShouldFailFrontend)
                        throw new InvalidOperationException("frontend");

                    return CompleteFrontendAsync();
                }

                return UniTask.CompletedTask;
            }

            /// <summary>
            /// 読込完了と画面準備完了を個別に制御し、実際の EntryPoint 通知を代替します。
            /// [KO] 로드 완료와 화면 준비 완료를 각각 제어하여 실제 EntryPoint 알림을 대체합니다.
            /// [EN] Controls load completion and screen readiness separately to stand in for entry point notification.
            /// </summary>
            private async UniTask CompleteFrontendAsync()
            {
                if (FrontendLoad != null)
                    await FrontendLoad.Task;

                if (!ShouldSkipFrontendReady)
                    _flow.MarkFrontendReady();
            }
        }
        /// <summary>
        /// 資産ロードの完了をテストが任意に遅らせる所有者。
        /// [KO] 자산 로드 완료를 테스트에서 지연시킬 수 있는 소유자입니다.
        /// [EN] Resource owner whose load completion can be delayed by the test.
        /// </summary>
        private sealed class FakeResources : IRunResources
        {
            private readonly FlowRig _rig;
            public bool IsDisposed { get; private set; }
            /// <summary>
            /// 遅延と解放順序を管理するテスト環境を保存します。
            /// [KO] 지연과 해제 순서를 관리할 테스트 환경을 저장합니다.
            /// [EN] Stores the fixture controlling delays and cleanup order.
            /// </summary>
            public FakeResources(FlowRig rig)
                => _rig = rig;
            /// <summary>
            /// テストが指定した失敗または遅延を適用し、最後に取消を確認します。
            /// [KO] 테스트에서 지정한 실패 또는 지연을 적용하고 마지막에 취소를 확인합니다.
            /// [EN] Applies test-controlled failure or delay and checks cancellation after completion.
            /// </summary>
            public async UniTask PrepareAsync(CancellationToken cancellationToken)
            {
                if (_rig.ShouldFailPreparation)
                    throw new InvalidOperationException("prepare");

                if (_rig.Preparation != null)
                    await _rig.Preparation.Task;

                cancellationToken.ThrowIfCancellationRequested();
            }
            /// <summary>
            /// 解放要求を記録し、終了処理の順番を検査できるようにします。
            /// [KO] 해제 요청을 기록하여 종료 처리 순서를 검사하도록 합니다.
            /// [EN] Records disposal requests for assertions about cleanup order.
            /// </summary>
            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                _rig.Events.Add("assets-dispose");
                if (_rig.ShouldFailDisposal)
                    throw new InvalidOperationException("dispose");
            }
        }
        /// <summary>
        /// 戦闘の開始回数と破棄を記録します。
        /// [KO] 전투 시작 횟수와 해제를 기록합니다.
        /// [EN] Records battle starts and disposal.
        /// </summary>
        private sealed class FakeBattle : IRunBattle
        {
            private readonly List<string> _events;

            /// <summary>
            /// この戦闘の Start を失敗させる、生成時に固定した条件。
            /// [KO] 이 전투의 Start를 실패시킬 생성 시 고정 조건입니다.
            /// [EN] Construction-time choice to fail this battle's Start method.
            /// </summary>
            private readonly bool _shouldFailStart;
            public int Starts { get; private set; }
            /// <summary>
            /// 解放順序を記録する一覧を受け取ります。
            /// [KO] 해제 순서를 기록할 목록을 받습니다.
            /// [EN] Receives the list recording cleanup order.
            /// </summary>
            public FakeBattle(List<string> events, bool shouldFailStart)
            {
                _events = events;
                _shouldFailStart = shouldFailStart;
            }
            /// <summary>
            /// 開始回数を増やし、重複開始を検査できるようにします。
            /// [KO] 시작 횟수를 늘려 중복 시작을 검사하도록 합니다.
            /// [EN] Counts starts so duplicate startup can be detected.
            /// </summary>
            public void Start()
            {
                Starts++;
                if (_shouldFailStart)
                    throw new InvalidOperationException("battle-start");
            }
            /// <summary>
            /// 解放要求を記録し、終了処理の順番を検査できるようにします。
            /// [KO] 해제 요청을 기록하여 종료 처리 순서를 검사하도록 합니다.
            /// [EN] Records disposal requests for assertions about cleanup order.
            /// </summary>
            public void Dispose()
                => _events.Add("battle-dispose");
        }
        /// <summary>
        /// 表示された処理段階と呼出し回数を記録します。
        /// [KO] 표시한 처리 단계와 호출 횟수를 기록합니다.
        /// [EN] Records the displayed phase and display call count.
        /// </summary>
        private sealed class FakeTransitionView : IRunTransitionView
        {
            public int Calls { get; private set; }
            public Define.RunTransitionPhase LastPhase { get; private set; }

            /// <summary>
            /// ローディング表示のどの段階でも例外を出す条件。
            /// [KO] 로딩 표시의 어느 단계에서든 예외를 발생시킬 조건입니다.
            /// [EN] Condition causing every loading-display phase to throw.
            /// </summary>
            public bool ShouldFailShow;
            /// <summary>
            /// 共通表示への呼出しを数え、終了後の操作を検出します。
            /// [KO] 공통 표시 호출을 세어 종료 후 조작을 감지합니다.
            /// [EN] Counts transition display calls to detect access after disposal.
            /// </summary>
            public void Show(Define.RunTransitionPhase phase)
            {
                Calls++;
                LastPhase = phase;
                if (ShouldFailShow)
                    throw new InvalidOperationException("loading-view");
            }
            /// <summary>
            /// 共通表示を閉じる要求を数えます。
            /// [KO] 공통 표시를 닫는 요청을 셉니다.
            /// [EN] Counts requests to close the transition display.
            /// </summary>
            public void Hide()
                => Calls++;
        }
    }
}
