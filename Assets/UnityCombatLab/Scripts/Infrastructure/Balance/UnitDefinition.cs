using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// Inspector で編集するユニットの開始 HP・攻撃力などを保存します。戦闘中に減った HP は保存しません。
    /// [KO] Inspector에서 편집할 유닛의 시작 HP·공격력 등을 저장합니다. 전투 중 감소한 HP는 저장하지 않습니다.
    /// [EN] Stores Inspector-editable starting stats. Health lost during battle is not written to this asset.
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
        /// <param name="id">
        /// この戦闘でユニットを区別する正数。例: 騎士は 1、最初の敵は 2。
        /// [KO] 이번 전투에서 유닛을 구별할 양수입니다. 예: 기사 1, 첫 번째 적 2.
        /// [EN] Positive ID identifying a unit in this battle, such as knight 1 and first enemy 2.
        /// </param>
        /// <param name="team">
        /// 味方か敵かを指定します。役割と異なる陣営は生成時に拒否されます。
        /// [KO] 아군인지 적인지 지정합니다. 역할과 맞지 않는 팀은 생성 시 거부됩니다.
        /// [EN] Selects ally or enemy; construction rejects a team that does not match the role.
        /// </param>
        /// <param name="slot">
        /// 前列・後列上・後列下のどこに置くか。ワールド座標ではありません。
        /// [KO] 전열·위쪽 후열·아래쪽 후열 중 배치할 자리입니다. 월드 좌표가 아닙니다.
        /// [EN] Front, upper-back or lower-back battle position; this is not a world coordinate.
        /// </param>
        /// <returns>
        /// 生成後は設定値を変更できない開始データ。不正な値は BattleUnitInput のコンストラクターが拒否します。
        /// [KO] 생성 후 설정값을 바꿀 수 없는 시작 데이터입니다. 잘못된 값은 BattleUnitInput 생성자가 거부합니다.
        /// [EN] Starting data whose properties cannot be changed after construction; its constructor rejects invalid values.
        /// </returns>
        public BattleUnitInput CreateInput(int id, Define.BattleTeam team, Define.BattleSlot slot)
        {
            // 開始 HP を別指定しないので、BattleUnitInput は最大 HP を開始 HP に使う。
            // この asset 自体は変更せず、Unity の asset を参照しない C# オブジェクトを返す。
            return new BattleUnitInput(id, team, slot, _role, _maxHealth, _attack, _intervalTicks, _shield);
        }
    }
}
