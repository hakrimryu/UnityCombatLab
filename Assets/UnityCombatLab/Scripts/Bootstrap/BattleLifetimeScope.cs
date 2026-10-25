using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Application.Run;
using System;
using System.Linq;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Infrastructure.Balance;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.Units;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// Dungeon の RunViewRoot に付ける開始コンポーネント。データ・キャラクター・HUD を一つの戦闘につなぎます。
    /// [KO] Dungeon의 RunViewRoot에 붙는 시작 컴포넌트입니다. 데이터·캐릭터·HUD를 한 전투로 연결합니다.
    /// [EN] Startup component on Dungeon/RunViewRoot. Connects unit data, characters and HUD for one battle.
    /// </summary>
    public sealed class BattleLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// 前列の味方/敵、後列上の味方/敵、後列下の味方/敵の順に Inspector で割り当てる能力設定。
        /// [KO] Inspector에서 아군/적 전열, 아군/적 위쪽 후열, 아군/적 아래쪽 후열 순으로 연결하는 능력치 설정.
        /// [EN] Stat assets assigned as ally/enemy front, ally/enemy upper-back, then ally/enemy lower-back.
        /// </summary>
        [SerializeField] private UnitDefinition[] _definitions;

        /// <summary>
        /// _definitions と同じ順序のキャラクター。例えば両配列の先頭は同じ騎士を指します。
        /// [KO] _definitions와 같은 순서의 캐릭터입니다. 예를 들어 두 배열의 첫 항목은 같은 기사를 가리킵니다.
        /// [EN] Characters in the same order as _definitions; the first item in both arrays refers to the knight.
        /// </summary>
        [SerializeField] private UnitView[] _unitViews;

        /// <summary>
        /// この Dungeon シーンが所有し、BattleEntryPoint が HUD を生成する Canvas。
        /// [KO] 이 Dungeon 씬이 소유하며 BattleEntryPoint가 HUD를 생성할 Canvas입니다.
        /// [EN] Dungeon-owned canvas where BattleEntryPoint creates the HUD.
        /// </summary>
        [SerializeField] private Canvas _hudCanvas;

        /// <summary>
        /// Dungeon のカメラを HUD に渡し、固定スロットの頭上へ状態パネルを投影します。
        /// [KO] Dungeon 카메라를 HUD에 전달하여 고정 슬롯 머리 위에 상태 패널을 투영합니다.
        /// [EN] Passes the Dungeon camera to the HUD to project status panels above fixed slots.
        /// </summary>
        [SerializeField] private Camera _battleCamera;

        /// <summary>
        /// 共通サービスを解決するため、Boot で作成した AppLifetimeScope を Dungeon の DI 親として返します。
        /// [KO] 공통 서비스를 가져오도록 Boot에서 생성한 AppLifetimeScope를 Dungeon의 DI 부모로 반환합니다.
        /// [EN] Returns the AppLifetimeScope created in Boot as the Dungeon DI parent for resolving shared services.
        /// </summary>
        protected override LifetimeScope FindParent()
        {
            return FindFirstObjectByType<AppLifetimeScope>();
        }

        /// <summary>
        /// 6 人の開始データを作り、Runner と View をつなぐ BattleEntryPoint を登録します。
        /// [KO] 6명의 시작 데이터를 만들고 Runner와 View를 연결할 BattleEntryPoint를 등록합니다.
        /// [EN] Creates six units' starting data and registers BattleEntryPoint to connect the runner and views.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            if (_definitions == null || _definitions.Length != Define.MaxBattleUnitCount ||
                _definitions.Any(definition => definition == null) || _unitViews == null ||
                _unitViews.Length != _definitions.Length || _unitViews.Any(view => view == null) ||
                _unitViews.Distinct().Count() != _unitViews.Length ||
                _battleCamera == null || Parent == null)
            {
                throw new InvalidOperationException(
                    "Battle requires six distinct views, definitions, HUD, camera and AppRoot.");
            }

            if (_hudCanvas == null || _hudCanvas.gameObject.scene != gameObject.scene ||
                _hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                throw new InvalidOperationException("Battle requires an Overlay canvas owned by its Dungeon scene.");
            }

            var session = Parent.Container.Resolve<RunFlowCoordinator>().ActiveSession;
            if (session == null)
                throw new InvalidOperationException("Dungeon requires a run started from formation.");

            // Unity のロードは取消できない。取消後に届いた Dungeon は、戦闘を登録せず復帰を待つ。
            if (session.Token.IsCancellationRequested)
                return;

            var resources = session.Resources as RunAssetScope;
            var hudObject = resources?.HudPrefab;
            var hudPrefab = hudObject != null ? hudObject.GetComponent<BattleHudView>() : null;
            if (hudPrefab == null)
                throw new InvalidOperationException("Run HUD must finish loading before Dungeon.");

            var originals = _definitions.Select((definition, index) => definition.CreateInput(index + 1,
                (Define.BattleTeam)(index % Define.BattleTeamCount),
                (Define.BattleSlot)(index / Define.BattleTeamCount))).ToArray();
            var setup = new RunBattleSetup(session.InitialState.Configuration, originals);
            var views = ArrangeViews(setup);

            var runId = session.InitialState.Id;
            // 全接続ができても、読込側が許可するまでは EntryPoint.Start/Tick を進めない。
            builder.RegisterEntryPoint(resolver =>
            {
                var battle = new BattleEntryPoint
                (new BattleRunner(runId, Define.InitialWave, setup.Units),
                    runId,
                    Define.InitialWave,
                    views,
                    hudPrefab,
                    _hudCanvas.transform,
                    _battleCamera,
                    resolver.Resolve<GameColorPalette>(),
                    resources.Icons,
                    session);
                session.AttachBattle(battle);
                return battle;
            }, Lifetime.Scoped).AsSelf();
        }

        /// <summary>
        /// 全配置先を先に保存してから、能力データと同じ原型のViewを移動します。
        /// [KO] 모든 배치 위치를 먼저 저장한 뒤 능력 데이터와 같은 원형의 View를 옮깁니다.
        /// [EN] Caches every destination before moving the views selected by the same source mapping as the stats.
        /// </summary>
        private UnitView[] ArrangeViews(RunBattleSetup setup)
        {
            var allyPositions = new Vector3[Define.SlotsPerTeam];
            for (var slot = 0; slot < allyPositions.Length; slot++)
                allyPositions[slot] = _unitViews[slot * Define.BattleTeamCount].transform.position;
            var views = new UnitView[setup.Units.Count];
            for (var index = 0; index < views.Length; index++)
            {
                views[index] = _unitViews[setup.GetSourceIndex(index)];
                var input = setup.Units[index];
                if (input.Team == Define.BattleTeam.Ally)
                    views[index].transform.position = allyPositions[(int)input.Slot];
            }

            return views;
        }
    }
}
