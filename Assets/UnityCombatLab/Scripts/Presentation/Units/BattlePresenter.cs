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
    /// 戦闘イベントを受け取り、各攻撃の命中時刻に HP と演出を反映します。
    /// [KO] 전투 이벤트를 받아 각 공격의 명중 시각에 HP와 연출을 반영합니다.
    /// [EN] Receives battle events and applies health and effects at each attack's impact time.
    /// </summary>
    public sealed class BattlePresenter : IDisposable
    {
        /// <summary>
        /// 攻撃ごとの発射・命中・終了時刻と、勝敗を公開する時刻を管理します。
        /// [KO] 공격별 발사·명중·종료 시각과 승패를 공개할 시각을 관리합니다.
        /// [EN] Tracks each attack's release, impact and completion timings and when the outcome can be published.
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
        /// 命中時刻に合わせて HUD へ通知する HP・盾・勝敗。
        /// [KO] 명중 시각에 맞춰 HUD에 알릴 HP·방어막·승패입니다.
        /// [EN] Health, shields and outcome notified to the HUD at presentation times.
        /// </summary>
        private readonly BattlePresentationReadModel _model;

        /// <summary>
        /// ユニット ID から、View と表示状態の配列番号を引く対応表。
        /// [KO] 유닛 ID로 View와 표시 상태의 배열 번호를 찾을 대응표입니다.
        /// [EN] Maps unit IDs to indices in the view and presentation-state arrays.
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
        /// 命中または演出終了を待っている攻撃数。
        /// [KO] 명중 또는 연출 종료를 기다리는 공격 수입니다.
        /// [EN] Number of attacks awaiting impact or animation completion.
        /// </summary>
        public int PendingAttackCount => _timeline.Attacks.Count;

        /// <summary>
        /// 全攻撃の命中と死亡演出を終え、勝敗を公開したかを示します。
        /// [KO] 모든 공격의 명중과 사망 연출을 끝내고 승패를 공개했는지 나타냅니다.
        /// [EN] Whether all impacts and death effects finished and the outcome was published.
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
        /// 攻撃開始を View へ伝え、被害をその攻撃の命中時刻に表示するよう登録します。
        /// [KO] View에 공격 시작을 알리고 피해를 해당 공격의 명중 시각에 표시하도록 등록합니다.
        /// [EN] Starts attack presentation on the view and schedules its damage for impact.
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
                // 後の命中表示に使う被害の値を、この攻撃に保存する。
                _timeline.ReserveDamage(actor, item);
                _model.Units[_indices[item.TargetId]].ReserveDamage(item.HealthDamage);
            }
        }

        /// <summary>
        /// 毎フレーム、各攻撃の進行・死亡表示・勝敗公開を順に更新します。
        /// [KO] 매 프레임 각 공격 진행·사망 표시·승패 공개를 순서대로 갱신합니다.
        /// [EN] Updates attacks, death presentation and outcome publication in order each frame.
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
                // 命中した被害だけを表示 HP と盾から引く。
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
