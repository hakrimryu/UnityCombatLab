using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// Inspector で一ウェーブの配置と基礎値を編集する設定。
    /// [KO] Inspector에서 한 웨이브의 배치·기본 수치를 편집하는 설정입니다.
    /// [EN] Inspector settings for one wave's slots and base values.
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
        /// 役割係数前の HP。
        /// [KO] 역할 계수 적용 전 HP입니다.
        /// [EN] Health before role coefficients.
        /// </summary>
        [SerializeField] private int _enemyHealth;

        /// <summary>
        /// 役割係数前の攻撃力。
        /// [KO] 역할 계수 적용 전 공격력입니다.
        /// [EN] Attack before role coefficients.
        /// </summary>
        [SerializeField] private int _enemyAttack;

        /// <summary>
        /// 報酬倍率。10000 は一倍、11000 は1.1倍。
        /// [KO] 보상 배율이며 10000은 1배, 11000은 1.1배입니다.
        /// [EN] Reward strength: 10000 is one times and 11000 is 1.1 times.
        /// </summary>
        [SerializeField] private int _rewardStrengthBp;

        /// <summary>
        /// Run ロード時に設定を検査し、純 C# の固定値へ変換します。
        /// [KO] Run 로드 시 설정을 검사하고 순수 C# 고정값으로 변환합니다.
        /// [EN] Validates and converts settings into fixed pure C# values when a run loads.
        /// </summary>
        public WaveData CreateSnapshot()
            => new(_enemies, _enemyHealth, _enemyAttack, _rewardStrengthBp);
    }
}
