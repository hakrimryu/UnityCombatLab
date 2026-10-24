using System;
using R3;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// 確定被害のうち画面へ到着した HP と、防御膜・予告範囲を保持します。戦闘判定には使いません。
    /// [KO] 확정 피해 중 화면에 도착한 HP와 방어막·예고 구간을 보관하며 전투 판정에는 쓰지 않습니다.
    /// [EN] Stores health, shield and preview values for committed hits that have reached the screen, never for combat decisions.
    /// </summary>
    public sealed class UnitPresentationReadModel : IDisposable
    {
        /// <summary>
        /// HUD が購読する到着済み HP。
        /// [KO] HUD가 구독하는 도착한 HP입니다.
        /// [EN] Arrived health observed by the HUD.
        /// </summary>
        private readonly ReactiveProperty<int> _health;

        /// <summary>
        /// HUD が購読する到着済み防御膜。
        /// [KO] HUD가 구독하는 도착한 방어막입니다.
        /// [EN] Arrived shield observed by the HUD.
        /// </summary>
        private readonly ReactiveProperty<int> _shield;

        /// <summary>
        /// 未到着の確定被害も含めた HP。攻撃準備中の予告にだけ使います。
        /// [KO] 미도착 확정 피해까지 포함한 HP이며 공격 준비 중 예고에만 씁니다.
        /// [EN] Health including committed hits still in flight, used only for preparation previews.
        /// </summary>
        private readonly ReactiveProperty<int> _previewHealth;

        /// <summary>
        /// Presenter が予約し、命中表示で取り除く HP 損失の合計。
        /// [KO] Presenter가 예약하고 명중 표시 때 제거하는 HP 손실 합계입니다.
        /// [EN] Total health loss reserved by the presenter and removed as hits are presented.
        /// </summary>
        private int _pendingHealthDamage;

        /// <summary>
        /// 同じユニットの名前・最大 HP を表示するための不変入力。
        /// [KO] 같은 유닛의 이름·최대 HP 표시에 쓰는 불변 입력입니다.
        /// [EN] Immutable input identifying the unit and its maximum health.
        /// </summary>
        public BattleUnitInput Input { get; }

        /// <summary>
        /// HUD 専用 HP。Domain と Runner の確定値は別に維持します。
        /// [KO] HUD 전용 HP이며 Domain과 Runner의 확정값은 별도로 유지합니다.
        /// [EN] HUD-only health, separate from committed Domain and Runner values.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Health => _health;

        /// <summary>
        /// HUD 専用防御膜。
        /// [KO] HUD 전용 방어막입니다.
        /// [EN] HUD-only shield.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Shield => _shield;

        /// <summary>
        /// 未到着分を示す予告 HP。
        /// [KO] 미도착분을 보여주는 예고 HP입니다.
        /// [EN] Preview health accounting for pending hits.
        /// </summary>
        public ReadOnlyReactiveProperty<int> PreviewHealth => _previewHealth;

        /// <summary>
        /// 戦闘開始時の入力から表示を初期化します。
        /// [KO] 전투 시작 입력으로 표시를 초기화합니다.
        /// [EN] Initializes presentation from battle starting input.
        /// </summary>
        public UnitPresentationReadModel(BattleUnitInput input)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            _health = new ReactiveProperty<int>(input.Health);
            _shield = new ReactiveProperty<int>(input.Shield);
            _previewHealth = new ReactiveProperty<int>(input.Health);
        }

        /// <summary>
        /// 同期消費でコピーした HP 損失を予告に加えます。攻撃力から再計算しません。
        /// [KO] 동기 소비 때 복사한 HP 손실을 예고에 추가하며 공격력으로 재계산하지 않습니다.
        /// [EN] Adds copied committed health loss to the preview without recalculating attack damage.
        /// </summary>
        public void ReserveDamage(int healthDamage)
        {
            if (healthDamage < 0 || healthDamage > _health.Value - _pendingHealthDamage)
            {
                throw new ArgumentOutOfRangeException(nameof(healthDamage));
            }

            _pendingHealthDamage += healthDamage;
            _previewHealth.Value = _health.Value - _pendingHealthDamage;
        }

        /// <summary>
        /// 命中した一件の確定損失だけを引きます。異なる到着順でも古い絶対 HP で表示を巻き戻しません。
        /// [KO] 명중한 한 건의 확정 손실만 뺍니다. 도착 순서가 달라도 이전 절대 HP로 표시를 되돌리지 않습니다.
        /// [EN] Applies one arrived committed loss, preventing stale absolute snapshots from restoring health when arrival order differs.
        /// </summary>
        public void ApplyDamage(int healthDamage, int shieldDamage)
        {
            if (healthDamage < 0 || healthDamage > _pendingHealthDamage || shieldDamage < 0 ||
                shieldDamage > _shield.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(healthDamage));
            }

            _pendingHealthDamage -= healthDamage;
            _shield.Value -= shieldDamage;
            _health.Value -= healthDamage;
            _previewHealth.Value = _health.Value - _pendingHealthDamage;
        }

        /// <summary>
        /// HUD 購読を外した後で通知元を解放します。
        /// [KO] HUD 구독을 끊은 뒤 알림 원본을 해제합니다.
        /// [EN] Releases notification sources after HUD subscriptions detach.
        /// </summary>
        public void Dispose()
        {
            _health.Dispose();
            _shield.Dispose();
            _previewHealth.Dispose();
        }
    }
}
