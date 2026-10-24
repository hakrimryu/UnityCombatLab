using System;
using R3;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Application.ReadModels
{
    /// <summary>
    /// 一人分の画面用 HP と防御膜を保存します。R3 は値が変わった時に HUD へ通知します。
    /// [KO] 한 명의 화면용 HP와 방어막을 저장합니다. 값이 바뀌면 R3가 HUD 쪽에 알립니다.
    /// [EN] Stores one unit’s display health and shield; R3 notifies the HUD when a value changes.
    /// </summary>
    public sealed class UnitReadModel : IDisposable
    {
        /// <summary>
        /// 表示寿命に属する HP。
        /// [KO] 표시 수명에 속하는 HP.
        /// [EN] Health owned by this display lifetime.
        /// </summary>
        private readonly ReactiveProperty<int> _health;

        /// <summary>
        /// 表示寿命に属する防御膜。
        /// [KO] 표시 수명에 속하는 방어막.
        /// [EN] Shield owned by this display lifetime.
        /// </summary>
        private readonly ReactiveProperty<int> _shield;

        /// <summary>
        /// このユニットの ID・配置・開始能力。生成後は値を変更できず、現在 HP は Health で別に扱います。
        /// [KO] 이 유닛의 ID·배치·시작 능력치입니다. 생성 후 바꿀 수 없으며 현재 HP는 Health에서 따로 관리합니다.
        /// [EN] ID, position and starting stats, unchanged after construction; current display health is stored in Health.
        /// </summary>
        public BattleUnitInput Input { get; }

        /// <summary>
        /// 変更権を公開しない HP。
        /// [KO] 변경권을 공개하지 않는 HP.
        /// [EN] Health without public mutation access.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Health => _health;

        /// <summary>
        /// 変更権を公開しない防御膜。
        /// [KO] 변경권을 공개하지 않는 방어막.
        /// [EN] Shield without public mutation access.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Shield => _shield;

        /// <summary>
        /// 初期表示値をコピーします。
        /// [KO] 초기 표시값을 복사합니다.
        /// [EN] Copies the initial display values.
        /// </summary>
        internal UnitReadModel(BattleUnitInput input)
        {
            Input = input;
            _health = new ReactiveProperty<int>(input.Health);
            _shield = new ReactiveProperty<int>(input.Shield);
        }

        /// <summary>
        /// コアが計算済みの HP 損失と防御膜吸収を表示値から引きます。ここで攻撃力から被害を再計算しません。
        /// [KO] 코어가 계산한 HP 손실과 방어막 흡수를 표시값에서 뺍니다. 여기서 공격력으로 피해를 다시 계산하지 않습니다.
        /// [EN] Subtracts health loss and shield absorption already calculated by the core; does not recalculate damage.
        /// </summary>
        internal void ApplyDamage(BattleEvent battleEvent)
        {
            // 例: 防御膜 5、HP 100 に吸収 5・HP 損失 7 が届いたら、防御膜 0・HP 93 を表示する。
            // Value の変更が Subscribe の処理をその場で呼ぶ。同じ値のままなら R3 は変更通知を出さない。
            _shield.Value -= battleEvent.ShieldDamage;
            _health.Value -= battleEvent.HealthDamage;
        }

        /// <summary>
        /// 所有する通知元を解放します。
        /// [KO] 소유한 알림 원본을 해제합니다.
        /// [EN] Disposes the owned notification sources.
        /// </summary>
        public void Dispose()
        {
            _health.Dispose();
            _shield.Dispose();
        }
    }
}
