using System;
using R3;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Application.ReadModels
{
    /// <summary>
    /// 一人分の計算上の確定 HP と盾を保存します。HUD が命中時に示す値とは別です。
    /// [KO] 한 명의 계산상 확정 HP와 방어막을 저장하며 HUD가 명중 때 표시하는 값과 별개입니다.
    /// [EN] Stores one unit’s committed simulation health and shield, separately from impact-timed HUD values.
    /// </summary>
    public sealed class UnitReadModel : IDisposable
    {
        /// <summary>
        /// この戦闘の確定 HP。
        /// [KO] 이번 전투의 확정 HP.
        /// [EN] Committed health for this battle.
        /// </summary>
        private readonly ReactiveProperty<int> _health;

        /// <summary>
        /// この戦闘の確定盾。
        /// [KO] 이번 전투의 확정 방어막.
        /// [EN] Committed shield for this battle.
        /// </summary>
        private readonly ReactiveProperty<int> _shield;

        /// <summary>
        /// このユニットの ID・配置・開始能力。生成後は値を変更できず、現在 HP は Health で別に扱います。
        /// [KO] 이 유닛의 ID·배치·시작 능력치입니다. 생성 후 바꿀 수 없으며 현재 HP는 Health에서 따로 관리합니다.
        /// [EN] ID, position and starting stats, unchanged after construction; current committed health is stored in Health.
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
        /// 初期確定値をコピーします。
        /// [KO] 초기 확정값을 복사합니다.
        /// [EN] Copies the initial committed values.
        /// </summary>
        internal UnitReadModel(BattleUnitInput input)
        {
            Input = input;
            _health = new ReactiveProperty<int>(input.Health);
            _shield = new ReactiveProperty<int>(input.Shield);
        }

        /// <summary>
        /// コアが計算済みの HP 損失と防御膜吸収を確定値から引きます。ここで攻撃力から被害を再計算しません。
        /// [KO] 코어가 계산한 HP 손실과 방어막 흡수를 확정값에서 뺍니다. 여기서 공격력으로 피해를 다시 계산하지 않습니다.
        /// [EN] Subtracts core-calculated health loss and shield absorption from committed values without recalculating damage.
        /// </summary>
        internal void ApplyDamage(BattleEvent battleEvent)
        {
            // 例: 盾 5、HP 100 に吸収 5・HP 損失 7 が届いたら、確定値を盾 0・HP 93 にする。
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
