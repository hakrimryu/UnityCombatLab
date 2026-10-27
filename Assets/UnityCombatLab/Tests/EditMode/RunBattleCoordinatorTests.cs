using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// シーンや prefab を生成せず、実際の Runner と制御可能な表示完了で Run の進行を確認します。
    /// [KO] 씬·prefab을 생성하지 않고 실제 Runner·제어 가능한 표시 완료로 Run 진행을 확인합니다.
    /// [EN] Checks run progression with real runners and controlled presentation completion without scenes or prefabs.
    /// </summary>
    public sealed class RunBattleCoordinatorTests
    {
        /// <summary>
        /// 案内・停止・表示完了・報酬を経て、前の戦闘を解放してから次の全回復ユニットを作ります。
        /// [KO] 안내·정지·표시 완료·보상을 거쳐 이전 전투 해제 후 다음 완전 회복 유닛을 만듭니다.
        /// [EN] Passes through banners, pause, presentation completion and rewards before replacing combat with fully healed units.
        /// </summary>
        [Test]
        public void ProgressionWaitsForBannersAndPresentationBeforeReplacingTheWave()
        {
            using var rig = new Rig();
            var coordinator = rig.Coordinator;
            var controller = rig.Flow.Controller;
            var id = rig.Session.InitialState.Id;
            var first = rig.Current;
            coordinator.Start();
            Assert.That(rig.Waves.Count, Is.EqualTo(1));
            coordinator.Advance(0.3f, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.WaveIntro));
            Assert.That(controller.TryPause(id), Is.True);
            coordinator.Advance(30, true);
            Assert.That(first.Runner.CurrentTick, Is.Zero);
            Assert.That(controller.TryResume(id), Is.True);
            coordinator.Advance(0.31f, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
            Assert.That(first.Runner.CurrentTick, Is.Zero);
            Assert.That(controller.TryToggleSpeed(id), Is.True);

            rig.FinishSimulation();
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
            Assert.That(coordinator.CanControlBattle, Is.False);
            Assert.That(rig.Session.PendingResult, Is.Null);
            Assert.That(controller.Progress.TotalTicks, Is.Zero);
            first.IsPresentationReady = true;
            coordinator.Advance(0, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.WaveClear));
            Assert.That(controller.Progress.TotalTicks, Is.EqualTo(first.Runner.CurrentTick));
            coordinator.Advance(0.25f, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.WaveClear));
            coordinator.Advance(0.26f, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.RewardSelection));
            var offers = controller.Progress.GetOffers();
            var before = first.AdvanceCalls;
            coordinator.Advance(100, true);
            Assert.That(first.AdvanceCalls, Is.EqualTo(before));
            Assert.That(controller.Progress.GetOffers(), Is.SameAs(offers));
            Assert.That(controller.TrySelectReward(id, 1, offers[0]), Is.True);
            Assert.That(controller.TrySelectReward(id, 1, offers[0]), Is.False);
            coordinator.Advance(0, true);

            Assert.That(rig.Lifecycle, Is.EqualTo(new[] { "create1", "dispose1", "create2" }));
            Assert.That(first.DisposeCalls, Is.EqualTo(1));
            Assert.That(rig.Resources.IsDisposed, Is.False);
            Assert.That(rig.Current.Runner.CurrentTick, Is.Zero);
            Assert.That(rig.Current.Inputs.Where(unit => unit.Team == Define.BattleTeam.Ally)
                .All(unit => unit.Health == unit.MaxHealth), Is.True);
            var expected = rig.Data.CreateUnits(rig.Session.InitialState.Configuration, 2, controller.Progress.Rewards);
            Assert.That(rig.Current.Inputs.Select(unit => (unit.Id, unit.Team, unit.Slot, unit.Role,
                    unit.MaxHealth, unit.Health, unit.Attack, unit.IntervalTicks, unit.Shield)),
                Is.EqualTo(expected.Select(unit => (unit.Id, unit.Team, unit.Slot, unit.Role,
                    unit.MaxHealth, unit.Health, unit.Attack, unit.IntervalTicks, unit.Shield))));
            Assert.That(controller.Speed, Is.EqualTo(2));
            coordinator.Advance(0.3f, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.WaveIntro));
            coordinator.Advance(0.31f, true);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
            Assert.That(rig.Current.Runner.CurrentTick, Is.Zero);
            Assert.That(rig.Session.Failure, Is.Null);
        }

        /// <summary>
        /// 開始案内と二倍速戦闘のどちらでも、フォーカス復帰は明示再開を待ち最初の delta を捨てます。
        /// [KO] 시작 안내·2배속 전투 모두 포커스 복귀 시 명시 재개를 기다리고 첫 delta를 버립니다.
        /// [EN] Requires manual resume and drops the first return delta during both introduction and double-speed combat.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FocusReturnDropsElapsedTimeUntilManualResume(bool isInBattle)
        {
            using var rig = new Rig();
            var coordinator = rig.Coordinator;
            var controller = rig.Flow.Controller;
            var id = rig.Session.InitialState.Id;
            coordinator.Advance(isInBattle ? 0.61f : 0.3f, true);
            if (isInBattle)
                Assert.That(controller.TryToggleSpeed(id), Is.True);

            coordinator.NotifyFocusChanged(false);
            coordinator.Advance(30, false);
            Assert.That(controller.IsPaused, Is.True);
            Assert.That(rig.Current.PlaybackSpeed, Is.Zero);
            coordinator.NotifyFocusChanged(true);
            coordinator.Advance(30, true);
            Assert.That(controller.IsPaused, Is.True);
            Assert.That(controller.TryResume(id), Is.True);
            coordinator.Advance(30, true);
            Assert.That(rig.Current.Runner.CurrentTick, Is.Zero);
            Assert.That(rig.Current.AdvanceCalls, Is.Zero);
            if (isInBattle)
            {
                coordinator.Advance(0.05f, true);
                Assert.That(rig.Current.Runner.CurrentTick, Is.EqualTo(2));
                Assert.That(rig.Current.PlaybackSpeed, Is.EqualTo(2));
            }
            else
            {
                Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.WaveIntro));
                coordinator.Advance(0.31f, true);
                Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
                Assert.That(rig.Current.Runner.CurrentTick, Is.Zero);
            }

            Assert.That(rig.Session.Failure, Is.Null);
        }

        /// <summary>
        /// 全五戦の表示完了後だけ結果を渡し、一倍・二倍の記録が純粋な再現計算と一致します。
        /// [KO] 다섯 전투의 표시 완료 후에만 결과를 전달하고 1배·2배 기록이 순수 재현 계산과 일치합니다.
        /// [EN] Queues a result only after all five presentations complete and matches replay calculations at normal and double speed.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        public void FinalPresentationQueuesOneReplayAcrossAllWaves(int speed)
        {
            using var rig = new Rig();
            var coordinator = rig.Coordinator;
            var controller = rig.Flow.Controller;
            var id = rig.Session.InitialState.Id;
            var ticks = 0;
            for (var wave = 1; wave <= Define.WavesPerRun; wave++)
            {
                coordinator.Advance(0.61f, true);
                if (wave == 1 && speed == 2)
                    Assert.That(controller.TryToggleSpeed(id), Is.True);

                rig.FinishSimulation();
                Assert.That(rig.Session.PendingResult, Is.Null);
                Assert.That(rig.Session.Replay, Is.Null);
                ticks += rig.Current.Runner.CurrentTick;
                rig.Current.IsPresentationReady = true;
                coordinator.Advance(0, true);
                if (wave < Define.WavesPerRun)
                {
                    coordinator.Advance(0.51f, true);
                    var offers = controller.Progress.GetOffers();
                    Assert.That(controller.TrySelectReward(id, wave, offers[0]), Is.True);
                }
            }

            var record = rig.Session.Replay;
            Assert.That(record, Is.Not.Null);
            Assert.That(rig.Session.PendingResult, Is.SameAs(controller.Result));
            Assert.That(record.Result.Ticks, Is.EqualTo(ticks));
            Assert.That(record.Result.ClearedWaves, Is.EqualTo(5));
            Assert.That(record.Result.Rewards.History.Count, Is.EqualTo(4));
            Assert.That(record.Events.Select(item => item.Wave).Distinct(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            var replay = RunReplayVerifier.Verify(record);
            Assert.That(replay.IsMatch, Is.True, replay.Message);
            var calls = rig.Current.AdvanceCalls;
            coordinator.Advance(100, true);
            Assert.That(rig.Session.Replay, Is.SameAs(record));
            Assert.That(rig.Current.AdvanceCalls, Is.EqualTo(calls));
            coordinator.Dispose();
            Assert.That(record.Events, Is.Not.Empty);
            Assert.That(rig.Waves.All(wave => wave.DisposeCalls == 1), Is.True);
            Assert.That(rig.Resources.IsDisposed, Is.False);
        }

        /// <summary>
        /// 敗北と時間切れは最初のウェーブでも表示完了を待ち、報酬を開かず結果を渡します。
        /// [KO] 첫 웨이브의 패배·시간 초과도 표시 완료를 기다리고 보상 없이 결과를 전달합니다.
        /// [EN] Waits for presentation on first-wave defeat or timeout and queues the result without opening rewards.
        /// </summary>
        [TestCase(Define.BattleResult.Defeat)]
        [TestCase(Define.BattleResult.Timeout)]
        public void NonVictoryWaitsForPresentationAndEndsTheRun(Define.BattleResult expected)
        {
            var defaults = RunProgressTests.CreateData();
            var waves = Enumerable.Range(0, 5).Select(_ => new WaveData
            (new[] { Define.WaveEnemy.Melee, Define.WaveEnemy.Empty, Define.WaveEnemy.Empty },
                1000000,
                expected == Define.BattleResult.Timeout ? 1 : 100000,
                10000)).ToArray();
            var roles = Enumerable.Range(0, 6).Select(index => BattleSimulationTests.Unit(index + 1,
                (Define.UnitRole)index, hp: expected == Define.BattleResult.Timeout ? 1000000 : 1000,
                attack: 1, interval: 24)).ToArray();
            var rewards = Enumerable.Range(0, 6).Select(index => defaults.GetReward((Define.RewardKind)index)).ToArray();
            using var rig = new Rig(new RunGameData(waves, rewards, roles));
            rig.Coordinator.Advance(0.61f, true);
            for (var frame = 0; frame < 20000 && rig.Current.Runner.Result == Define.BattleResult.Ongoing; frame++)
                rig.Coordinator.Advance(0.05f, true);

            Assert.That(rig.Current.Runner.Result, Is.EqualTo(expected));
            Assert.That(rig.Flow.Controller.Current.Phase, Is.EqualTo(Define.RunPhase.Battle));
            Assert.That(rig.Session.PendingResult, Is.Null);
            rig.Current.IsPresentationReady = true;
            rig.Coordinator.Advance(0, true);
            Assert.That(rig.Session.PendingResult.Outcome, Is.EqualTo(expected));
            Assert.That(rig.Session.PendingResult.ClearedWaves, Is.Zero);
            Assert.That(rig.Session.PendingResult.Rewards.History, Is.Empty);
            Assert.That(rig.Flow.Controller.TryOpenRewards(rig.Session.InitialState.Id), Is.False);
            var replay = RunReplayVerifier.Verify(rig.Session.Replay);
            Assert.That(replay.IsMatch, Is.True, replay.Message);
            rig.Coordinator.Advance(100, true);
            Assert.That(rig.Waves.Count, Is.EqualTo(1));
            Assert.That(rig.Session.Failure, Is.Null);
        }

        /// <summary>
        /// 次ウェーブの生成または開始失敗で進行を止め、所有済み戦闘だけを一度解放します。
        /// [KO] 다음 웨이브 생성·시작 실패 시 진행을 멈추고 소유한 전투만 한 번 해제합니다.
        /// [EN] Stops on next-wave factory or startup failure and releases each owned battle once.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void NextWaveCreationFailureStopsAndReportsTheOriginalError(bool shouldFailStart)
        {
            using var rig = new Rig();
            rig.Coordinator.Advance(0.61f, true);
            rig.FinishSimulation();
            rig.Current.IsPresentationReady = true;
            rig.Coordinator.Advance(0, true);
            rig.Coordinator.Advance(0.51f, true);
            var controller = rig.Flow.Controller;
            controller.TrySelectReward(rig.Session.InitialState.Id, 1, controller.Progress.GetOffers()[0]);
            rig.ShouldFailCreation = !shouldFailStart;
            rig.ShouldFailStart = shouldFailStart;
            rig.Coordinator.Advance(0, true);

            Assert.That(rig.Coordinator.IsDisposed, Is.True);
            Assert.That(rig.Session.Failure, Is.SameAs(rig.Failure));
            Assert.That(rig.Session.PendingResult, Is.Null);
            Assert.That(rig.Resources.IsDisposed, Is.False);
            Assert.That(rig.Waves.Count, Is.EqualTo(shouldFailStart ? 2 : 1));
            Assert.That(rig.Waves.All(wave => wave.DisposeCalls == 1), Is.True);
            var count = rig.Lifecycle.Count;
            rig.Coordinator.Start();
            rig.Coordinator.Advance(100, true);
            rig.Coordinator.Dispose();
            Assert.That(rig.Lifecycle.Count, Is.EqualTo(count));
        }

        /// <summary>
        /// 表示更新の例外をセッションへ渡し、結果を作らず戦闘を停止します。
        /// [KO] 표시 갱신 예외를 세션에 전달하고 결과를 만들지 않은 채 전투를 멈춥니다.
        /// [EN] Reports a presentation update error to the session and stops combat without producing a result.
        /// </summary>
        [Test]
        public void AdvancementFailureDisposesTheWaveWithoutReleasingRunAssets()
        {
            using var rig = new Rig();
            rig.Coordinator.Advance(0.61f, true);
            rig.Current.AdvanceFailure = rig.Failure;
            rig.Coordinator.Advance(0.05f, true);
            Assert.That(rig.Coordinator.IsDisposed, Is.True);
            Assert.That(rig.Session.Failure, Is.SameAs(rig.Failure));
            Assert.That(rig.Session.PendingResult, Is.Null);
            Assert.That(rig.Current.DisposeCalls, Is.EqualTo(1));
            Assert.That(rig.Resources.IsDisposed, Is.False);
        }

        /// <summary>
        /// 取消または別 Run への交代後の古い進行処理は、時間・生成・表示を変更しません。
        /// [KO] 취소·다른 Run 교체 후 이전 진행 처리는 시간·생성·표시를 변경하지 않습니다.
        /// [EN] An old coordinator changes no timing, creation or display after cancellation or replacement by another run.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void CancelledOrReplacedRunRejectsLateUpdates(bool shouldReplaceRun)
        {
            using var rig = new Rig();
            rig.Coordinator.Advance(0.61f, true);
            var changes = 0;
            rig.Coordinator.ClockChanged += _ => changes++;
            rig.Coordinator.BattleChanged += () => changes++;
            var controller = rig.Flow.Controller;
            if (shouldReplaceRun)
            {
                var id = rig.Session.InitialState.Id;
                controller.TryBeginCleanup(id);
                controller.TryCompleteCleanup(id);
                controller.TryStart(RunConfiguration.CreateDefault());
                controller.PrepareProgress(controller.Current.Id, rig.Data);
                controller.TryEnterBattle(controller.Current.Id);
            }
            else
            {
                rig.Session.Cancel();
            }

            var state = controller.Current;
            rig.Coordinator.Start();
            rig.Coordinator.Advance(100, true);
            Assert.That(controller.Current, Is.SameAs(state));
            Assert.That(rig.Current.Runner.CurrentTick, Is.Zero);
            Assert.That(rig.Waves.Count, Is.EqualTo(1));
            Assert.That(changes, Is.Zero);
            Assert.That(rig.Session.PendingResult, Is.Null);
            Assert.That(rig.Session.Failure, Is.Null);
            rig.Coordinator.Dispose();
            rig.Coordinator.Dispose();
            Assert.That(rig.Current.DisposeCalls, Is.EqualTo(1));
        }

        /// <summary>
        /// 戦闘更新中に所有者が破棄しても、その後の画面通知や失敗結果を作りません。
        /// [KO] 전투 갱신 중 소유자가 파괴해도 이후 화면 알림·실패 결과를 만들지 않습니다.
        /// [EN] Produces no subsequent display notifications or failure when the owner disposes during combat advancement.
        /// </summary>
        [Test]
        public void DisposalDuringAdvanceStopsRemainingNotifications()
        {
            using var rig = new Rig();
            rig.Coordinator.Advance(0.61f, true);
            var changes = 0;
            rig.Coordinator.ClockChanged += _ => changes++;
            rig.Coordinator.BattleChanged += () => changes++;
            rig.Current.AfterAdvance = rig.Coordinator.Dispose;
            rig.Coordinator.Advance(0.05f, true);
            rig.Coordinator.Advance(100, true);
            Assert.That(rig.Current.DisposeCalls, Is.EqualTo(1));
            Assert.That(changes, Is.Zero);
            Assert.That(rig.Session.PendingResult, Is.Null);
            Assert.That(rig.Session.Failure, Is.Null);
        }

        /// <summary>
        /// 実際のロード許可順と Controller を使い、シーンの代わりに表示完了を制御できる戦闘を接続します。
        /// [KO] 실제 로딩 허용 순서·Controller를 사용하고 씬 대신 표시 완료를 제어할 전투를 연결합니다.
        /// [EN] Uses actual loading permissions and the controller while attaching controllable battles instead of scenes.
        /// </summary>
        private sealed class Rig : ISceneTransition, IRunTransitionView, IDisposable
        {
            public readonly RunGameData Data;
            public readonly TestResources Resources = new();
            public readonly List<TestWave> Waves = new();
            public readonly List<string> Lifecycle = new();
            public readonly Exception Failure = new InvalidOperationException("wave presentation failed");
            /// <summary>
            /// セッションの開始許可と資産解放を行う実際の Run 調整役。
            /// [KO] 세션 시작 허용·자산 해제를 수행할 실제 Run 조정자입니다.
            /// [EN] Actual run coordinator granting startup permission and releasing resources.
            /// </summary>
            public readonly RunFlowCoordinator Flow;

            /// <summary>
            /// 終了後も結果・失敗通知を確認するために保持する対象セッション。
            /// [KO] 종료 후에도 결과·실패 알림을 확인하도록 보관할 대상 세션입니다.
            /// [EN] Target session retained to inspect result and failure notifications after shutdown.
            /// </summary>
            public RunSession Session;

            /// <summary>
            /// 経過時間と表示完了をテストから与える進行処理。
            /// [KO] 테스트에서 경과 시간·표시 완료를 전달할 진행 처리입니다.
            /// [EN] Progression coordinator receiving test-controlled time and presentation completion.
            /// </summary>
            public RunBattleCoordinator Coordinator;
            public bool ShouldFailCreation;
            public bool ShouldFailStart;
            public TestWave Current => Waves[Waves.Count - 1];

            /// <summary>
            /// 実際の Run 開始経路から、許可済みの最初のウェーブまで準備します。
            /// [KO] 실제 Run 시작 경로로 허용된 첫 웨이브까지 준비합니다.
            /// [EN] Prepares the permitted first wave through the actual run startup path.
            /// </summary>
            public Rig(RunGameData data = null)
            {
                Data = data ?? RunProgressTests.CreateData();
                Flow = new RunFlowCoordinator
                (this,
                    this,
                    () => Resources,
                    () => UniTask.CompletedTask,
                    exception => Assert.Fail(exception.ToString()),
                    _ => { });
                Flow.StartRunAsync(RunConfiguration.CreateDefault()).GetAwaiter().GetResult();
            }

            /// <summary>
            /// Dungeon 読込時点で進行を準備し、ロード側が開始を許可する前に戦闘を接続します。
            /// [KO] Dungeon 로드 시 진행을 준비하고 로딩 측 시작 허용 전에 전투를 연결합니다.
            /// [EN] Prepares progression and attaches combat during Dungeon loading, before loading permits startup.
            /// </summary>
            public UniTask LoadSceneAsync(Define.SceneType scene, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scene == Define.SceneType.Dungeon)
                {
                    Session = Flow.ActiveSession;
                    Flow.Controller.PrepareProgress(Session.InitialState.Id, Data);
                    Coordinator = new RunBattleCoordinator(Flow.Controller, Session, CreateBattle);
                    Session.AttachBattle(Coordinator);
                    Coordinator.Start();
                    Assert.That(Waves, Is.Empty);
                }

                return UniTask.CompletedTask;
            }

            /// <summary>
            /// 実際の Runner を作り、生成順と解放順を記録する表示の代役へ渡します。
            /// [KO] 실제 Runner를 만들어 생성·해제 순서를 기록하는 표시 대역에 전달합니다.
            /// [EN] Creates a real runner and transfers it to a presentation double recording creation and release order.
            /// </summary>
            private IWaveBattle CreateBattle(int wave, IReadOnlyList<BattleUnitInput> inputs, RunRewards rewards)
            {
                if (ShouldFailCreation)
                    throw Failure;

                Lifecycle.Add("create" + wave);
                var battle = new TestWave
                (new BattleRunner(Session.InitialState.Id, wave, inputs, rewards),
                    inputs,
                    () => Lifecycle.Add("dispose" + wave),
                    ShouldFailStart ? Failure : null);
                Waves.Add(battle);
                return battle;
            }

            /// <summary>
            /// 表示完了を保留したまま、計算の勝敗が確定するまで上限付きで進めます。
            /// [KO] 표시 완료를 보류하고 계산 승패가 확정될 때까지 상한을 두고 진행합니다.
            /// [EN] Advances with a frame limit until simulation finishes while presentation completion remains withheld.
            /// </summary>
            public void FinishSimulation()
            {
                for (var frame = 0; frame < 20000 && Current.Runner.Result == Define.BattleResult.Ongoing; frame++)
                    Coordinator.Advance(0.05f, true);

                Assert.That(Session.Failure, Is.Null);
                Assert.That(Current.Runner.Result, Is.EqualTo(Define.BattleResult.Victory));
            }

            /// <summary>
            /// 描画なしでロード案内を受け取ります。
            /// [KO] 화면 없이 로딩 안내를 받습니다.
            /// [EN] Receives loading guidance without rendering.
            /// </summary>
            public void Show(Define.RunTransitionPhase phase) { }

            /// <summary>
            /// 描画なしでロード案内の終了を受け取ります。
            /// [KO] 화면 없이 로딩 안내 종료를 받습니다.
            /// [EN] Receives loading guidance completion without rendering.
            /// </summary>
            public void Hide() { }

            /// <summary>
            /// Run 所有者から戦闘と資産を解放します。
            /// [KO] Run 소유자를 통해 전투·자산을 해제합니다.
            /// [EN] Releases combat and resources through the run owner.
            /// </summary>
            public void Dispose()
                => Flow.Dispose();
        }

        /// <summary>
        /// Runner は実物を使い、表示完了・更新失敗・解放回数だけを制御します。
        /// [KO] 실제 Runner를 사용하고 표시 완료·갱신 실패·해제 횟수만 제어합니다.
        /// [EN] Uses a real runner while controlling presentation completion, update failure and release counts.
        /// </summary>
        private sealed class TestWave : IWaveBattle
        {
            private readonly Action _released;
            private readonly Exception _startFailure;
            public BattleRunner Runner { get; }
            public IReadOnlyList<BattleUnitInput> Inputs { get; }
            public bool IsPresentationReady;
            public bool IsPresentationComplete => IsPresentationReady && Runner.Result != Define.BattleResult.Ongoing;
            public int DisposeCalls { get; private set; }
            public int AdvanceCalls { get; private set; }
            public int PlaybackSpeed { get; private set; }
            public Exception AdvanceFailure;
            public Action AfterAdvance;

            /// <summary>
            /// Runner と生成入力を保持し、解放時の記録先を保存します。
            /// [KO] Runner·생성 입력을 보관하고 해제 시 기록 대상을 저장합니다.
            /// [EN] Retains its runner and starting inputs and stores its release recorder.
            /// </summary>
            public TestWave(BattleRunner runner, IReadOnlyList<BattleUnitInput> inputs, Action released, Exception startFailure)
            {
                Runner = runner;
                Inputs = inputs;
                _released = released;
                _startFailure = startFailure;
            }

            /// <summary>
            /// 指定した場合だけ表示の接続失敗を再現します。
            /// [KO] 지정한 경우에만 표시 연결 실패를 재현합니다.
            /// [EN] Simulates presentation startup failure only when requested.
            /// </summary>
            public void Start()
            {
                if (_startFailure != null)
                    throw _startFailure;
            }

            /// <summary>
            /// 受け取った戦闘時間で Runner を進め、指定した失敗や途中破棄を再現します。
            /// [KO] 받은 전투 시간으로 Runner를 진행하고 지정한 실패·도중 파괴를 재현합니다.
            /// [EN] Advances the runner with supplied combat time and simulates requested failure or mid-update disposal.
            /// </summary>
            public void Advance(float combatDeltaSeconds)
            {
                if (AdvanceFailure != null)
                    throw AdvanceFailure;

                AdvanceCalls++;
                Runner.Advance(combatDeltaSeconds);
                AfterAdvance?.Invoke();
            }

            /// <summary>
            /// 表示に要求された速度を記録します。
            /// [KO] 표시에 요청한 속도를 기록합니다.
            /// [EN] Records the requested presentation speed.
            /// </summary>
            public void SetPlaybackSpeed(int speed)
                => PlaybackSpeed = speed;

            /// <summary>
            /// 二重要求も数えて Runner と生成記録を解放します。
            /// [KO] 중복 요청도 세고 Runner·생성 기록을 해제합니다.
            /// [EN] Counts even duplicate release requests and disposes the runner and creation record.
            /// </summary>
            public void Dispose()
            {
                DisposeCalls++;
                Runner.Dispose();
                _released();
            }
        }

        /// <summary>
        /// Unity 資産を持たず、Run 終了時の解放だけを記録します。
        /// [KO] Unity 자산 없이 Run 종료 시 해제만 기록합니다.
        /// [EN] Records run resource disposal without holding Unity assets.
        /// </summary>
        private sealed class TestResources : IRunResources
        {
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// 取消を検査し、購入資産なしで準備を完了します。
            /// [KO] 취소를 검사하고 구매 자산 없이 준비를 완료합니다.
            /// [EN] Checks cancellation and completes preparation without purchased assets.
            /// </summary>
            public UniTask PrepareAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return UniTask.CompletedTask;
            }

            /// <summary>
            /// 資産解放を記録します。
            /// [KO] 자산 해제를 기록합니다.
            /// [EN] Records resource disposal.
            /// </summary>
            public void Dispose()
                => IsDisposed = true;
        }
    }
}
