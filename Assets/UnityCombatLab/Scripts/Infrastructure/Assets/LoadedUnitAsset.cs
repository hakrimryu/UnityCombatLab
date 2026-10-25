using System;
using UnityCombatLab.Infrastructure.Balance;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// Runのロード済み能力設定とprefab。ハンドルはRunAssetScopeが保持します。
    /// [KO] Run에서 로드한 능력 설정과 prefab이며 핸들은 RunAssetScope가 유지합니다.
    /// [EN] Loaded run stats and prefab whose handles remain owned by RunAssetScope.
    /// </summary>
    public sealed class LoadedUnitAsset
    {
        /// <summary>
        /// 戦闘入力を作る能力設定。
        /// [KO] 전투 입력을 만들 능력 설정입니다.
        /// [EN] Stats used to create battle inputs.
        /// </summary>
        public UnitDefinition Definition { get; }

        /// <summary>
        /// Dungeon内で複製する表示prefab。
        /// [KO] Dungeon 안에서 복제할 표시 prefab입니다.
        /// [EN] Visual prefab instantiated within Dungeon.
        /// </summary>
        public GameObject Prefab { get; }

        /// <summary>
        /// 両方のロード成功後に、Runが借用先へ渡す組を作ります。
        /// [KO] 두 자산 로드 성공 후 Run이 사용처에 빌려줄 묶음을 만듭니다.
        /// [EN] Creates the pair lent by the run after both assets have loaded successfully.
        /// </summary>
        public LoadedUnitAsset(UnitDefinition definition, GameObject prefab)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            Prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
        }
    }
}
