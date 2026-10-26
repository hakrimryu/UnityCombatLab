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
        /// 最後の死亡表示から勝敗公開まで待つ秒数。
        /// [KO] 마지막 사망 표시부터 승패 공개까지 기다릴 초 단위 시간입니다.
        /// [EN] Seconds to wait between the last death presentation and outcome publication.
        /// </summary>
        private const double DeathResultDelaySeconds = 0.82;

        /// <summary>
        /// 浮動小数点の丸めで命中を一フレーム遅らせないための秒単位の許容差。
        /// [KO] 부동소수점 반올림 때문에 명중이 한 프레임 늦어지지 않도록 둔 초 단위 허용 오차입니다.
        /// [EN] Tolerance in seconds preventing floating-point rounding from postponing impact by one frame.
        /// </summary>
        private const double ImpactToleranceSeconds = 0.00001;

        /// <summary>
        /// 開始順に保持し、各命中と演出終了を確認する攻撃一覧。
        /// [KO] 시작 순서로 보관하며 각 명중과 연출 종료를 확인할 공격 목록입니다.
        /// [EN] Attacks kept in start order for checking each impact and animation completion.
        /// </summary>
        private readonly List<AttackCue> _attacks = new();

        /// <summary>
        /// 最後に死亡表示を始めた時刻。結果公開までの待機時間に使います。
        /// [KO] 마지막 사망 표시를 시작한 시각이며 결과 공개까지의 대기 시간에 사용합니다.
        /// [EN] Time the last death presentation began, used for the outcome delay.
        /// </summary>
        private double? _lastDeathTime;

        /// <summary>
        /// 一回の攻撃の発射・命中時刻と、表示する被害を保持します。
        /// [KO] 한 번의 공격의 발사·명중 시각과 표시할 피해를 보관합니다.
        /// [EN] Stores release and impact timings and damage for one attack.
        /// </summary>
        public sealed class AttackCue
        {
            /// <summary>
            /// この攻撃で表示する、イベントからコピーした被害。
            /// [KO] 이번 공격에서 표시할 이벤트에서 복사한 피해입니다.
            /// [EN] Damage copied from events for presentation by this attack.
            /// </summary>
            private readonly List<BattleEvent> _damage = new(Define.SlotsPerTeam);

            /// <summary>
            /// 同じ攻撃の全被害後に表示する回復イベント。
            /// [KO] 같은 공격의 모든 피해 후 표시할 회복 이벤트입니다.
            /// [EN] Healing events presented after all damage from this attack.
            /// </summary>
            private readonly List<BattleEvent> _healing = new();

            /// <summary>
            /// 命中後に対象モデルへ渡す回復一覧。
            /// [KO] 명중 후 대상 모델에 전달할 회복 목록입니다.
            /// [EN] Healing passed to target models after impact.
            /// </summary>
            public IReadOnlyList<BattleEvent> Healing => _healing;

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
            /// 単体攻撃の対象番号。全体攻撃では対象を固定せず -1 を使います。
            /// [KO] 단일 공격 대상 번호이며 광역 공격은 대상을 고정하지 않고 -1을 사용합니다.
            /// [EN] Single-target view index, or -1 for an area attack without one fixed target.
            /// </summary>
            public int Target { get; }

            /// <summary>
            /// 攻撃イベントを受けた時点で確定した、強攻撃モーションの指定。
            /// [KO] 공격 이벤트를 받은 시점에 확정한 특수 공격 모션 지정입니다.
            /// [EN] Special-motion choice captured when the attack event was received.
            /// </summary>
            public bool IsSpecial { get; }

            /// <summary>
            /// 待機中の攻撃モーションを一度だけ開始するための状態。
            /// [KO] 대기 중인 공격 모션을 한 번만 시작하기 위한 상태입니다.
            /// [EN] State ensuring a queued attack's motion starts only once.
            /// </summary>
            internal bool HasStartedMotion { get; set; }

            /// <summary>
            /// 同じ攻撃者の前の演出が終わり、この攻撃を開始できる表示時計の秒数。
            /// [KO] 같은 공격자의 이전 연출이 끝나 이번 공격을 시작할 수 있는 표시 시계의 초 단위 시각입니다.
            /// [EN] Presentation-clock time when this attack may start after the same actor's preceding cue finishes.
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
                float impactDelay, int target, bool isSpecial)
            {
                Actor = actor;
                Tick = tick;
                Target = target;
                IsSpecial = isSpecial;
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
            internal void AddDamage(BattleEvent damage)
                => _damage.Add(damage);

            /// <summary>
            /// 原因の攻撃に回復イベントを値として追加します。
            /// [KO] 원인 공격에 회복 이벤트를 값으로 추가합니다.
            /// [EN] Adds a healing event by value to its source attack.
            /// </summary>
            internal void AddHealing(BattleEvent healing)
                => _healing.Add(healing);
        }

        /// <summary>
        /// EntryPoint が渡すゲーム内経過秒を積み上げた、演出の時刻。
        /// [KO] EntryPoint가 전달한 게임 경과 시간을 누적한 연출 시각입니다.
        /// [EN] Presentation time accumulated from game-time seconds supplied by EntryPoint.
        /// </summary>
        public double Time { get; private set; }

        /// <summary>
        /// Presenter が開始順に読み取る攻撃一覧。
        /// [KO] Presenter가 시작 순서로 읽을 공격 목록입니다.
        /// [EN] Attack list read by the presenter in start order.
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
        public BattlePresentationTimeline()
            => Attacks = _attacks.AsReadOnly();

        /// <summary>
        /// Presenter が渡した経過秒を、演出の時刻に加算します。
        /// [KO] Presenter가 전달한 경과 시간을 연출 시각에 더합니다.
        /// [EN] Adds elapsed seconds supplied by the presenter to presentation time.
        /// </summary>
        public void Advance(double scaledDeltaSeconds)
        {
            if (double.IsNaN(scaledDeltaSeconds) || double.IsInfinity(scaledDeltaSeconds) || scaledDeltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaSeconds));

            Time += scaledDeltaSeconds;
        }

        /// <summary>
        /// 表示設定を秒で受け取り、同じ攻撃者の前の演出が終わる時刻以降へ予約します。
        /// [KO] 표시 설정을 초 단위로 받아 같은 공격자의 이전 연출이 끝나는 시각 이후로 예약합니다.
        /// [EN] Schedules an attack from timing settings in seconds after the same actor's preceding presentation finishes.
        /// </summary>
        /// <param name="actor">
        /// Presenter の攻撃者番号。
        /// [KO] Presenter의 공격자 번호입니다.
        /// [EN] Attacker index in the presenter.
        /// </param>
        /// <param name="tick">
        /// 被害イベントと対応付ける Domain tick。
        /// [KO] 피해 이벤트와 대응시킬 Domain 틱입니다.
        /// [EN] Domain tick used to match damage events.
        /// </param>
        /// <param name="duration">
        /// 元の姿勢へ戻るまでのモーション秒数。
        /// [KO] 원래 자세로 돌아올 때까지의 모션 시간(초)입니다.
        /// [EN] Motion duration in seconds, including return to the home pose.
        /// </param>
        /// <param name="releaseDelay">
        /// モーション開始から発射までの秒数。
        /// [KO] 모션 시작부터 발사까지의 시간(초)입니다.
        /// [EN] Seconds from motion start to projectile release.
        /// </param>
        /// <param name="impactDelay">
        /// モーション開始から命中までの秒数。
        /// [KO] 모션 시작부터 명중까지의 시간(초)입니다.
        /// [EN] Seconds from motion start to impact.
        /// </param>
        /// <param name="target">
        /// Presenter の対象番号。全体攻撃では -1。
        /// [KO] Presenter의 대상 번호이며 광역 공격은 -1입니다.
        /// [EN] Presenter target index, or -1 for an area attack.
        /// </param>
        /// <param name="isSpecial">
        /// 攻撃受信時に確定した強攻撃モーションの指定。
        /// [KO] 공격 수신 때 확정한 특수 공격 모션 지정입니다.
        /// [EN] Special-motion choice captured when the attack was received.
        /// </param>
        /// <returns>
        /// 今回追加した予約の番号。予約一覧を変更する前に使用します。
        /// [KO] 이번에 추가한 예약 번호이며 예약 목록을 변경하기 전에 사용합니다.
        /// [EN] Index of the new cue, valid until the cue list is modified.
        /// </returns>
        public int BeginAttack(int actor, int tick, float duration, float releaseDelay, float impactDelay,
            int target = -1, bool isSpecial = false)
        {
            if (actor < 0)
                throw new ArgumentOutOfRangeException(nameof(actor));

            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));

            if (target < -1)
                throw new ArgumentOutOfRangeException(nameof(target));

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
            var started = Time;
            for (var index = _attacks.Count - 1; index >= 0; index--)
            {
                var previous = _attacks[index];
                if (previous.Actor != actor)
                    continue;

                // 同じユニットのモーションと三つの効果枠は、前の命中と帰還を終えてから再使用する。
                started = Math.Max(started, previous.Started + Math.Max(previous.Duration, previous.ImpactDelay));
                break;
            }
            _attacks.Add(new AttackCue(actor, tick, started, duration, releaseDelay, impactDelay, target, isSpecial));
            return _attacks.Count - 1;
        }

        /// <summary>
        /// 開始予定時刻に達した攻撃だけに、モーションの開始を一度許可します。
        /// [KO] 시작 예정 시각에 도달한 공격에만 모션 시작을 한 번 허용합니다.
        /// [EN] Allows motion to start once for an attack whose scheduled start time has arrived.
        /// </summary>
        public bool TryStartMotion(int index)
        {
            var cue = _attacks[index];
            if (cue.HasStartedMotion || Time < cue.Started)
                return false;

            cue.HasStartedMotion = true;
            return true;
        }

        /// <summary>
        /// 同じ攻撃者と tick で始まった攻撃へ、表示する被害を追加します。
        /// [KO] 같은 공격자와 tick에서 시작한 공격에 표시할 피해를 추가합니다.
        /// [EN] Adds presentation damage to the attack matching the actor and tick.
        /// </summary>
        public void ReserveDamage(int actor, BattleEvent damage)
        {
            for (var index = _attacks.Count - 1; index >= 0; index--)
            {
                var cue = _attacks[index];
                if (cue.Actor != actor || cue.Tick != damage.Tick)
                    continue;

                cue.AddDamage(damage);
                return;
            }

            throw new InvalidOperationException("Committed damage requires its attack cue.");
        }

        /// <summary>
        /// 撃破回復を原因の攻撃へ結び、被害表示の後まで待たせます。
        /// [KO] 처치 회복을 원인 공격에 연결하고 피해 표시 후까지 대기시킵니다.
        /// [EN] Associates kill healing with its source attack and waits until damage presentation.
        /// </summary>
        public void ReserveHealing(int actor, BattleEvent item)
        {
            for (var index = _attacks.Count - 1; index >= 0; index--)
            {
                if (_attacks[index].Actor != actor || _attacks[index].Tick != item.Tick)
                    continue;

                _attacks[index].AddHealing(item);
                return;
            }

            throw new InvalidOperationException("Healing requires its source attack cue.");
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
            if (cue.HasImpacted || elapsed < cue.ReleaseDelay || cue.ImpactDelay <= cue.ReleaseDelay)
                return false;

            progress = (float)Math.Min(1, (elapsed - cue.ReleaseDelay) / (cue.ImpactDelay - cue.ReleaseDelay));
            return true;
        }

        /// <summary>
        /// 命中時刻に達した攻撃を一度だけ命中済みにし、その時だけ true を返します。
        /// [KO] 명중 시각에 도달한 공격을 한 번만 명중 상태로 바꾸고 그때만 true를 반환합니다.
        /// [EN] Marks an attack impacted once its impact time is reached and returns true only for that change.
        /// </summary>
        public bool TryImpact(int index)
        {
            var cue = _attacks[index];
            if (cue.HasImpacted || Time - cue.Started + ImpactToleranceSeconds < cue.ImpactDelay)
                return false;

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
            if (!cue.HasImpacted || Time - cue.Started < Math.Max(cue.Duration, cue.ImpactDelay))
                return false;

            _attacks.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 表示 HP が 0 で、自身の全攻撃が命中済みなら死亡表示を許可します。
        /// [KO] 표시 HP가 0이고 자신의 모든 공격이 명중했다면 사망 표시를 허용합니다.
        /// [EN] Allows death presentation when displayed health is zero and all of this unit's attacks have impacted.
        /// </summary>
        public bool CanPresentDeath(int actor, int displayedHealth, bool isDead)
        {
            if (displayedHealth != 0 || isDead)
                return false;

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
        public void RecordDeath()
            => _lastDeathTime = Time;

        /// <summary>
        /// Runner で確定した勝敗を、演出終了後に公開するため保存します。
        /// [KO] Runner에서 확정한 승패를 연출 종료 후 공개하도록 저장합니다.
        /// [EN] Stores the runner's committed outcome for publication after effects finish.
        /// </summary>
        public void CommitResult(Define.BattleResult result)
            => Result = result;

        /// <summary>
        /// Presenter の終了時に、残っている攻撃を全て取り除きます。
        /// [KO] Presenter 종료 시 남은 공격을 모두 제거합니다.
        /// [EN] Removes all remaining attacks when the presenter ends.
        /// </summary>
        public void Clear()
            => _attacks.Clear();
    }
}
