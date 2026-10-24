using System;
using System.Collections.Generic;
using System.Linq;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.UI;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// イベント値をその場でコピーし、各攻撃の命中へ表示を届けます。Domain の進行は待たせません。
    /// [KO] 이벤트 값을 즉시 복사해 각 공격의 명중에 표시를 전달하며 Domain 진행은 기다리게 하지 않습니다.
    /// [EN] Copies event values immediately and presents each attack at impact without delaying Domain progression.
    /// </summary>
    public sealed class BattlePresenter : IDisposable
    {
        /// <summary>
        /// 確定イベントを同期的に受け取る借用 Runner。
        /// [KO] 확정 이벤트를 동기로 받는 빌린 Runner입니다.
        /// [EN] Borrowed runner supplying committed events synchronously.
        /// </summary>
        private readonly BattleRunner _runner;

        /// <summary>
        /// 命中を画面へ届ける状態。戦闘計算は含みません。
        /// [KO] 명중을 화면으로 전달할 상태이며 전투 계산은 포함하지 않습니다.
        /// [EN] Presentation state receiving impacts without combat calculation.
        /// </summary>
        private readonly BattlePresentationReadModel _model;

        /// <summary>
        /// ID と固定表示枠の対応、および借用する View。
        /// [KO] ID와 고정 표시 슬롯의 대응 및 빌린 View입니다.
        /// [EN] ID-to-slot mapping and borrowed views.
        /// </summary>
        private readonly Dictionary<int, int> _indices;

        /// <summary>
        /// 表示モデルと同じ固定順で借用するユニット。
        /// [KO] 표시 모델과 같은 고정 순서로 빌리는 유닛입니다.
        /// [EN] Borrowed units in the same fixed order as presentation state.
        /// </summary>
        private readonly UnitView[] _views;

        /// <summary>
        /// 命中時に各ユニットの被害数字を渡す HUD。
        /// [KO] 명중 때 유닛별 피해 숫자를 전달할 HUD입니다.
        /// [EN] HUD receiving each unit damage number at impact.
        /// </summary>
        private readonly BattleHudView _hud;

        /// <summary>
        /// 古い戦闘・ウェーブの通知を拒否する識別子。
        /// [KO] 이전 전투·웨이브 알림을 거부할 식별자입니다.
        /// [EN] Identifiers rejecting stale battle and wave notifications.
        /// </summary>
        private readonly Guid _runId;

        /// <summary>
        /// 別のウェーブの通知を拒否する番号。
        /// [KO] 다른 웨이브 알림을 거부할 번호입니다.
        /// [EN] Wave number used to reject unrelated notifications.
        /// </summary>
        private readonly int _wave;

        /// <summary>
        /// 他の攻撃を待たず並行して進む、この戦闘の予約。
        /// [KO] 다른 공격을 기다리지 않고 병렬로 진행할 이번 전투 예약입니다.
        /// [EN] Battle-owned cues progressing concurrently without waiting for other attacks.
        /// </summary>
        private readonly List<AttackCue> _attacks = new();

        /// <summary>
        /// 表示だけの経過秒・最後の死亡・確定勝敗と解除状態。
        /// [KO] 표시 전용 경과 시간·마지막 사망·확정 승패와 해제 상태입니다.
        /// [EN] Presentation clock, final death time, committed result and disposal state.
        /// </summary>
        private double _time;

        /// <summary>
        /// 最後に死亡表示を開始した秒数。初期値は待機を発生させない過去。
        /// [KO] 마지막 사망 표시를 시작한 시각(초)이며 초기값은 대기를 만들지 않는 과거입니다.
        /// [EN] Last death start in seconds, initially far enough in the past to cause no wait.
        /// </summary>
        private double _lastDeathTime = -100;

        /// <summary>
        /// Runner が既に確定した、最後の演出後に表示する勝敗。
        /// [KO] Runner가 이미 확정했고 마지막 연출 후 표시할 승패입니다.
        /// [EN] Runner-committed result published after final effects.
        /// </summary>
        private Define.BattleResult _committedResult;

        /// <summary>
        /// 解除後のイベントとフレーム更新を拒否する状態。
        /// [KO] 해제 후 이벤트·프레임 갱신을 거부할 상태입니다.
        /// [EN] State rejecting events and frame updates after disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 未完了の同時攻撃数。終了と検証で使用します。
        /// [KO] 미완료 동시 공격 수이며 종료·검증에 사용합니다.
        /// [EN] Number of unfinished simultaneous attacks used for completion and validation.
        /// </summary>
        public int PendingAttackCount => _attacks.Count;

        /// <summary>
        /// 最後の命中・死亡まで画面に届いた状態。
        /// [KO] 마지막 명중·사망까지 화면에 도착한 상태입니다.
        /// [EN] Whether final hits and deaths have reached the screen.
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// 一回の攻撃へコピーした値だけを持つ内部予約。元のイベント配列は保持しません。
        /// [KO] 공격 한 번에 복사한 값만 보관하는 내부 예약이며 원래 이벤트 배열은 보관하지 않습니다.
        /// [EN] Internal cue containing only copied values for one attack, never the reused event buffer.
        /// </summary>
        private sealed class AttackCue
        {
            /// <summary>
            /// 同じ actor・tick の被害を、この開始時刻と攻撃表示へ結び付けます。
            /// [KO] 같은 actor·tick 피해를 이 시작 시각·공격 표시에 연결합니다.
            /// [EN] Connects damage from the same actor and tick to this start time and attack.
            /// </summary>
            public readonly int Actor;

            /// <summary>
            /// この攻撃と同じ tick の被害だけを探す番号。
            /// [KO] 이 공격과 같은 틱의 피해만 찾을 번호입니다.
            /// [EN] Tick identifying damage belonging to this attack.
            /// </summary>
            public readonly int Tick;

            /// <summary>
            /// 他の攻撃とは独立した、この攻撃の開始秒数。
            /// [KO] 다른 공격과 독립적인 이 공격 시작 시각(초)입니다.
            /// [EN] Start time in seconds, independent of other attacks.
            /// </summary>
            public readonly double Started;

            /// <summary>
            /// 同期消費中に値としてコピーした、最大三対象の確定被害。
            /// [KO] 동기 소비 중 값으로 복사한 최대 세 대상의 확정 피해입니다.
            /// [EN] Committed damage copied by value during synchronous consumption for up to three targets.
            /// </summary>
            public readonly List<BattleEvent> Damage = new(Define.SlotsPerTeam);

            /// <summary>
            /// 同じ確定被害を二度表示しないための到着済み状態。
            /// [KO] 같은 확정 피해를 두 번 표시하지 않기 위한 도착 상태입니다.
            /// [EN] Arrival flag preventing the same committed loss from being presented twice.
            /// </summary>
            public bool HasImpacted;

            /// <summary>
            /// 同期通知の時刻と行動者を一度記録します。
            /// [KO] 동기 알림 시각과 행동자를 한 번 기록합니다.
            /// [EN] Records the synchronous notification time and actor once.
            /// </summary>
            public AttackCue(int actor, int tick, double started)
            {
                Actor = actor;
                Tick = tick;
                Started = started;
            }
        }

        /// <summary>
        /// EntryPoint が同じ順のモデルと View を渡し、この戦闘のイベント接続を開始します。
        /// [KO] EntryPoint가 같은 순서의 모델·View를 전달해 이번 전투의 이벤트 연결을 시작합니다.
        /// [EN] EntryPoint supplies matching model and view order to connect this battle's events.
        /// </summary>
        public BattlePresenter(BattleRunner runner, Guid runId, int wave, UnitView[] views, BattleHudView hud,
            BattlePresentationReadModel model)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _hud = hud != null ? hud : throw new ArgumentNullException(nameof(hud));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (views == null || views.Length != runner.ReadModel.Units.Count || views.Any(v => v == null) ||
                views.Distinct().Count() != views.Length || model.Units.Count != views.Length)
                throw new ArgumentException("Each battle slot requires its own view and presentation state.",
                    nameof(views));
            _views = (UnitView[])views.Clone();
            _indices = runner.ReadModel.Units.Select((u, i) => (u.Input.Id, i)).ToDictionary(x => x.Id, x => x.i);
            _runId = runId;
            _wave = wave;
            foreach (var view in _views)
            {
                view.Bind(runId);
            }

            _runner.EventOccurred += HandleEventOccurred;
        }

        /// <summary>
        /// 同期消費中に必要な値をコピーし、攻撃は即開始、被害は対応する攻撃へ予約します。
        /// [KO] 동기 소비 중 필요한 값을 복사하고 공격은 즉시 시작하며 피해는 대응 공격에 예약합니다.
        /// [EN] Copies needed values synchronously, starts attacks immediately and reserves damage with the matching attack.
        /// </summary>
        private void HandleEventOccurred(BattleEvent item)
        {
            if (_isDisposed || item.RunId != _runId || item.Wave != _wave)
            {
                return;
            }

            if (item.Kind == Define.BattleEventKind.BattleEnded)
            {
                _committedResult = item.Result;
                return;
            }

            if (item.Kind == Define.BattleEventKind.AttackExecuted)
            {
                var actor = _indices[item.ActorId];
                var input = _model.Units[actor].Input;
                var view = _views[actor];
                var target = item.TargetId == 0 ? -1 : _indices[item.TargetId];
                var isArea = target < 0;
                var isMelee = !isArea && input.Role is Define.UnitRole.Knight or
                    Define.UnitRole.SkeletonMelee or Define.UnitRole.SkeletonCaptain;
                var state = _runner.GetUnit(input.Team, input.Slot);
                var isSpecial = isArea || (input.Role == Define.UnitRole.Ranger &&
                                           state.AttackCount % Define.RangerSpecialAttackPeriod == 0);
                var targetPosition = isArea ? view.transform.position : _views[target].transform.position;
                var shareApproach = !isArea && input.Slot == Define.BattleSlot.Front &&
                                    _model.Units[target].Input.Slot == Define.BattleSlot.Front;
                view.ShowAttack(_runId, targetPosition, isMelee, isSpecial, shareApproach ? 0.5f : 1f);
                _attacks.Add(new AttackCue(actor, item.Tick, _time));
            }
            else if (item.Kind == Define.BattleEventKind.DamageApplied)
            {
                var actor = _indices[item.ActorId];
                var cue = _attacks.LastOrDefault(a => a.Actor == actor && a.Tick == item.Tick);
                if (cue == null)
                {
                    throw new InvalidOperationException("Committed damage requires its attack cue.");
                }

                // BattleEvent は不変の値型。コピー後は Runner が Acknowledge して配列を再利用できる。
                cue.Damage.Add(item);
                _model.Units[_indices[item.TargetId]].ReserveDamage(item.HealthDamage);
            }
            // 死亡は既に確定しているが、画面では予約被害が到着して HP 0 になった時に一度だけ示す。
        }

        /// <summary>
        /// EntryPoint が毎フレーム呼び、全攻撃を同時に進めます。誰かの演出待ちで戦闘 tick を止めません。
        /// [KO] EntryPoint가 매 프레임 호출해 모든 공격을 동시에 진행하며 누군가의 연출 때문에 전투 틱을 멈추지 않습니다.
        /// [EN] EntryPoint advances every attack concurrently each frame, never blocking combat ticks for another unit's animation.
        /// </summary>
        public void Advance(double deltaSeconds)
        {
            if (_isDisposed || IsComplete)
            {
                return;
            }

            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _time += deltaSeconds;
            for (var index = 0; index < _attacks.Count; index++)
            {
                var cue = _attacks[index];
                var actor = _views[cue.Actor];
                var elapsed = _time - cue.Started;
                if (!cue.HasImpacted && elapsed >= actor.ReleaseDelay && actor.ImpactDelay > actor.ReleaseDelay)
                {
                    var progress = (float)Math.Min(1, (elapsed - actor.ReleaseDelay) /
                                                      (actor.ImpactDelay - actor.ReleaseDelay));
                    for (var target = 0; target < cue.Damage.Count; target++)
                    {
                        var targetView = _views[_indices[cue.Damage[target].TargetId]];
                        actor.PresentProjectile(target, targetView.ImpactPosition, progress);
                    }
                }

                if (!cue.HasImpacted && elapsed + 0.00001 >= actor.ImpactDelay)
                {
                    cue.HasImpacted = true;
                    actor.HideProjectiles();
                    actor.ShowStrike(_runId);
                    for (var hit = 0; hit < cue.Damage.Count; hit++)
                    {
                        var damage = cue.Damage[hit];
                        var target = _indices[damage.TargetId];
                        // 到着順が異なっても確定損失を一度引く。後で古い絶対 HP を代入して体力を戻さない。
                        _model.Units[target].ApplyDamage(damage.HealthDamage, damage.ShieldDamage);
                        actor.ShowImpact(_runId, hit, _views[target].ImpactPosition);
                        _views[target].ShowHit(_runId, actor.ImpactPosition);
                        _hud.Units[target].ShowDamage(damage.HealthDamage, damage.ShieldDamage);
                    }
                }

                if (cue.HasImpacted && elapsed >= Math.Max(actor.AttackDuration, actor.ImpactDelay))
                {
                    _attacks.RemoveAt(index);
                    index--;
                }
            }

            for (var index = 0; index < _views.Length; index++)
            {
                if (_model.Units[index].Health.CurrentValue != 0 || _views[index].IsDead)
                {
                    continue;
                }

                // 同じ tick で既に確定した最後の斬撃・発射だけを届けてから倒す。他人の攻撃は待たない。
                if (_attacks.Any(a => a.Actor == index && !a.HasImpacted))
                {
                    continue;
                }

                _views[index].ShowDeath(_runId);
                _lastDeathTime = _time;
            }

            if (_committedResult == Define.BattleResult.Ongoing || _attacks.Count != 0 ||
                _time < _lastDeathTime + 0.82) return;
            foreach (var view in _views)
            {
                view.FinishBattle();
            }

            _model.Complete(_committedResult);
            IsComplete = true;
        }

        /// <summary>
        /// イベントを外し全予約を捨ててから View の所有演出を解除します。
        /// [KO] 이벤트를 끊고 모든 예약을 버린 뒤 View의 소유 연출을 해제합니다.
        /// [EN] Detaches events and clears pending cues before releasing view-owned presentation.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _runner.EventOccurred -= HandleEventOccurred;
            _attacks.Clear();
            foreach (var view in _views)
                if (view != null)
                    view.Release();
        }
    }
}
