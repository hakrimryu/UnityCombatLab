using System;
using UnityCombatLab.Common;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 動的UIの型付き参照を一か所で管理します。画像本体やprefab本体は直接保持しません。
    /// [KO] 동적 UI의 타입 지정 참조를 한 곳에서 관리합니다. 이미지와 prefab 본체는 직접 보관하지 않습니다.
    /// [EN] Centralizes typed dynamic UI references without retaining the actual images or prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/UI Asset Catalog")]
    public sealed class UiAssetCatalog : ScriptableObject
    {
        /// <summary>
        /// 全職業が共用する英雄カード。
        /// [KO] 모든 직업이 공유할 영웅 카드입니다.
        /// [EN] Hero card shared by all roles.
        /// </summary>
        [SerializeField] private AssetReferenceGameObject _heroCard;

        /// <summary>
        /// 三つの配置先が共用する枠。
        /// [KO] 세 배치 위치가 공유할 슬롯입니다.
        /// [EN] Slot shared by the three formation positions.
        /// </summary>
        [SerializeField] private AssetReferenceGameObject _formationSlot;

        /// <summary>
        /// Run中だけ使用する戦闘画面。
        /// [KO] Run 중에만 사용할 전투 화면입니다.
        /// [EN] Battle display used only during a run.
        /// </summary>
        [SerializeField] private AssetReferenceGameObject _battleHud;

        /// <summary>
        /// 騎士・弓使い・魔法使いの順に保存する表示素材。
        /// [KO] 기사·궁수·마법사 순서로 저장할 표시 소재입니다.
        /// [EN] Visuals stored in knight, ranger and mage order.
        /// </summary>
        [SerializeField] private HeroVisualAsset[] _heroes;

        /// <summary>
        /// UnitRole順の戦闘用画像。配置を交換しても同じ職業画像を選びます。
        /// [KO] UnitRole 순서의 전투용 이미지이며 배치를 교환해도 같은 직업 이미지를 선택합니다.
        /// [EN] Battle images in UnitRole order, so swapping formation preserves the role's image.
        /// </summary>
        [SerializeField] private AssetReferenceSprite[] _battleIcons;

        /// <summary>
        /// Frontendが読み込む共通カード。
        /// [KO] Frontend가 로드할 공통 카드입니다.
        /// [EN] Shared card loaded by Frontend.
        /// </summary>
        public AssetReferenceGameObject HeroCard => _heroCard;
        /// <summary>
        /// Frontendが読み込む共通配置枠。
        /// [KO] Frontend가 로드할 공통 배치 슬롯입니다.
        /// [EN] Shared formation slot loaded by Frontend.
        /// </summary>
        public AssetReferenceGameObject FormationSlot => _formationSlot;
        /// <summary>
        /// Runが読み込むHUD。
        /// [KO] Run이 로드할 HUD입니다.
        /// [EN] HUD loaded by a run.
        /// </summary>
        public AssetReferenceGameObject BattleHud => _battleHud;

        /// <summary>
        /// Dungeon が所有する報酬・速度・停止 UI の参照。
        /// [KO] Dungeon이 소유하는 보상·속도·정지 UI 참조입니다.
        /// [EN] Reference to reward, speed and pause UI owned by Dungeon.
        /// </summary>
        [SerializeField] private AssetReferenceGameObject _runBattle;

        /// <summary>
        /// Run 開始前にロードする操作画面。
        /// [KO] Run 시작 전에 로드하는 조작 화면입니다.
        /// [EN] Run controls loaded before starting combat.
        /// </summary>
        public AssetReferenceGameObject RunBattle => _runBattle;

        /// <summary>
        /// 職業に対応する画像参照と色を返します。
        /// [KO] 직업에 대응하는 이미지 참조와 색상을 반환합니다.
        /// [EN] Returns image reference and tint for the given role.
        /// </summary>
        public HeroVisualAsset GetHero(Define.UnitRole role)
        {
            var index = (int)role;
            if (_heroes == null || _heroes.Length != Define.SlotsPerTeam || index < 0 || index >= _heroes.Length)
                throw new InvalidOperationException("UI catalog requires knight, ranger and mage visuals.");

            return _heroes[index] ?? throw new InvalidOperationException($"UI catalog has no visual for {role}.");
        }

        /// <summary>
        /// 味方・敵の職業からHUD画像の型付き参照を返します。
        /// [KO] 아군·적 직업으로 HUD 이미지의 타입 지정 참조를 반환합니다.
        /// [EN] Returns a typed HUD image reference for an ally or enemy role.
        /// </summary>
        public AssetReferenceSprite GetBattleIcon(Define.UnitRole role)
        {
            var index = (int)role;
            if (_battleIcons == null || _battleIcons.Length != Enum.GetValues(typeof(Define.UnitRole)).Length ||
                index < 0 || index >= _battleIcons.Length)
            {
                throw new InvalidOperationException("UI catalog requires a battle icon for each unit role.");
            }

            return _battleIcons[index] ?? throw new InvalidOperationException($"UI catalog has no battle icon for {role}.");
        }
    }
}
