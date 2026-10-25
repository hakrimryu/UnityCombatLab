using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// Inspector で編集する、戦闘開始時の HP・攻撃力・攻撃間隔を保存します。
    /// [KO] Inspector에서 편집할 전투 시작 HP·공격력·공격 간격을 저장합니다.
    /// [EN] Stores starting health, attack power and attack interval edited in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        /// <summary>
        /// 判定規則に対応する役割。
        /// [KO] 판정 규칙에 대응하는 역할.
        /// [EN] Role mapped to combat rules.
        /// </summary>
        [SerializeField] private Define.UnitRole _role;

        /// <summary>
        /// 初期最大 HP。
        /// [KO] 초기 최대 HP.
        /// [EN] Initial maximum health.
        /// </summary>
        [SerializeField] private int _maxHealth;

        /// <summary>
        /// 通常攻撃の計算に使う開始時の攻撃力。
        /// [KO] 일반 공격 계산에 사용할 시작 공격력.
        /// [EN] Starting attack power used to calculate normal attacks.
        /// </summary>
        [SerializeField] private int _attack;

        /// <summary>
        /// 攻撃を繰り返す間隔。1 tick は 0.05 秒なので、24 tick は 1.2 秒です。
        /// [KO] 반복 공격 간격입니다. 1tick은 0.05초이므로 24tick은 1.2초입니다.
        /// [EN] Repeat attack interval: one tick is 0.05 seconds, so 24 ticks means 1.2 seconds.
        /// </summary>
        [SerializeField] private int _intervalTicks;

        /// <summary>
        /// 戦闘開始時の防御膜。
        /// [KO] 전투 시작 시 방어막.
        /// [EN] Shield at battle start.
        /// </summary>
        [SerializeField] private int _shield;

        /// <summary>
        /// この設定の HP・攻撃力に ID・陣営・配置枠を付け、今回の戦闘を始めるためのデータを作ります。
        /// [KO] 이 설정의 HP·공격력에 ID·팀·슬롯을 붙여, 이번 전투를 시작하는 데 사용할 데이터를 만듭니다.
        /// [EN] Combines these stats with an ID, team and slot to create the data used to start this battle.
        /// </summary>
        public BattleUnitInput CreateInput(int id, Define.BattleTeam team, Define.BattleSlot slot)
        {
            // 開始 HP には最大 HP を使う。
            return new BattleUnitInput(id, team, slot, _role, _maxHealth, _attack, _intervalTicks, _shield);
        }
    }
}
