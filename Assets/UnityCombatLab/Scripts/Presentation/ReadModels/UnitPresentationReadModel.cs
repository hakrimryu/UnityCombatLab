using System;
using System.Collections.Generic;
using R3;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// 命中表示に合わせた HP・盾と、これから減る HP の予告値を保持します。
    /// [KO] 명중 표시에 맞춘 HP·방어막과 앞으로 줄어들 HP의 예고값을 보관합니다.
    /// [EN] Stores health and shields timed to impact presentation and preview health for pending damage.
    /// </summary>
    public sealed class UnitPresentationReadModel : IDisposable
    {
        /// <summary>
        /// 命中表示で更新し、HUD へ通知する HP。
        /// [KO] 명중 표시 때 갱신해 HUD에 알릴 HP입니다.
        /// [EN] Health updated at impact presentation and notified to the HUD.
        /// </summary>
        private readonly ReactiveProperty<int> _health;

        /// <summary>
        /// 命中表示で更新し、HUD へ通知する防御膜。
        /// [KO] 명중 표시 때 갱신해 HUD에 알릴 방어막입니다.
        /// [EN] Shield value updated at impact presentation and notified to the HUD.
        /// </summary>
        private readonly ReactiveProperty<int> _shield;

        /// <summary>
        /// 命中前の被害も差し引いた HP。ゲージの損失予告に使います。
        /// [KO] 명중 전 피해까지 뺀 HP이며 게이지의 손실 예고에 사용합니다.
        /// [EN] Health after subtracting pending damage, used for the gauge loss preview.
        /// </summary>
        private readonly ReactiveProperty<int> _previewHealth;

        /// <summary>
        /// Presenter が予約し、命中表示で取り除く HP 損失の合計。
        /// [KO] Presenter가 예약하고 명중 표시 때 제거하는 HP 손실 합계입니다.
        /// [EN] Total health loss reserved by the presenter and removed as hits are presented.
        /// </summary>
        private int _pendingHealthDamage;

        /// <summary>
        /// 未表示の回復量。後続被害の予約可能 HP に含めます。
        /// [KO] 미표시 회복량이며 후속 피해의 예약 가능 HP에 포함합니다.
        /// [EN] Pending healing included in health available for subsequent damage reservations.
        /// </summary>
        private int _pendingHealing;

        /// <summary>
        /// 対象一人の予約を保持し、回復の前後だけは確定順を守って表示します。
        /// [KO] 대상 한 명의 예약을 보관하고 회복 전후에만 확정 순서를 지켜 표시합니다.
        /// [EN] Holds one target's reservations, preserving committed order across healing boundaries.
        /// </summary>
        private readonly List<(BattleEvent Event, bool IsReady)> _changes = new();

        /// <summary>
        /// HP と盾へ反映したイベントを Presenter に渡し、数字と被弾も同じ時点で表示します。
        /// [KO] HP·방어막에 반영한 이벤트를 Presenter에 전달해 숫자·피격도 같은 시점에 표시합니다.
        /// [EN] Reports the applied health or shield event so the presenter displays numbers and hits at the same time.
        /// </summary>
        public event Action<BattleEvent> ChangePresented;

        /// <summary>
        /// 同じユニットの名前・最大 HP を表示するための不変入力。
        /// [KO] 같은 유닛의 이름·최대 HP 표시에 쓰는 불변 입력입니다.
        /// [EN] Immutable input identifying the unit and its maximum health.
        /// </summary>
        public BattleUnitInput Input { get; }

        /// <summary>
        /// HUD に表示する HP と、その変更通知。
        /// [KO] HUD에 표시할 HP와 변경 알림입니다.
        /// [EN] Health displayed by the HUD and its change notifications.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Health => _health;

        /// <summary>
        /// HUD 専用防御膜。
        /// [KO] HUD 전용 방어막입니다.
        /// [EN] HUD-only shield.
        /// </summary>
        public ReadOnlyReactiveProperty<int> Shield => _shield;

        /// <summary>
        /// まだ命中していない被害を含めた、ゲージ予告用 HP。
        /// [KO] 아직 명중하지 않은 피해를 포함한 게이지 예고용 HP입니다.
        /// [EN] Preview health including damage whose impact has not yet been displayed.
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
        /// 命中を待つ HP 損失を加算し、ゲージの予告値を更新します。
        /// [KO] 명중을 기다리는 HP 손실을 더하고 게이지 예고값을 갱신합니다.
        /// [EN] Accumulates health loss awaiting impact and updates preview health.
        /// </summary>
        public void ReserveDamage(int healthDamage)
        {
            if (healthDamage < 0 || healthDamage > _health.Value - _pendingHealthDamage + _pendingHealing)
            {
                throw new ArgumentOutOfRangeException(nameof(healthDamage));
            }

            _pendingHealthDamage += healthDamage;
            RefreshPreview();
        }

        /// <summary>
        /// 一回の命中の HP 損失と盾吸収を表示値から引き、待機中の損失合計も更新します。
        /// [KO] 한 번의 명중에 해당하는 HP 손실과 방어막 흡수를 표시 값에서 빼고 대기 중인 손실 합계도 갱신합니다.
        /// [EN] Subtracts one impact's health loss and shield absorption from displayed values and updates pending health loss.
        /// </summary>
        public void ApplyDamage(int healthDamage, int shieldDamage)
        {
            if (healthDamage < 0 || healthDamage > _pendingHealthDamage || healthDamage > _health.Value || shieldDamage < 0 ||
                shieldDamage > _shield.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(healthDamage));
            }

            _pendingHealthDamage -= healthDamage;
            _shield.Value -= shieldDamage;
            _health.Value -= healthDamage;
            RefreshPreview();
        }

        /// <summary>
        /// 被害または回復を確定順に予約し、後の表示順序を記録します。
        /// [KO] 피해·회복을 확정 순서로 예약하고 이후 표시 순서를 기록합니다.
        /// [EN] Reserves damage or healing in committed order for later presentation.
        /// </summary>
        public void ReserveChange(BattleEvent item)
        {
            if (item.TargetId != Input.Id ||
                (item.Kind != Define.BattleEventKind.DamageApplied && item.Kind != Define.BattleEventKind.HealthRestored))
            {
                throw new ArgumentException("Health change must target this unit.", nameof(item));
            }

            if (item.Kind == Define.BattleEventKind.DamageApplied)
                ReserveDamage(item.HealthDamage);
            else
            {
                var projected = _health.Value - _pendingHealthDamage + _pendingHealing;
                if (item.HealthRestored <= 0 || projected <= 0 || item.HealthRestored > Input.MaxHealth - projected)
                    throw new ArgumentException("Healing exceeds living target health limits.", nameof(item));

                _pendingHealing += item.HealthRestored;
                RefreshPreview();
            }

            _changes.Add((item, false));
        }

        /// <summary>
        /// 命中した被害は到着順に反映し、回復は前の被害を待ち、後の被害はその回復を待ちます。
        /// [KO] 명중 피해는 도착 순서로 반영하되 회복은 앞 피해를, 뒤 피해는 그 회복을 기다립니다.
        /// [EN] Applies damage in arrival order, while healing waits for prior damage and later damage waits for that healing.
        /// </summary>
        public void PresentChange(int sequence)
        {
            var found = false;
            for (var index = 0; index < _changes.Count; index++)
            {
                if (_changes[index].Event.Sequence != sequence)
                    continue;

                _changes[index] = (_changes[index].Event, true);
                found = true;
                break;
            }

            if (!found)
                throw new InvalidOperationException("The presented health change has not been reserved.");

            for (var index = 0; index < _changes.Count;)
            {
                var change = _changes[index];
                var item = change.Event;
                // 被害同士は加算なので先着を表示できる。回復を追い越すと HP の負値や上限超過が生じる。
                if (item.Kind == Define.BattleEventKind.HealthRestored && (index != 0 || !change.IsReady))
                    break;

                if (!change.IsReady)
                {
                    index++;
                    continue;
                }

                _changes.RemoveAt(index);
                if (item.Kind == Define.BattleEventKind.DamageApplied)
                    ApplyDamage(item.HealthDamage, item.ShieldDamage);
                else
                {
                    _pendingHealing -= item.HealthRestored;
                    _health.Value += item.HealthRestored;
                    RefreshPreview();
                }

                ChangePresented?.Invoke(item);
            }
        }

        /// <summary>
        /// 予約済みの被害と回復から、現在 HP 以下の被害予告を再計算します。
        /// [KO] 예약 피해·회복으로 현재 HP 이하의 피해 예고를 다시 계산합니다.
        /// [EN] Recomputes damage preview from pending damage and healing, capped at current health.
        /// </summary>
        private void RefreshPreview()
            => _previewHealth.Value = Math.Min(_health.Value, _health.Value - _pendingHealthDamage + _pendingHealing);

        /// <summary>
        /// HUD 購読を外した後で通知元を解放します。
        /// [KO] HUD 구독을 끊은 뒤 알림 원본을 해제합니다.
        /// [EN] Releases notification sources after HUD subscriptions detach.
        /// </summary>
        public void Dispose()
        {
            ChangePresented = null;
            _changes.Clear();
            _health.Dispose();
            _shield.Dispose();
            _previewHealth.Dispose();
        }
    }
}
