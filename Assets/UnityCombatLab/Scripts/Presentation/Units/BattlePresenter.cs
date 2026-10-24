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
        /// 表示時計と予約の判断だけを所有する純粋 C#。View と HUD への呼出しはこの Presenter が担当します。
        /// [KO] 표시 시계와 예약 판단만 소유하는 순수 C#이며 View·HUD 호출은 이 Presenter가 담당합니다.
        /// [EN] Pure C# owner of presentation time and cue decisions; this presenter owns view and HUD calls.
        /// </summary>
        private readonly BattlePresentationTimeline _timeline = new();

        /// <summary>
        /// 前列同士の接近を双方で半分ずつ分担する比率。
        /// [KO] 전열끼리의 접근을 양쪽이 절반씩 분담하는 비율입니다.
        /// [EN] Fraction splitting front-line approach equally between both actors.
        /// </summary>
        private const float SharedFrontApproachFraction = 0.5f;

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
        public int PendingAttackCount => _timeline.Attacks.Count;

        /// <summary>
        /// 最後の命中・死亡まで画面に届いた状態。
        /// [KO] 마지막 명중·사망까지 화면에 도착한 상태입니다.
        /// [EN] Whether final hits and deaths have reached the screen.
        /// </summary>
        public bool IsComplete { get; private set; }

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
                _timeline.CommitResult(item.Result);
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
                view.ShowAttack(_runId, targetPosition, isMelee, isSpecial,
                    shareApproach ? SharedFrontApproachFraction : 1f);
                _timeline.BeginAttack(actor, item.Tick, view.AttackDuration, view.ReleaseDelay, view.ImpactDelay);
            }
            else if (item.Kind == Define.BattleEventKind.DamageApplied)
            {
                var actor = _indices[item.ActorId];
                // コピーしたイベントだけを予約し、Runner の再利用バッファを非同期表示へ渡さない。
                _timeline.ReserveDamage(actor, item);
                _model.Units[_indices[item.TargetId]].ReserveDamage(item.HealthDamage);
            }
            // 死亡は既に確定しているが、画面では予約被害が到着して HP 0 になった時に一度だけ示す。
        }

        /// <summary>
        /// EntryPoint が毎フレーム呼び、全攻撃を同時に進めます。誰かの演出待ちで戦闘 tick を止めません。
        /// [KO] EntryPoint가 매 프레임 호출해 모든 공격을 동시에 진행하며 누군가의 연출 때문에 전투 틱을 멈추지 않습니다.
        /// [EN] EntryPoint advances every attack concurrently each frame, never blocking combat ticks for another unit's animation.
        /// </summary>
        public void Advance(double scaledDeltaSeconds)
        {
            if (_isDisposed || IsComplete)
            {
                return;
            }

            _timeline.Advance(scaledDeltaSeconds);
            // 先に全命中を反映し、最後の攻撃を届けた死亡だけを表示してから結果を公開する。
            AdvanceAttacks();
            PresentDeaths();
            CompleteWhenReady();
        }

        /// <summary>
        /// 全予約を作成順に同時進行させ、命中済みで演出時間も過ぎた攻撃だけを除去します。
        /// [KO] 모든 예약을 생성 순서로 동시에 진행하고 명중 및 연출 시간이 끝난 공격만 제거합니다.
        /// [EN] Advances all cues concurrently in creation order and removes attacks only after impact and presentation finish.
        /// </summary>
        private void AdvanceAttacks()
        {
            for (var index = 0; index < _timeline.Attacks.Count; index++)
            {
                var cue = _timeline.Attacks[index];
                var actor = _views[cue.Actor];
                AdvanceProjectiles(index, cue, actor);
                ApplyImpactWhenReady(index, cue, actor);
                if (_timeline.RemoveIfFinished(index)) index--;
            }
        }

        /// <summary>
        /// 発射から命中までの間だけ、各対象の現在位置へ投射物を進めます。
        /// [KO] 발사부터 명중 사이에만 각 대상의 현재 위치로 투사체를 진행합니다.
        /// [EN] Advances projectiles toward each target current position only between release and impact.
        /// </summary>
        private void AdvanceProjectiles(int index, BattlePresentationTimeline.AttackCue cue, UnitView actor)
        {
            if (!_timeline.TryGetProjectileProgress(index, out var progress)) return;
            for (var target = 0; target < cue.Damage.Count; target++)
            {
                var targetView = _views[_indices[cue.Damage[target].TargetId]];
                actor.PresentProjectile(target, targetView.ImpactPosition, progress);
            }
        }

        /// <summary>
        /// 命中時刻に一度だけ HP、効果、被弾、数字をこの順で反映します。
        /// [KO] 명중 시각에 한 번만 HP·효과·피격·숫자를 이 순서로 반영합니다.
        /// [EN] Applies health, effects, hit reaction and numbers once, in that order, at impact time.
        /// </summary>
        private void ApplyImpactWhenReady(int index, BattlePresentationTimeline.AttackCue cue, UnitView actor)
        {
            if (!_timeline.TryImpact(index)) return;
            actor.HideProjectiles();
            actor.ShowStrike(_runId);
            for (var hit = 0; hit < cue.Damage.Count; hit++)
            {
                var damage = cue.Damage[hit];
                var target = _indices[damage.TargetId];
                // 到着順が異なっても確定損失を一度引く。古い絶対 HP は代入しない。
                _model.Units[target].ApplyDamage(damage.HealthDamage, damage.ShieldDamage);
                actor.ShowImpact(_runId, hit, _views[target].ImpactPosition);
                _views[target].ShowHit(_runId, actor.ImpactPosition);
                _hud.Units[target].ShowDamage(damage.HealthDamage, damage.ShieldDamage);
            }
        }

        /// <summary>
        /// 表示 HP が零になり、自分の最後の命中を届けたユニットだけを倒します。
        /// [KO] 표시 HP가 0이고 자신의 마지막 명중을 전달한 유닛만 사망시킵니다.
        /// [EN] Shows death only after displayed health reaches zero and the unit delivers its final impact.
        /// </summary>
        private void PresentDeaths()
        {
            for (var index = 0; index < _views.Length; index++)
            {
                if (!_timeline.CanPresentDeath(index, _model.Units[index].Health.CurrentValue, _views[index].IsDead))
                    continue;
                _views[index].ShowDeath(_runId);
                _timeline.RecordDeath();
            }
        }

        /// <summary>
        /// 確定結果があり、全攻撃と死亡待機が終わった時だけ結果を公開します。
        /// [KO] 확정 결과가 있고 모든 공격과 사망 대기가 끝났을 때만 결과를 공개합니다.
        /// [EN] Publishes the committed result only after all attacks and the death wait finish.
        /// </summary>
        private void CompleteWhenReady()
        {
            if (!_timeline.CanComplete) return;
            foreach (var view in _views)
            {
                view.FinishBattle();
            }

            _model.Complete(_timeline.Result);
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
            _timeline.Clear();
            foreach (var view in _views)
                if (view != null)
                    view.Release();
        }
    }
}
