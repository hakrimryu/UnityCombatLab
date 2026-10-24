using System;
using System.Collections.Generic;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// Presenter が渡す経過秒と確定イベントだけで、攻撃の到着・死亡待機・結果公開を判断します。
    /// [KO] Presenter가 전달한 경과 시간과 확정 이벤트만으로 공격 도착·사망 대기·결과 공개를 판단합니다.
    /// [EN] Decides attack arrival, death waits and result readiness using only elapsed seconds and committed events from the presenter.
    /// </summary>
    public sealed class BattlePresentationTimeline
    {
        /// <summary>
        /// 最後の死亡表示から勝敗公開まで待つ秒数。以前の表示時間を維持します。
        /// [KO] 마지막 사망 표시부터 승패 공개까지 기다리는 초 단위 시간이며 기존 표시 시간을 유지합니다.
        /// [EN] Seconds from the last death presentation until result publication, preserving the existing delay.
        /// </summary>
        private const double DeathResultDelaySeconds = 0.82;

        /// <summary>
        /// 浮動小数点の丸めで命中を一フレーム遅らせないための秒単位の許容差。
        /// [KO] 부동소수점 반올림 때문에 명중이 한 프레임 늦어지지 않도록 둔 초 단위 허용 오차입니다.
        /// [EN] Tolerance in seconds preventing floating-point rounding from postponing impact by one frame.
        /// </summary>
        private const double ImpactToleranceSeconds = 0.00001;

        /// <summary>
        /// 作成順を維持して並行処理する予約。このクラスだけが追加と除去を行います。
        /// [KO] 생성 순서를 유지해 병렬 처리할 예약이며 이 클래스만 추가·제거합니다.
        /// [EN] Cues processed concurrently in creation order, added and removed only by this class.
        /// </summary>
        private readonly List<AttackCue> _attacks = new();

        /// <summary>
        /// 最後に死亡を表示した時刻。未表示なら待機を発生させません。
        /// [KO] 마지막으로 사망을 표시한 시각이며 아직 표시하지 않았다면 대기를 만들지 않습니다.
        /// [EN] Time of the last death presentation; no death means no death wait.
        /// </summary>
        private double? _lastDeathTime;

        /// <summary>
        /// View を参照しない一回の攻撃。イベントは値でコピーし、時刻設定は開始時に固定します。
        /// [KO] View를 참조하지 않는 한 번의 공격이며 이벤트는 값으로 복사하고 시간 설정은 시작 시 고정합니다.
        /// [EN] One attack without view references, holding event value copies and timings fixed at attack start.
        /// </summary>
        public sealed class AttackCue
        {
            /// <summary>
            /// 同期消費からコピーした被害。元のイベント配列は保持しません。
            /// [KO] 동기 소비 중 복사한 피해이며 원래 이벤트 배열은 보관하지 않습니다.
            /// [EN] Damage copied during synchronous consumption, never the original event buffer.
            /// </summary>
            private readonly List<BattleEvent> _damage = new(Define.SlotsPerTeam);

            /// <summary>
            /// Presenter の固定配列で攻撃者を指す番号。
            /// [KO] Presenter의 고정 배열에서 공격자를 가리키는 번호입니다.
            /// [EN] Index identifying the attacker in the presenter's fixed arrays.
            /// </summary>
            public int Actor { get; }

            /// <summary>
            /// 同じ攻撃者の被害を対応付ける Domain tick。
            /// [KO] 같은 공격자의 피해를 대응시킬 Domain 틱입니다.
            /// [EN] Domain tick matching damage from the same attacker.
            /// </summary>
            public int Tick { get; }

            /// <summary>
            /// この攻撃を受け取った表示時計の秒数。
            /// [KO] 이 공격을 받은 표시 시계의 초 단위 시각입니다.
            /// [EN] Presentation-clock time in seconds when this attack arrived.
            /// </summary>
            internal double Started { get; }

            /// <summary>
            /// 開始から発射までの秒数。
            /// [KO] 시작부터 발사까지의 초 단위 시간입니다.
            /// [EN] Seconds from attack start to release.
            /// </summary>
            internal float ReleaseDelay { get; }

            /// <summary>
            /// 開始から命中までの秒数。
            /// [KO] 시작부터 명중까지의 초 단위 시간입니다.
            /// [EN] Seconds from attack start to impact.
            /// </summary>
            internal float ImpactDelay { get; }

            /// <summary>
            /// 復帰を含む攻撃モーションの秒数。
            /// [KO] 복귀를 포함한 공격 모션의 초 단위 시간입니다.
            /// [EN] Attack-motion duration in seconds, including the return.
            /// </summary>
            internal float Duration { get; }

            /// <summary>
            /// 一度表示した命中を再度届けないための状態。Timeline が変更します。
            /// [KO] 한 번 표시한 명중을 다시 전달하지 않을 상태이며 Timeline이 변경합니다.
            /// [EN] State preventing duplicate impact delivery, changed by the timeline.
            /// </summary>
            public bool HasImpacted { get; internal set; }

            /// <summary>
            /// Presenter が命中時に読む、変更不可の被害一覧。
            /// [KO] Presenter가 명중 때 읽는 변경 불가 피해 목록입니다.
            /// [EN] Read-only damage list consumed by the presenter at impact.
            /// </summary>
            public IReadOnlyList<BattleEvent> Damage { get; }

            /// <summary>
            /// Timeline が攻撃開始時にだけ作り、表示時刻と時間設定を記録します。
            /// [KO] Timeline이 공격 시작 때만 생성하여 표시 시각과 시간 설정을 기록합니다.
            /// [EN] Created only by the timeline at attack start to capture presentation time and timing settings.
            /// </summary>
            internal AttackCue(int actor, int tick, double started, float duration, float releaseDelay,
                float impactDelay)
            {
                Actor = actor;
                Tick = tick;
                Started = started;
                Duration = duration;
                ReleaseDelay = releaseDelay;
                ImpactDelay = impactDelay;
                Damage = _damage.AsReadOnly();
            }

            /// <summary>
            /// Timeline が同じ actor と tick の確定被害を値として追加します。
            /// [KO] Timeline이 같은 actor와 tick의 확정 피해를 값으로 추가합니다.
            /// [EN] Timeline appends committed damage with the same actor and tick as a value copy.
            /// </summary>
            internal void AddDamage(BattleEvent damage) => _damage.Add(damage);
        }

        /// <summary>
        /// EntryPoint が渡した scaled 経過秒だけを積み上げる表示時計。Domain の時計とは別です。
        /// [KO] EntryPoint가 전달한 scaled 경과 시간만 누적하는 표시 시계이며 Domain 시계와 별개입니다.
        /// [EN] Presentation clock accumulating only scaled elapsed seconds supplied by EntryPoint, separate from Domain time.
        /// </summary>
        public double Time { get; private set; }

        /// <summary>
        /// Presenter が作成順で読む予約一覧。外部から追加や除去はできません。
        /// [KO] Presenter가 생성 순서대로 읽는 예약 목록이며 외부에서 추가·제거할 수 없습니다.
        /// [EN] Cues read by the presenter in creation order, without external add or remove access.
        /// </summary>
        public IReadOnlyList<AttackCue> Attacks { get; }

        /// <summary>
        /// Domain が既に確定した勝敗。公開の可否は CanComplete で別に判断します。
        /// [KO] Domain이 이미 확정한 승패이며 공개 가능 여부는 CanComplete에서 별도로 판단합니다.
        /// [EN] Domain-committed outcome, with publication readiness decided separately by CanComplete.
        /// </summary>
        public Define.BattleResult Result { get; private set; }

        /// <summary>
        /// 全攻撃が終わり、最後の死亡待機も終わった時だけ true。
        /// [KO] 모든 공격과 마지막 사망 대기가 끝났을 때만 true입니다.
        /// [EN] True only after every attack and the final death wait have finished.
        /// </summary>
        public bool CanComplete => Result != Define.BattleResult.Ongoing && _attacks.Count == 0 &&
                                   (!_lastDeathTime.HasValue || Time >= _lastDeathTime.Value + DeathResultDelaySeconds);

        /// <summary>
        /// Presenter ごとに一つ作り、予約一覧を一度だけ読み取り専用で公開します。
        /// [KO] Presenter마다 하나 생성하고 예약 목록을 한 번만 읽기 전용으로 공개합니다.
        /// [EN] Created once per presenter, exposing a read-only cue list once.
        /// </summary>
        public BattlePresentationTimeline() => Attacks = _attacks.AsReadOnly();

        /// <summary>
        /// Unity の時計を直接読まず、Presenter から受け取った表示秒だけを進めます。
        /// [KO] Unity 시계를 직접 읽지 않고 Presenter에서 받은 표시 시간만 진행합니다.
        /// [EN] Advances only presentation seconds supplied by the presenter, without reading Unity clocks.
        /// </summary>
        public void Advance(double scaledDeltaSeconds)
        {
            if (double.IsNaN(scaledDeltaSeconds) || double.IsInfinity(scaledDeltaSeconds) || scaledDeltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaSeconds));
            Time += scaledDeltaSeconds;
        }

        /// <summary>
        /// 表示設定を秒で受け取り、他の攻撃とは独立した予約を開始します。
        /// [KO] 표시 설정을 초 단위로 받아 다른 공격과 독립적인 예약을 시작합니다.
        /// [EN] Starts an independent attack cue from presentation settings expressed in seconds.
        /// </summary>
        public void BeginAttack(int actor, int tick, float duration, float releaseDelay, float impactDelay)
        {
            if (actor < 0) throw new ArgumentOutOfRangeException(nameof(actor));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration),
                    "Attack duration must be positive finite seconds.");
            if (float.IsNaN(releaseDelay) || float.IsInfinity(releaseDelay) || releaseDelay < 0 ||
                releaseDelay >= duration)
                throw new ArgumentOutOfRangeException(nameof(releaseDelay),
                    "Release delay must be finite seconds from zero up to, but not including, attack duration.");
            if (float.IsNaN(impactDelay) || float.IsInfinity(impactDelay) || impactDelay < releaseDelay)
                throw new ArgumentOutOfRangeException(nameof(impactDelay),
                    "Impact delay must be finite seconds at or after release.");
            _attacks.Add(new AttackCue(actor, tick, Time, duration, releaseDelay, impactDelay));
        }

        /// <summary>
        /// 同期消費中に同じ actor と tick の最後の予約へイベント値をコピーします。
        /// [KO] 동기 소비 중 같은 actor와 tick의 마지막 예약에 이벤트 값을 복사합니다.
        /// [EN] Copies the event value into the latest matching actor and tick cue during synchronous consumption.
        /// </summary>
        public void ReserveDamage(int actor, BattleEvent damage)
        {
            for (var index = _attacks.Count - 1; index >= 0; index--)
            {
                var cue = _attacks[index];
                if (cue.Actor != actor || cue.Tick != damage.Tick) continue;
                cue.AddDamage(damage);
                return;
            }

            throw new InvalidOperationException("Committed damage requires its attack cue.");
        }

        /// <summary>
        /// 発射から命中までの飛行率を返します。瞬時命中と命中済みの攻撃には飛行を出しません。
        /// [KO] 발사부터 명중까지의 비행 비율을 반환하며 즉시 명중·명중 완료 공격에는 비행을 표시하지 않습니다.
        /// [EN] Returns flight progress between release and impact, excluding instant and already delivered impacts.
        /// </summary>
        public bool TryGetProjectileProgress(int index, out float progress)
        {
            var cue = _attacks[index];
            var elapsed = Time - cue.Started;
            progress = 0;
            if (cue.HasImpacted || elapsed < cue.ReleaseDelay || cue.ImpactDelay <= cue.ReleaseDelay) return false;
            progress = (float)Math.Min(1, (elapsed - cue.ReleaseDelay) / (cue.ImpactDelay - cue.ReleaseDelay));
            return true;
        }

        /// <summary>
        /// 命中時刻に到着状態を一度だけ変更し、Presenter に被害表示を許可します。
        /// [KO] 명중 시각에 도착 상태를 한 번만 변경해 Presenter에 피해 표시를 허용합니다.
        /// [EN] Marks arrival once at impact time, allowing the presenter to deliver damage presentation.
        /// </summary>
        public bool TryImpact(int index)
        {
            var cue = _attacks[index];
            if (cue.HasImpacted || Time - cue.Started + ImpactToleranceSeconds < cue.ImpactDelay) return false;
            cue.HasImpacted = true;
            return true;
        }

        /// <summary>
        /// 命中とモーションが両方終わった予約だけを除去します。呼出側は除去後に添字を一つ戻します。
        /// [KO] 명중과 모션이 모두 끝난 예약만 제거하며 호출자는 제거 후 인덱스를 하나 되돌립니다.
        /// [EN] Removes a cue only after impact and motion finish; the caller steps its index back after removal.
        /// </summary>
        public bool RemoveIfFinished(int index)
        {
            var cue = _attacks[index];
            if (!cue.HasImpacted || Time - cue.Started < Math.Max(cue.Duration, cue.ImpactDelay)) return false;
            _attacks.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 表示 HP が零で、自分の最後の命中だけを届け終えたか判断します。他人の攻撃は待ちません。
        /// [KO] 표시 HP가 0이고 자신의 마지막 명중 전달을 끝냈는지 판단하며 다른 유닛의 공격은 기다리지 않습니다.
        /// [EN] Checks zero displayed health and delivery of this actor's final impact, without waiting for other actors.
        /// </summary>
        public bool CanPresentDeath(int actor, int displayedHealth, bool isDead)
        {
            if (displayedHealth != 0 || isDead) return false;
            foreach (var cue in _attacks)
                if (cue.Actor == actor && !cue.HasImpacted)
                    return false;
            return true;
        }

        /// <summary>
        /// Presenter が死亡表示を実際に開始した直後、その時刻を記録します。
        /// [KO] Presenter가 실제 사망 표시를 시작한 직후 그 시각을 기록합니다.
        /// [EN] Records the time immediately after the presenter actually starts a death presentation.
        /// </summary>
        public void RecordDeath() => _lastDeathTime = Time;

        /// <summary>
        /// Runner の確定結果を保存し、残っている表示時間は止めません。
        /// [KO] Runner의 확정 결과를 저장하고 남은 표시 시간은 멈추지 않습니다.
        /// [EN] Stores the runner's committed result without stopping remaining presentation time.
        /// </summary>
        public void CommitResult(Define.BattleResult result) => Result = result;

        /// <summary>
        /// Presenter の解除で全予約を捨てます。時計と確定結果は再利用用に初期化しません。
        /// [KO] Presenter 해제 시 모든 예약을 버리며 시계와 확정 결과를 재사용용으로 초기화하지 않습니다.
        /// [EN] Discards all cues on presenter disposal without resetting time or result for reuse.
        /// </summary>
        public void Clear() => _attacks.Clear();
    }
}
