using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// 強化一種類の基礎増加量と重複上限を編集する設定。
    /// [KO] 강화 한 종류의 기본 증가량과 중첩 상한을 편집하는 설정입니다.
    /// [EN] Inspector settings for one upgrade's base increment and stack limit.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/Reward Definition")]
    public sealed class RewardDefinition : ScriptableObject
    {
        /// <summary>
        /// 適用する効果。
        /// [KO] 적용할 효과입니다.
        /// [EN] Effect to apply.
        /// </summary>
        [SerializeField] private Define.RewardKind _kind;

        /// <summary>
        /// 一回の基本増加量。1500 は15%。
        /// [KO] 1회 기본 증가량이며 1500은 15%입니다.
        /// [EN] Base increment per choice; 1500 denotes fifteen percent.
        /// </summary>
        [SerializeField] private int _baseIncreaseBp;

        /// <summary>
        /// 同じ種類の最大選択数。
        /// [KO] 같은 종류의 최대 선택 횟수입니다.
        /// [EN] Maximum selections of this kind.
        /// </summary>
        [SerializeField] private int _maxStacks = 3;

        /// <summary>
        /// Run 開始時に純 C# の定義へコピーします。
        /// [KO] Run 시작 시 순수 C# 정의로 복사합니다.
        /// [EN] Copies settings into a pure C# definition at run start.
        /// </summary>
        public RewardData CreateSnapshot()
            => new(_kind, _baseIncreaseBp, _maxStacks);
    }
}
