using System;
using System.Linq;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Balance;
using UnityCombatLab.Presentation.UI;
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
        /// AppRoot Canvas に生成する HUD の原型。
        /// [KO] AppRoot Canvas에 생성할 HUD 원형.
        /// [EN] HUD prefab instantiated under the AppRoot canvas.
        /// </summary>
        [SerializeField] private BattleHudView _hudPrefab;

        /// <summary>
        /// Boot から残った AppLifetimeScope を DI の親にします。Hierarchy の親子関係は変更しません。
        /// [KO] Boot에서 유지된 AppLifetimeScope를 DI 부모로 사용합니다. Hierarchy의 부모·자식 관계는 바꾸지 않습니다.
        /// [EN] Uses the AppLifetimeScope retained from Boot as the DI parent without changing the Hierarchy.
        /// </summary>
        protected override LifetimeScope FindParent()
        {
            return FindFirstObjectByType<AppLifetimeScope>();
        }

        /// <summary>
        /// 継承元の Awake → Build から呼ばれ、6 人の開始データと BattleEntryPoint の作り方を登録します。
        /// [KO] 상속받은 Awake → Build에서 호출되어, 6명의 시작 데이터와 BattleEntryPoint 생성 방법을 등록합니다.
        /// [EN] Called by inherited Awake → Build to prepare six starting inputs and register how to create BattleEntryPoint.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            if (_definitions == null || _definitions.Length != Define.MaxBattleUnitCount ||
                _definitions.Any(definition => definition == null) || _unitViews == null ||
                _unitViews.Length != _definitions.Length || _unitViews.Any(view => view == null) ||
                _unitViews.Distinct().Count() != _unitViews.Length || _hudPrefab == null || Parent == null)
            {
                throw new InvalidOperationException(
                    "Battle requires six distinct views, definitions, HUD and AppRoot.");
            }

            var canvas = Parent.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                throw new InvalidOperationException("AppRoot must own the shared canvas.");
            }

            // Inspector の並びから ID・陣営・配置枠を決める。両配列は同じ並びであることが前提。
            // index 0/1 → 味方/敵の前列、2/3 → 味方/敵の後列上、4/5 → 味方/敵の後列下。
            // % 2 は 0,1,0,1,0,1 となり味方/敵を交互に選ぶ。整数の / 2 は 0,0,1,1,2,2 となり配置枠を選ぶ。
            // index + 1 は ID 1〜6。ID 0 は「単一対象なし」のイベント用なのでユニットには使わない。
            var inputs = _definitions.Select((definition, index) => definition.CreateInput(index + 1,
                (Define.BattleTeam)(index % Define.BattleTeamCount),
                (Define.BattleSlot)(index / Define.BattleTeamCount))).ToArray();
            // 再読込前のイベントと区別するため、新しい戦闘 ID を一度だけ作る。
            var runId = Guid.NewGuid();
            // 今回は単一戦闘だけを起動するため wave は 1。ここでは 5 ウェーブの進行を実装しない。
            const int InitialWave = 1;
            // Boot の RegisterEntryPoint<AppEntryPoint>() は登録済みの型から引数を自動でそろえる。
            // ここはシーンの配列・HUD・今回だけの runId を渡すため、生成方法をラムダで指定する。
            // _ は VContainer が渡す検索用オブジェクトだが、ここでは使わない。ラムダは毎フレーム呼ばれない。
            // new BattleRunner(...) は計算担当、その後の引数は同じ戦闘 ID・wave と表示対象。
            // runId/wave を両方へ渡すのは、Runner が出すイベントと Presenter が受け取る戦闘を一致させるため。
            // Scoped はこの Dungeon のコンテナで一つを共有し、破棄時に Dispose する指定。
            // AsSelf はテストなどで具体的な型 BattleEntryPoint としても取得できるようにする指定。
            builder.RegisterEntryPoint(_ => new BattleEntryPoint(
                new BattleRunner(runId, InitialWave, inputs), runId, InitialWave,
                _unitViews, _hudPrefab, canvas.transform), Lifetime.Scoped).AsSelf();
        }
    }
}
