using System;
using System.Collections.Generic;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.UI;
using UnityEngine;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// Dungeon 内で五つの戦闘と報酬画面を進め、Run 資産を終了まで保持します。
    /// [KO] Dungeon 안에서 다섯 전투·보상 화면을 진행하고 Run 자산을 종료까지 유지합니다.
    /// [EN] Advances five battles and rewards inside Dungeon while retaining run assets until completion.
    /// </summary>
    public sealed class RunBattleEntryPoint : IStartable, ITickable, IRunBattle
    {

        /// <summary>
        /// 開始バナーの実時間秒。戦闘 tick には含めません。
        /// [KO] 시작 배너의 실제 초이며 전투 tick에는 포함하지 않습니다.
        /// [EN] Real seconds for the introduction, excluded from battle ticks.
        /// </summary>
        private const float IntroSeconds = 0.6f;

        /// <summary>
        /// 中間クリア案内の実時間秒。
        /// [KO] 중간 클리어 안내의 실제 초입니다.
        /// [EN] Real seconds for intermediate-clear guidance.
        /// </summary>
        private const float ClearSeconds = 0.5f;

        /// <summary>
        /// Run の進行状態と終了処理を借ります。
        /// [KO] Run 진행 상태·종료 처리를 빌립니다.
        /// [EN] Borrowed run flow and cleanup coordinator.
        /// </summary>
        private readonly RunFlowCoordinator _flow;

        /// <summary>
        /// この Dungeon が動かす Run と取消の所有者。
        /// [KO] 이 Dungeon이 진행할 Run·취소의 소유자입니다.
        /// [EN] Owner of this Dungeon's run and cancellation.
        /// </summary>
        private readonly RunSession _session;

        /// <summary>
        /// 全ウェーブで共有し、Run 終了後に解放する資産。
        /// [KO] 모든 웨이브에서 공유하고 Run 종료 후 해제할 자산입니다.
        /// [EN] Assets shared across waves and released after the run.
        /// </summary>
        private readonly RunAssetScope _resources;

        /// <summary>
        /// 味方/敵を交互に並べた六つの生成位置。
        /// [KO] 아군·적 교차 순서의 여섯 생성 위치입니다.
        /// [EN] Six spawn anchors in interleaved ally/enemy slot order.
        /// </summary>
        private readonly Transform[] _spawnPoints;

        /// <summary>
        /// Dungeon が所有する生成ユニットの親。
        /// [KO] Dungeon이 소유하는 생성 유닛 부모입니다.
        /// [EN] Dungeon-owned parent of spawned units.
        /// </summary>
        private readonly Transform _unitsRoot;

        /// <summary>
        /// Dungeon の既存 Canvas。新しい Canvas は作りません。
        /// [KO] Dungeon의 기존 Canvas이며 새 Canvas는 만들지 않습니다.
        /// [EN] Existing Dungeon canvas used for combat UI.
        /// </summary>
        private readonly Transform _hudParent;

        /// <summary>
        /// スロット上の HUD を投影する Dungeon カメラ。
        /// [KO] 슬롯 위 HUD를 투영할 Dungeon 카메라입니다.
        /// [EN] Dungeon camera projecting slot HUD positions.
        /// </summary>
        private readonly Camera _camera;

        /// <summary>
        /// ユニットと HUD が共有する配色。
        /// [KO] 유닛·HUD가 공유하는 색상입니다.
        /// [EN] Palette shared by units and HUD.
        /// </summary>
        private readonly GameColorPalette _colors;

        /// <summary>
        /// Runner の再利用バッファから値として複写した Run 全体のイベント。
        /// [KO] Runner 재사용 버퍼에서 값으로 복사한 전체 Run 이벤트입니다.
        /// [EN] Whole-run events copied by value from the runner's reusable buffer.
        /// </summary>
        private readonly List<BattleEvent> _events = new();

        /// <summary>
        /// この Dungeon 内に生成し、終了時に破棄する操作画面。
        /// [KO] 이 Dungeon에 생성하고 종료 시 파괴할 조작 화면입니다.
        /// [EN] Controls spawned in this Dungeon and destroyed at cleanup.
        /// </summary>
        private RunBattleView _view;

        /// <summary>
        /// 操作画面と Controller の購読をまとめて解除する Presenter。
        /// [KO] 조작 화면·Controller 구독을 함께 해제하는 Presenter입니다.
        /// [EN] Presenter owning subscriptions between controls and controller.
        /// </summary>
        private RunBattlePresenter _presenter;

        /// <summary>
        /// 現在生成している戦闘のウェーブ番号。
        /// [KO] 현재 생성한 전투의 웨이브 번호입니다.
        /// [EN] Wave number of the currently spawned battle.
        /// </summary>
        private int _wave;

        /// <summary>
        /// 同じ秒の文字列を作り直さないための最終表示秒。
        /// [KO] 같은 초의 문자열을 다시 만들지 않기 위한 마지막 표시 초입니다.
        /// [EN] Last displayed second, avoiding repeated formatting within the same second.
        /// </summary>
        private int _lastClockSecond = -1;

        /// <summary>
        /// 開始またはクリアの案内に残った実時間秒。
        /// [KO] 시작·클리어 안내에 남은 실제 초입니다.
        /// [EN] Remaining real seconds of introduction or clear guidance.
        /// </summary>
        private float _remainingBannerSeconds;

        /// <summary>
        /// 画面と戦闘の生成がすべて成功したか。
        /// [KO] 화면·전투 생성이 모두 성공했는지 나타냅니다.
        /// [EN] Whether controls and battle creation both succeeded.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// 終了後の Start と Tick を拒否する状態。
        /// [KO] 종료 후 Start·Tick을 거부하는 상태입니다.
        /// [EN] State rejecting Start and Tick after disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 勝敗確定直後にボタンを一度更新するための前回状態。
        /// [KO] 승패 확정 직후 버튼을 한 번 갱신하기 위한 이전 상태입니다.
        /// [EN] Prior control state used to refresh buttons once when combat ends.
        /// </summary>
        private bool _wereControlsEnabled;

        /// <summary>
        /// フォーカス変化直後の経過時間を取り込まない指定。
        /// [KO] 포커스 변경 직후 경과 시간을 받지 않는 지정입니다.
        /// [EN] Flag excluding elapsed time immediately after a focus change.
        /// </summary>
        private bool _shouldSkipDelta;

        /// <summary>
        /// 現在の一ウェーブだけを所有する戦闘。ウェーブ変更時に交換します。
        /// [KO] 현재 한 웨이브만 소유하는 전투이며 웨이브 변경 시 교체합니다.
        /// [EN] Battle owning only the current wave, replaced at wave transitions.
        /// </summary>
        public BattleEntryPoint Battle { get; private set; }

        /// <summary>
        /// Run と Dungeon の既存参照を受け取ります。生成物はこの EntryPoint が解放します。
        /// [KO] Run·Dungeon의 기존 참조를 받으며 생성물은 이 EntryPoint가 해제합니다.
        /// [EN] Receives existing run and Dungeon references and owns spawned objects.
        /// </summary>
        public RunBattleEntryPoint(RunFlowCoordinator flow, RunSession session, RunAssetScope resources,
            Transform[] spawnPoints, Transform unitsRoot, Transform hudParent, Camera camera, GameColorPalette colors)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _spawnPoints = spawnPoints ?? throw new ArgumentNullException(nameof(spawnPoints));
            _unitsRoot = unitsRoot != null ? unitsRoot : throw new ArgumentNullException(nameof(unitsRoot));
            _hudParent = hudParent != null ? hudParent : throw new ArgumentNullException(nameof(hudParent));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _colors = colors != null ? colors : throw new ArgumentNullException(nameof(colors));
        }

        /// <summary>
        /// 操作 UI と最初のウェーブを生成し、重複 Start を無視します。
        /// [KO] 조작 UI·첫 웨이브를 생성하고 중복 Start를 무시합니다.
        /// [EN] Creates controls and the first wave, ignoring duplicate Start calls.
        /// </summary>
        public void Start()
        {
            if (_isStarted || _isDisposed || !_session.IsBattleAllowed)
                return;

            try
            {
                var prefab = _resources.RunBattlePrefab.GetComponent<RunBattleView>();
                if (prefab == null)
                    throw new InvalidOperationException("Run controls prefab requires RunBattleView.");

                _view = UnityEngine.Object.Instantiate(prefab, _hudParent);
                _view.name = "UI_RunBattleView";
                _view.ApplyPalette(_colors);
                _view.FocusChanged += HandleFocusChanged;
                CreateWave();
                _presenter = new RunBattlePresenter(_flow, _view, _session.InitialState.Id, CanControlBattle, ApplyPlayback);
                _isStarted = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 前の戦闘を破棄し、現在の強化で全回復した味方と次の敵を生成します。
        /// [KO] 이전 전투를 파괴하고 현재 강화로 완전히 회복한 아군·다음 적을 생성합니다.
        /// [EN] Replaces the previous battle with fully healed upgraded allies and the next enemies.
        /// </summary>
        private void CreateWave()
        {
            // 表示と購読を先に止める。資産 lease は全五ウェーブを所有する RunAssetScope に残す。
            Battle?.Dispose();
            var progress = _flow.Controller.Progress;
            var inputs = progress.Data.CreateUnits(_session.InitialState.Configuration, progress.Wave, progress.Rewards);
            var runId = _session.InitialState.Id;
            _wave = progress.Wave;
            var runner = new BattleRunner(runId, _wave, inputs, progress.Rewards);
            runner.EventOccurred += HandleEventOccurred;
            Battle = new BattleEntryPoint
            (runner,
                runId,
                _wave,
                () => BattleUnitFactory.Create(inputs, _resources, _spawnPoints, _unitsRoot),
                _resources.HudPrefab.GetComponent<BattleHudView>(),
                _hudParent,
                _camera,
                _colors,
                _resources.Icons,
                showResult: false);
            Battle.Start();
            Battle.SetPlaybackSpeed(0);
            _view.transform.SetAsLastSibling();
            _view.ShowClock(0);
            _lastClockSecond = 0;
            _remainingBannerSeconds = IntroSeconds;
            _wereControlsEnabled = false;
        }

        /// <summary>
        /// 同期受信したイベントを再現記録へ値として追加します。
        /// [KO] 동기 수신 이벤트를 재현 기록에 값으로 추가합니다.
        /// [EN] Copies a synchronously received event into the replay record.
        /// </summary>
        private void HandleEventOccurred(BattleEvent item)
            => _events.Add(item);

        /// <summary>
        /// 復帰時の時間ジャンプを防ぎ、すぐに表示速度を更新します。
        /// [KO] 복귀 시 시간 점프를 막고 표시 속도를 즉시 갱신합니다.
        /// [EN] Prevents a time jump on return and immediately refreshes presentation speed.
        /// </summary>
        private void HandleFocusChanged()
        {
            // 復帰フレームの大きな unscaledDelta を、バナーや戦闘時間へ加えない。
            _shouldSkipDelta = true;
            ApplyPlayback();
        }

        /// <summary>
        /// 現在の Run の未終了戦闘だけが操作を受け付けます。
        /// [KO] 현재 Run의 종료 전 전투만 조작을 받습니다.
        /// [EN] Accepts controls only for the current run's unfinished battle.
        /// </summary>
        private bool CanControlBattle()
            => !_isDisposed && _session.IsBattleAllowed && _flow.Controller.Current?.Phase == Define.RunPhase.Battle &&
               Battle != null && Battle.Runner.Result == Define.BattleResult.Ongoing;

        /// <summary>
        /// 状態とフォーカスから、自分の戦闘表示だけの速度を決めます。
        /// [KO] 상태·포커스로 자신의 전투 표시에만 적용할 속도를 정합니다.
        /// [EN] Derives speed for owned combat presentation from phase and focus.
        /// </summary>
        private void ApplyPlayback()
        {
            var controller = _flow.Controller;
            var canAdvance = !_isDisposed && _session.IsBattleAllowed && _view != null && _view.HasFocus &&
                             !controller.IsPaused && controller.Current?.Phase == Define.RunPhase.Battle;
            Battle?.SetPlaybackSpeed(canAdvance ? controller.Speed : 0);
        }

        /// <summary>
        /// 開始バナー・戦闘・中間クリアを進め、報酬と停止中は時間を蓄積しません。
        /// [KO] 시작 배너·전투·중간 클리어를 진행하고 보상·정지 중에는 시간을 쌓지 않습니다.
        /// [EN] Advances introductions, combat and intermediate clear, accumulating no time during rewards or pause.
        /// </summary>
        public void Tick()
        {
            if (!_isStarted || _isDisposed || !_session.IsBattleAllowed)
                return;

            try
            {
                var controller = _flow.Controller;
                var phase = controller.Current?.Phase;
                if (phase == Define.RunPhase.WaveIntro && _wave != controller.Progress.Wave)
                {
                    CreateWave();
                    _presenter.Refresh();
                }

                if (!_view.HasFocus && (phase == Define.RunPhase.WaveIntro || CanControlBattle()))
                    controller.TryPause(_session.InitialState.Id);

                ApplyPlayback();
                if (!_view.HasFocus || controller.IsPaused)
                    return;

                if (_shouldSkipDelta)
                {
                    _shouldSkipDelta = false;
                    return;
                }

                if (phase == Define.RunPhase.WaveIntro)
                {
                    _remainingBannerSeconds -= Time.unscaledDeltaTime;
                    if (_remainingBannerSeconds <= 0)
                        controller.TryBeginWave(_session.InitialState.Id, _wave);

                    return;
                }

                if (phase == Define.RunPhase.WaveClear)
                {
                    _remainingBannerSeconds -= Time.unscaledDeltaTime;
                    if (_remainingBannerSeconds <= 0)
                        controller.TryOpenRewards(_session.InitialState.Id);

                    return;
                }

                if (phase != Define.RunPhase.Battle)
                    return;

                Battle.Advance(Time.unscaledDeltaTime * controller.Speed);
                // 20 tick が1秒。警告開始の75秒も同じ秒境界なので、表示は秒が変わる時だけ更新する。
                var clockSecond = Battle.Runner.CurrentTick / Define.TicksPerSecond;
                if (_lastClockSecond != clockSecond)
                {
                    _lastClockSecond = clockSecond;
                    _view.ShowClock(Battle.Runner.CurrentTick);
                }

                var controlsEnabled = CanControlBattle();
                if (_wereControlsEnabled != controlsEnabled)
                {
                    _wereControlsEnabled = controlsEnabled;
                    _presenter.Refresh();
                }

                if (!Battle.Presenter.IsComplete)
                    return;

                if (!controller.TryFinishWave(_session.InitialState.Id, _wave, Battle.Runner.Result, Battle.Runner.CurrentTick))
                    throw new InvalidOperationException("The current wave completion was rejected.");

                if (controller.Progress.IsFinished)
                {
                    var result = controller.Result;
                    _session.QueueResult(result, new RunReplayRecord(controller.Progress.Data, result, _events));
                }
                else
                {
                    _remainingBannerSeconds = ClearSeconds;
                }
            }
            catch (Exception exception)
            {
                Dispose();
                _session.QueueFailure(exception);
            }
        }

        /// <summary>
        /// 購読・現在ウェーブ・操作 UI を解放します。資産は既存 Run 終了処理が次フレームに解放します。
        /// [KO] 구독·현재 웨이브·조작 UI를 해제하며 자산은 기존 Run 종료 처리가 다음 프레임에 해제합니다.
        /// [EN] Releases subscriptions, current wave and controls; existing run cleanup releases assets next frame.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _presenter?.Dispose();
            Battle?.Dispose();
            _events.Clear();
            if (_view != null)
            {
                _view.FocusChanged -= HandleFocusChanged;
                _view.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_view.gameObject);
            }
        }
    }
}
