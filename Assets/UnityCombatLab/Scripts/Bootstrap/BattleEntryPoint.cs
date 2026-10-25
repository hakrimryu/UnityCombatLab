using System;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Units;
using UnityCombatLab.Presentation.ReadModels;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// 戦闘の開始・毎フレームの進行・終了時の片付けを担当します。
    /// [KO] 전투 시작·매 프레임 진행·종료 시 정리를 담당합니다.
    /// [EN] Handles battle startup, per-frame advancement and cleanup.
    /// </summary>
    public sealed class BattleEntryPoint : IStartable, ITickable, IDisposable
    {
        /// <summary>
        /// この戦闘の計算とイベント通知を進める Runner。
        /// [KO] 이번 전투의 계산과 이벤트 알림을 진행할 Runner입니다.
        /// [EN] Runner advancing this battle's calculations and event delivery.
        /// </summary>
        public BattleRunner Runner { get; }

        /// <summary>
        /// Dungeon の戦闘専用 Canvas から借りる配置先。
        /// [KO] Dungeon의 전투 전용 Canvas에서 빌려 쓰는 배치 대상입니다.
        /// [EN] Borrowed host under the Dungeon-owned battle canvas.
        /// </summary>
        private readonly Transform _hudParent;

        /// <summary>
        /// 戦闘開始時に複製する HUD プレハブ。
        /// [KO] 전투 시작 시 복제할 HUD 프리팹입니다.
        /// [EN] HUD prefab instantiated at battle startup.
        /// </summary>
        private readonly BattleHudView _hudPrefab;

        /// <summary>
        /// Dungeon に配置された、今回の戦闘に使うユニットの View。
        /// [KO] Dungeon에 배치된 이번 전투의 유닛 View입니다.
        /// [EN] Unit views placed in Dungeon for this battle.
        /// </summary>
        private readonly UnitView[] _unitViews;

        /// <summary>
        /// 背景色の設定と HUD の座標変換に使う Dungeon カメラ。
        /// [KO] 배경색 설정과 HUD 좌표 변환에 사용할 Dungeon 카메라입니다.
        /// [EN] Dungeon camera used for the background color and HUD projection.
        /// </summary>
        private readonly Camera _battleCamera;

        /// <summary>
        /// HUD とカメラの色に使う共通パレット。
        /// [KO] HUD와 카메라 색상에 사용할 공통 팔레트입니다.
        /// [EN] Shared palette for HUD and camera colors.
        /// </summary>
        private readonly GameColorPalette _colors;

        /// <summary>
        /// 今回の戦闘を識別する ID。
        /// [KO] 이번 전투를 구별할 ID입니다.
        /// [EN] ID identifying this battle.
        /// </summary>
        private readonly Guid _runId;

        /// <summary>
        /// 単一戦闘のウェーブ番号。
        /// [KO] 단일 전투의 웨이브 번호.
        /// [EN] Wave number for this battle.
        /// </summary>
        private readonly int _wave;

        /// <summary>
        /// 戦闘イベントを受け取り、ユニットの演出を進める Presenter。
        /// [KO] 전투 이벤트를 받아 유닛 연출을 진행할 Presenter입니다.
        /// [EN] Presenter receiving battle events and advancing unit effects.
        /// </summary>
        private BattlePresenter _presenter;

        /// <summary>
        /// 表示用 HP・盾・勝敗の変更を HUD へ伝える Presenter。
        /// [KO] 표시용 HP·방어막·승패 변경을 HUD에 전달할 Presenter입니다.
        /// [EN] Presenter forwarding displayed health, shield and result changes to the HUD.
        /// </summary>
        private BattleHudPresenter _hudPresenter;

        /// <summary>
        /// 命中時刻へ合わせた HUD 状態。Runner の確定状態とは別に所有します。
        /// [KO] 명중 시각에 맞춘 HUD 상태이며 Runner 확정 상태와 별도로 소유합니다.
        /// [EN] Owned impact-timed HUD state, separate from the runner's committed state.
        /// </summary>
        public BattlePresentationReadModel Presentation { get; private set; }

        /// <summary>
        /// この戦闘の同時演出を進める Presenter。
        /// [KO] 이번 전투의 동시 연출을 진행할 Presenter입니다.
        /// [EN] Presenter advancing this battle's concurrent effects.
        /// </summary>
        public BattlePresenter Presenter => _presenter;

        /// <summary>
        /// Dungeon の Canvas 下に生成し、この戦闘の終了時に解放する HUD。
        /// [KO] Dungeon Canvas 아래에 생성하고 이번 전투 종료 시 해제할 HUD입니다.
        /// [EN] HUD created under the Dungeon canvas and released when this battle ends.
        /// </summary>
        public BattleHudView Hud { get; private set; }

        /// <summary>
        /// 開始成功前と解放後の tick を止めます。
        /// [KO] 시작 성공 전 및 해제 후 tick을 막습니다.
        /// [EN] Prevents ticks before successful startup and after disposal.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// 重複破棄を拒否します。
        /// [KO] 중복 해제를 거부합니다.
        /// [EN] Prevents repeated disposal.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// BattleLifetimeScope が用意した Runner・戦闘識別子・View・設定を保持し、Start の接続に使います。
        /// [KO] BattleLifetimeScope가 준비한 Runner·전투 식별자·View·설정을 보관해 Start에서 연결합니다.
        /// [EN] Stores the runner, battle identifiers, views and settings supplied by BattleLifetimeScope for connection in Start.
        /// </summary>
        public BattleEntryPoint(BattleRunner runner, Guid runId, int wave, UnitView[] unitViews,
            BattleHudView hudPrefab, Transform hudParent, Camera battleCamera, GameColorPalette colors)
        {
            Runner = runner;
            _runId = runId;
            _wave = wave;
            _unitViews = unitViews;
            _hudPrefab = hudPrefab;
            _hudParent = hudParent;
            _battleCamera = battleCamera;
            _colors = colors;
        }

        /// <summary>
        /// VContainer が一度呼びます。HUD を作り、HP 通知とキャラクター演出を接続してから進行を許可します。
        /// [KO] VContainer가 한 번 호출합니다. HUD를 만들고 HP 알림·캐릭터 연출을 연결한 후 전투 진행을 허용합니다.
        /// [EN] Called once by VContainer to create the HUD and connect HP notifications and character effects before ticking.
        /// </summary>
        public void Start()
        {
            if (_isStarted || IsDisposed)
            {
                return;
            }

            try
            {
                Hud = UnityEngine.Object.Instantiate(_hudPrefab, _hudParent);
                Hud.name = "UI_BattleHud";
                // スロット位置だけを記録するので、攻撃中のモデルの踏み込みにパネルは追従しない。
                Hud.BindLayout(_battleCamera, _unitViews);
                _battleCamera.backgroundColor = _colors.GetColor(Define.PaletteColor.Background);
                // 先に R3 の HP 通知を接続して初期値を表示し、その後に攻撃・被弾・死亡イベントを接続する。
                Presentation = new BattlePresentationReadModel(Runner.ReadModel.Units.Select(unit => unit.Input));
                foreach (var unit in _unitViews)
                {
                    unit.ApplyPalette(_colors);
                    // 接続前に各ユニットのエフェクトを準備する。
                    unit.PrepareEffects();
                }

                _hudPresenter = new BattleHudPresenter(Presentation, Hud, _colors);
                _presenter = new BattlePresenter(Runner, _runId, _wave, _unitViews, Hud, Presentation);
                // 接続途中に Tick が来ても進まないよう、すべて成功した後で開始済みにする。
                _isStarted = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// VContainer が毎フレーム呼び、実経過秒で計算を、scaled 秒で表示を順に進めます。
        /// [KO] VContainer가 매 프레임 호출하여 실제 경과 시간으로 계산을, scaled 시간으로 표시를 차례로 진행합니다.
        /// [EN] Called each frame by VContainer to advance simulation with unscaled seconds, then presentation with scaled seconds.
        /// </summary>
        public void Tick()
        {
            if (!_isStarted || IsDisposed)
            {
                return;
            }

            try
            {
                // 計算は実経過秒を20Hzへ換算する。表示時計の遅れを計算へ戻さない。
                var simulationDeltaSeconds = Time.unscaledDeltaTime;
                Runner.Advance(simulationDeltaSeconds);
                // 命中予約・View.Update・Normal Tween は scaled 秒を使う。Domain 終了後も最後の表示を進める。
                var presentationDeltaSeconds = Time.deltaTime;
                _presenter.Advance(presentationDeltaSeconds);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Dungeon のコンテナ破棄または開始/進行失敗で呼びます。通知を止めてからデータと生成した HUD を解放します。
        /// [KO] Dungeon 컨테이너 해제 또는 시작·진행 실패 시 호출합니다. 알림을 끊은 뒤 데이터와 생성한 HUD를 해제합니다.
        /// [EN] Called on Dungeon container disposal or startup/tick failure; detaches notifications before releasing data and HUD.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _isStarted = false;
            // 表示先への呼び出しを先に外す。HP の通知元を先に破棄すると、まだ接続中の表示側に終了通知が届く。
            _presenter?.Dispose();
            _hudPresenter?.Dispose();
            Presentation?.Dispose();
            Runner.Dispose();
            if (Hud != null)
            {
                Hud.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(Hud.gameObject);
            }
        }
    }
}
