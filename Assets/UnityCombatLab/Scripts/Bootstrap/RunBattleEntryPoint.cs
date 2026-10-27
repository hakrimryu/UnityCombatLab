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
    /// Dungeon の Unity 参照を戦闘生成処理へ接続し、フレーム入力を Application の進行処理へ渡します。
    /// [KO] Dungeon의 Unity 참조를 전투 생성 처리에 연결하고 프레임 입력을 Application 진행 처리에 전달합니다.
    /// [EN] Connects Dungeon Unity references to battle creation and forwards frame input to Application progression.
    /// </summary>
    public sealed class RunBattleEntryPoint : IStartable, ITickable, IRunBattle
    {
        /// <summary>
        /// 操作画面の Presenter に渡す Run と共通音量の調整役。
        /// [KO] 조작 화면 Presenter에 전달할 Run·공통 음량 조정자입니다.
        /// [EN] Run and shared-volume coordinator supplied to the controls presenter.
        /// </summary>
        private readonly RunFlowCoordinator _flow;

        /// <summary>
        /// この Dungeon の開始許可と識別子を持つ Run。
        /// [KO] 이 Dungeon의 시작 허용 상태·식별자를 가진 Run입니다.
        /// [EN] Run holding this Dungeon's startup permission and identity.
        /// </summary>
        private readonly RunSession _session;

        /// <summary>
        /// 戦闘と操作画面の生成に借りる資産。セッションの終了処理が解放します。
        /// [KO] 전투·조작 화면 생성에 빌리는 자산이며 세션 종료 처리가 해제합니다.
        /// [EN] Borrowed assets for combat and controls, released by session cleanup.
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
        /// 戦闘 HUD と操作画面を置く Dungeon の Canvas。
        /// [KO] 전투 HUD·조작 화면을 배치할 Dungeon Canvas입니다.
        /// [EN] Dungeon canvas hosting battle HUDs and controls.
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
        /// ウェーブの時間・戦闘交換・完了判定を所有する Application の実行処理。
        /// [KO] 웨이브 시간·전투 교체·완료 판정을 소유하는 Application 실행 처리입니다.
        /// [EN] Application coordinator owning wave timing, battle replacement and completion.
        /// </summary>
        private readonly RunBattleCoordinator _coordinator;

        /// <summary>
        /// Dungeon 内に生成し、この EntryPoint が破棄する操作画面。
        /// [KO] Dungeon에 생성하고 이 EntryPoint가 파괴할 조작 화면입니다.
        /// [EN] Controls created in Dungeon and destroyed by this entry point.
        /// </summary>
        private RunBattleView _view;

        /// <summary>
        /// 操作画面と Controller の購読を所有する Presenter。
        /// [KO] 조작 화면·Controller 구독을 소유하는 Presenter입니다.
        /// [EN] Presenter owning subscriptions between controls and controller.
        /// </summary>
        private RunBattlePresenter _presenter;

        /// <summary>
        /// 接続完了前の Tick と重複 Start を拒否します。
        /// [KO] 연결 완료 전 Tick·중복 Start를 거부합니다.
        /// [EN] Rejects ticks before connection completes and duplicate starts.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// 破棄後の Start と Tick を拒否します。
        /// [KO] 파괴 후 Start·Tick을 거부합니다.
        /// [EN] Rejects startup and ticks after disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// Application が交換・解放を管理する、現在の Unity 戦闘表示。
        /// [KO] Application이 교체·해제를 관리하는 현재 Unity 전투 표시입니다.
        /// [EN] Current Unity battle presentation whose replacement and disposal are managed by Application.
        /// </summary>
        public BattleEntryPoint Battle { get; private set; }

        /// <summary>
        /// Run と Dungeon の参照を受け取り、Unity 戦闘の生成関数を Application に接続します。
        /// [KO] Run·Dungeon 참조를 받아 Unity 전투 생성 함수를 Application에 연결합니다.
        /// [EN] Receives run and Dungeon references and connects a Unity battle factory to Application.
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
            _coordinator = new RunBattleCoordinator(_flow.Controller, _session, CreateBattle);
        }

        /// <summary>
        /// 操作画面を作って進行通知へ接続し、最初の戦闘の開始を Application に任せます。
        /// [KO] 조작 화면을 만들고 진행 알림에 연결한 뒤 첫 전투 시작을 Application에 맡깁니다.
        /// [EN] Creates controls, connects progression notifications and delegates first-battle startup to Application.
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
                _coordinator.ClockChanged += _view.ShowClock;
                _coordinator.BattleChanged += HandleBattleChanged;
                _coordinator.Start();
                _presenter = new RunBattlePresenter(_flow, _view, _session.InitialState.Id,
                    () => _coordinator.CanControlBattle, ApplyPlayback);
                _isStarted = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Application が用意した開始値と Dungeon の参照から、Runner を所有する未開始の戦闘を組み立てます。
        /// [KO] Application이 준비한 시작값·Dungeon 참조로 Runner를 소유하는 미시작 전투를 조립합니다.
        /// [EN] Assembles an unstarted battle owning its runner from Application-prepared inputs and Dungeon references.
        /// </summary>
        private IWaveBattle CreateBattle(int wave, IReadOnlyList<BattleUnitInput> inputs, RunRewards rewards)
        {
            var runId = _session.InitialState.Id;
            var runner = new BattleRunner(runId, wave, inputs, rewards);
            try
            {
                Battle = new BattleEntryPoint
                (runner,
                    runId,
                    wave,
                    () => BattleUnitFactory.Create(inputs, _resources, _spawnPoints, _unitsRoot),
                    _resources.HudPrefab.GetComponent<BattleHudView>(),
                    _hudParent,
                    _camera,
                    _colors,
                    _resources.Icons,
                    showResult: false);
                return Battle;
            }
            catch
            {
                // 戦闘へ所有権を渡す前の組立失敗では、この生成処理が Runner を解放する。
                runner.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 生成した HUD より操作画面を手前に置き、Presenter に状態の再表示を頼みます。
        /// [KO] 생성한 HUD보다 조작 화면을 앞에 두고 Presenter에 상태 재표시를 요청합니다.
        /// [EN] Keeps controls above the created HUD and asks the presenter to refresh state.
        /// </summary>
        private void HandleBattleChanged()
        {
            _view.transform.SetAsLastSibling();
            _presenter?.Refresh();
        }

        /// <summary>
        /// OS のフォーカス通知を、経過時間を管理する進行処理へ渡します。
        /// [KO] OS 포커스 알림을 경과 시간을 관리하는 진행 처리에 전달합니다.
        /// [EN] Forwards OS focus notifications to the coordinator managing elapsed time.
        /// </summary>
        private void HandleFocusChanged()
            => _coordinator.NotifyFocusChanged(_view.HasFocus);

        /// <summary>
        /// 操作変更時のフォーカス状態を Application の再生速度判断へ渡します。
        /// [KO] 조작 변경 시 포커스 상태를 Application의 재생 속도 판단에 전달합니다.
        /// [EN] Supplies current focus to Application playback decisions when controls change.
        /// </summary>
        private void ApplyPlayback()
            => _coordinator.ApplyPlayback(_view != null && _view.HasFocus);

        /// <summary>
        /// Unity の実経過秒とフォーカスを渡し、進行処理が終了したら画面の接続も解除します。
        /// [KO] Unity 실제 경과 초·포커스를 전달하고 진행 처리가 종료되면 화면 연결도 해제합니다.
        /// [EN] Forwards Unity elapsed seconds and focus, releasing screen connections when progression is disposed.
        /// </summary>
        public void Tick()
        {
            if (!_isStarted || _isDisposed)
                return;

            try
            {
                _coordinator.Advance(Time.unscaledDeltaTime, _view.HasFocus);
            }
            finally
            {
                if (_coordinator.IsDisposed)
                    Dispose();
            }
        }

        /// <summary>
        /// Presenter と進行通知を外してから戦闘と操作画面を解放し、Run 資産はセッションに残します。
        /// [KO] Presenter·진행 알림을 해제한 뒤 전투·조작 화면을 해제하고 Run 자산은 세션에 남깁니다.
        /// [EN] Detaches the presenter and progression notifications, then releases combat and controls while leaving run assets with the session.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _presenter?.Dispose();
            _coordinator.BattleChanged -= HandleBattleChanged;
            if (_view != null)
                _coordinator.ClockChanged -= _view.ShowClock;

            try
            {
                _coordinator.Dispose();
            }
            finally
            {
                if (_view != null)
                {
                    _view.FocusChanged -= HandleFocusChanged;
                    _view.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(_view.gameObject);
                }
            }
        }
    }
}
