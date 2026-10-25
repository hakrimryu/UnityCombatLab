using System;
using System.Linq;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
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
        /// Dungeon 専用 Canvas に生成する HUD の原型。
        /// [KO] Dungeon 전용 Canvas에 생성할 HUD 원형입니다.
        /// [EN] HUD prefab instantiated under the Dungeon-owned canvas.
        /// </summary>
        [SerializeField] private BattleHudView _hudPrefab;

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
                _unitViews.Distinct().Count() != _unitViews.Length || _hudPrefab == null ||
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

            // Inspector の並びから ID・陣営・配置枠を決める。両配列は同じ並びであることが前提。
            // index 0/1 → 味方/敵の前列、2/3 → 味方/敵の後列上、4/5 → 味方/敵の後列下。
            // index + 1 は ID 1〜6。ID 0 は「単一対象なし」のイベント用なのでユニットには使わない。
            var inputs = _definitions.Select((definition, index) => definition.CreateInput(index + 1,
                (Define.BattleTeam)(index % Define.BattleTeamCount),
                (Define.BattleSlot)(index / Define.BattleTeamCount))).ToArray();
            // 再読込前のイベントと区別するため、新しい戦闘 ID を一度だけ作る。
            var runId = Guid.NewGuid();
            // 単一戦闘の開始ウェーブは 1。
            const int InitialWave = 1;
            // 同じ runId/wave の Runner と EntryPoint を作り、シーンの View と共通パレットを渡す。
            builder.RegisterEntryPoint(resolver => new BattleEntryPoint(
                new BattleRunner(runId, InitialWave, inputs), runId, InitialWave,
                _unitViews, _hudPrefab, _hudCanvas.transform, _battleCamera,
                resolver.Resolve<GameColorPalette>()), Lifetime.Scoped).AsSelf();
        }
    }
}
