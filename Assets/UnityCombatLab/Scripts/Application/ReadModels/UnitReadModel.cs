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
        /// ユニットの ID・陣営・スロットと開始時の能力値。
        /// [KO] 유닛의 ID·팀·슬롯과 시작 능력치입니다.
        /// [EN] Unit ID, team, slot and starting stats.
        /// </summary>
        public BattleUnitInput Input { get; }

        /// <summary>
        /// 戦闘計算で確定した HP と、その変更通知。
        /// [KO] 전투 계산으로 확정한 HP와 변경 알림입니다.
        /// [EN] Committed health and its change notifications.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Health => _health;

        /// <summary>
        /// 戦闘計算で確定した防御膜と、その変更通知。
        /// [KO] 전투 계산으로 확정한 방어막과 변경 알림입니다.
        /// [EN] Committed shield value and its change notifications.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Shield => _shield;

        /// <summary>
        /// 開始データの HP と盾で通知用の値を初期化します。
        /// [KO] 시작 데이터의 HP와 방어막으로 알림용 값을 초기화합니다.
        /// [EN] Initializes observable health and shield values from starting data.
        /// </summary>
        internal UnitReadModel(BattleUnitInput input)
        {
            Input = input;
            _health = new ReactiveProperty<int>(input.Health);
            _shield = new ReactiveProperty<int>(input.Shield);
        }

        /// <summary>
        /// 被害イベントの HP 損失と盾吸収を現在値から引きます。
        /// [KO] 피해 이벤트의 HP 손실과 방어막 흡수를 현재 값에서 뺍니다.
        /// [EN] Subtracts the event's health loss and shield absorption from current values.
        /// </summary>
        internal void ApplyDamage(BattleEvent battleEvent)
        {
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
