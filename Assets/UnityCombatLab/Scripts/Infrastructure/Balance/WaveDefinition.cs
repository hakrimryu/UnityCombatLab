using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// Inspector で一ウェーブの敵配置を編集する設定。
    /// [KO] Inspector에서 한 웨이브의 적 배치를 편집하는 설정입니다.
    /// [EN] Inspector settings for one wave's enemy slots.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/Wave Definition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        /// <summary>
        /// 前列・上後列・下後列順の敵。Empty の枠は生成しません。
        /// [KO] 전열·위 후열·아래 후열 순서의 적이며 Empty 슬롯은 생성하지 않습니다.
        /// [EN] Enemies ordered front, upper back, lower back; Empty slots are not spawned.
        /// </summary>
        [SerializeField] private Define.WaveEnemy[] _enemies;

        /// <summary>
        /// 配置と Adapter が検査した能力値を組み合わせ、Run 用にコピーします。
        /// [KO] 배치와 Adapter가 검사한 능력값을 합쳐 Run용으로 복사합니다.
        /// [EN] Combines slots with adapter-validated stats and copies them for the run.
        /// </summary>
        public WaveData CreateSnapshot(int enemyHealth, int enemyAttack, int rewardStrengthBp)
            => new(_enemies, enemyHealth, enemyAttack, rewardStrengthBp);
    }
}
