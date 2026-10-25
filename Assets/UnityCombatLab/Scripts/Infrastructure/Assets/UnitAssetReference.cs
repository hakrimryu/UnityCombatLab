using System;
using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Balance;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 一職業の能力設定と表示prefabを、ロード前の参照として組にします。
    /// [KO] 한 직업의 능력 설정과 표시 prefab을 로드 전 참조로 묶습니다.
    /// [EN] Pairs unloaded references to one role's stats and visual prefab.
    /// </summary>
    [Serializable]
    public sealed class UnitAssetReference
    {
        /// <summary>
        /// この組に対応する職業。
        /// [KO] 이 묶음에 대응하는 직업입니다.
        /// [EN] Role represented by this pair.
        /// </summary>
        [SerializeField] private Define.UnitRole _role;

        /// <summary>
        /// 編成説明と戦闘入力が共有する能力設定の参照。
        /// [KO] 편성 설명과 전투 입력이 공유하는 능력 설정 참조입니다.
        /// [EN] Stat reference shared by formation descriptions and battle inputs.
        /// </summary>
        [SerializeField] private AssetReferenceT<UnitDefinition> _definition;

        /// <summary>
        /// モデル・アニメーション・効果を依存先に持つ自作prefab。
        /// [KO] 모델·애니메이션·효과를 의존성으로 포함하는 자체 prefab입니다.
        /// [EN] Owned prefab with model, animation and effect dependencies.
        /// </summary>
        [SerializeField] private AssetReferenceGameObject _prefab;

        /// <summary>
        /// 対応する職業を返します。
        /// [KO] 대응하는 직업을 반환합니다.
        /// [EN] Returns the represented role.
        /// </summary>
        public Define.UnitRole Role => _role;

        /// <summary>
        /// 能力設定を読み込む型付き参照。
        /// [KO] 능력 설정을 로드할 타입 지정 참조입니다.
        /// [EN] Typed reference for loading stats.
        /// </summary>
        public AssetReferenceT<UnitDefinition> Definition => _definition;

        /// <summary>
        /// 戦闘表示を読み込む型付き参照。
        /// [KO] 전투 표시를 로드할 타입 지정 참조입니다.
        /// [EN] Typed reference for loading battle visuals.
        /// </summary>
        public AssetReferenceGameObject Prefab => _prefab;
    }
}
