using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Application.Run;
using System;
using System.Linq;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.Styling;
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
        /// 前列の味方/敵、後列上の味方/敵、後列下の味方/敵の順に割り当てる配置先。
        /// [KO] 아군/적 전열, 아군/적 위쪽 후열, 아군/적 아래쪽 후열 순으로 연결하는 배치 지점입니다.
        /// [EN] Spawn points assigned as ally/enemy front, upper-back, then lower-back.
        /// </summary>
        [SerializeField] private Transform[] _spawnPoints;

        /// <summary>
        /// このDungeonが所有し、生成したキャラクターをまとめる親。
        /// [KO] 이 Dungeon이 소유하며 생성한 캐릭터를 모아둘 부모입니다.
        /// [EN] Dungeon-owned parent for spawned characters.
        /// </summary>
        [SerializeField] private Transform _unitsRoot;

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
            if (_spawnPoints == null || _spawnPoints.Length != Define.MaxBattleUnitCount ||
                _spawnPoints.Any(point => point == null || point.gameObject.scene != gameObject.scene) ||
                _spawnPoints.Distinct().Count() != _spawnPoints.Length ||
                _unitsRoot == null || _unitsRoot.gameObject.scene != gameObject.scene ||
                _battleCamera == null || _battleCamera.gameObject.scene != gameObject.scene || Parent == null)
            {
                throw new InvalidOperationException(
                    "Battle requires six distinct spawn points, a unit parent, camera and AppRoot in the correct scene.");
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

            // 既存の戦闘契約に合わせ、0/1は味方/敵の前列、2/3は後列上、4/5は後列下の原型を渡す。
            var roles = new[] { Define.UnitRole.Knight, Define.UnitRole.SkeletonMelee,
                Define.UnitRole.Ranger, Define.UnitRole.SkeletonRanged, Define.UnitRole.Mage, Define.UnitRole.SkeletonCaptain };
            var originals = roles.Select((role, index) => resources.GetUnit(role).Definition.CreateInput(index + 1,
                (Define.BattleTeam)(index % Define.BattleTeamCount),
                (Define.BattleSlot)(index / Define.BattleTeamCount))).ToArray();
            var setup = new RunBattleSetup(session.InitialState.Configuration, originals);

            var runId = session.InitialState.Id;
            // 全接続ができても、読込側が許可するまでは EntryPoint.Start/Tick を進めない。
            builder.RegisterEntryPoint(resolver =>
            {
                var battle = new BattleEntryPoint
                (new BattleRunner(runId, Define.InitialWave, setup.Units),
                    runId,
                    Define.InitialWave,
                    () => BattleUnitFactory.Create(setup.Units, resources, _spawnPoints, _unitsRoot),
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
    }
}
