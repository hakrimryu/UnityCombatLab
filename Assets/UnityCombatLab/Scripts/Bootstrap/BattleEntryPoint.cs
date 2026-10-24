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
    /// VContainer が Start/Tick/Dispose を呼ぶ戦闘の窓口。画面を接続してから進行し、退場時に片付けます。
    /// [KO] VContainer가 Start/Tick/Dispose를 호출하는 전투 진입점입니다. 화면 연결 후 진행하고 씬을 떠날 때 정리합니다.
    /// [EN] VContainer calls Start/Tick/Dispose here to connect the display, advance combat and clean up on scene exit.
    /// </summary>
    public sealed class BattleEntryPoint : IStartable, ITickable, IDisposable
    {
        /// <summary>
        /// この戦闘が所有するコア接続。
        /// [KO] 이 전투가 소유하는 코어 연결.
        /// [EN] Core connection owned by this battle.
        /// </summary>
        public BattleRunner Runner { get; }

        /// <summary>
        /// 共通 Canvas から借りる配置先。
        /// [KO] 공통 Canvas에서 빌려 쓰는 배치 대상.
        /// [EN] Borrowed host under the common canvas.
        /// </summary>
        private readonly Transform _hudParent;

        /// <summary>
        /// 生成元だけを借用する HUD。
        /// [KO] 생성 원본만 빌려 쓰는 HUD.
        /// [EN] Borrowed HUD template.
        /// </summary>
        private readonly BattleHudView _hudPrefab;

        /// <summary>
        /// Dungeon が所有するモデル。
        /// [KO] Dungeon이 소유하는 모델.
        /// [EN] Models owned by Dungeon.
        /// </summary>
        private readonly UnitView[] _unitViews;

        /// <summary>
        /// Dungeon が所有するカメラ。背景色と HUD の座標変換に使い、このクラスでは破棄しません。
        /// [KO] Dungeon이 소유한 카메라입니다. 배경색과 HUD 좌표 변환에 사용하며 이 클래스에서 파괴하지 않습니다.
        /// [EN] Dungeon-owned camera used for the background color and HUD projection; this class does not destroy it.
        /// </summary>
        private readonly Camera _battleCamera;

        /// <summary>
        /// App Scope から借りる色アセット。HUD とカメラへ同じ設定を渡し、この戦闘では破棄しません。
        /// [KO] App Scope에서 빌리는 색상 에셋입니다. HUD와 카메라에 같은 설정을 전달하며 이 전투에서 파괴하지 않습니다.
        /// [EN] Shared app-scope colors passed to the HUD and camera without transferring asset ownership.
        /// </summary>
        private readonly GameColorPalette _colors;

        /// <summary>
        /// 表示寿命識別子。
        /// [KO] 표시 수명 식별자.
        /// [EN] Presentation lifetime identifier.
        /// </summary>
        private readonly Guid _runId;

        /// <summary>
        /// 単一戦闘のウェーブ番号。
        /// [KO] 단일 전투의 웨이브 번호.
        /// [EN] Wave number for this battle.
        /// </summary>
        private readonly int _wave;

        /// <summary>
        /// 所有する表現イベント購読。
        /// [KO] 소유한 표현 이벤트 구독.
        /// [EN] Owned presentation event subscription.
        /// </summary>
        private BattlePresenter _presenter;

        /// <summary>
        /// 所有する R3 購読。
        /// [KO] 소유한 R3 구독.
        /// [EN] Owned R3 subscriptions.
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
        /// 生成した HUD は親が DDOL でも戦闘側で破棄します。
        /// [KO] 생성한 HUD는 부모가 DDOL이어도 전투 측에서 해제합니다.
        /// [EN] Generated HUD is battle-owned even under a DDOL parent.
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
        /// BattleLifetimeScope が用意した Runner とシーンの表示先を受け取ります。まだ戦闘は進めません。
        /// [KO] BattleLifetimeScope가 준비한 Runner와 씬의 표시 대상을 받습니다. 여기서는 아직 전투를 진행하지 않습니다.
        /// [EN] Receives the runner and scene display references prepared by BattleLifetimeScope; does not advance combat yet.
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
                // 共通 Canvas の下に置くが、この戦闘が作った HUD はこのクラスの Dispose で削除する。
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
                    // 色の適用とは別に効果を準備し、後の Bind は生成済みの効果を再利用する。
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
