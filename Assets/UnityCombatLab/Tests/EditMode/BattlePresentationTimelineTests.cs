using System;
using NUnit.Framework;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.Units;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 攻撃の発射・命中時刻と、死亡・勝敗の待機条件を確認します。
    /// [KO] 공격 발사·명중 시각과 사망·승패 대기 조건을 확인합니다.
    /// [EN] Checks attack release and impact timings and conditions for death and outcome waits.
    /// </summary>
    public sealed class BattlePresentationTimelineTests
    {
        /// <summary>
        /// 指定した HP 損失が発生する戦闘を作り、その被害イベントを返します。
        /// [KO] 지정한 HP 손실이 발생할 전투를 만들고 해당 피해 이벤트를 반환합니다.
        /// [EN] Creates combat producing the specified health loss and returns its damage event.
        /// </summary>
        private static BattleEvent DamageOf(int healthDamage, int actorId = 1)
        {
            var inputs = new[]
            {
                new BattleUnitInput(actorId, Define.BattleTeam.Ally, Define.BattleSlot.Front,
                    Define.UnitRole.Knight, 100, healthDamage, 24),
                new BattleUnitInput(2, Define.BattleTeam.Enemy, Define.BattleSlot.Front,
                    Define.UnitRole.SkeletonMelee, 100, 1, 32)
            };
            using var runner = new BattleRunner(Guid.NewGuid(), 1, inputs);
            BattleEvent damage = default;
            // Runner が通知した被害イベントを値として取り出す。
            runner.EventOccurred += item =>
            {
                if (item.Kind == Define.BattleEventKind.DamageApplied && item.ActorId == actorId) damage = item;
            };
            for (var tick = 0; tick < Define.FirstAttackTick; tick++)
                runner.Advance(1d / Define.TicksPerSecond);
            return damage;
        }

        /// <summary>
        /// 攻撃を並行して進め、各発射・命中時刻と一度だけの到着を確認します。
        /// [KO] 공격을 병렬 진행하여 각 발사·명중 시각과 한 번만 도착하는지 확인합니다.
        /// [EN] Checks concurrent release and impact times and exactly-once arrival for each attack.
        /// </summary>
        [Test]
        public void AttacksAdvanceIndependentlyAndImpactOnce()
        {
            var timeline = new BattlePresentationTimeline();
            timeline.BeginAttack(0, 8, 0.5f, 0.125f, 0.25f);
            timeline.BeginAttack(1, 8, 1f, 0.25f, 0.75f);
            Assert.That(timeline.TryGetProjectileProgress(0, out _), Is.False);
            timeline.Advance(0.125);
            Assert.That(timeline.TryGetProjectileProgress(0, out var progress), Is.True);
            Assert.That(progress, Is.Zero);
            Assert.That(timeline.TryImpact(0), Is.False);
            timeline.Advance(0.125);
            Assert.That(timeline.TryImpact(0), Is.True);
            Assert.That(timeline.TryImpact(0), Is.False);
            Assert.That(timeline.TryGetProjectileProgress(0, out _), Is.False);
            Assert.That(timeline.TryImpact(1), Is.False);
            Assert.That(timeline.RemoveIfFinished(0), Is.False);
            timeline.Advance(0.5);
            Assert.That(timeline.TryImpact(1), Is.True);
            Assert.That(timeline.TryImpact(1), Is.False);
            Assert.That(timeline.RemoveIfFinished(0), Is.True);
            Assert.That(timeline.Attacks[0].Actor, Is.EqualTo(1));
        }

        /// <summary>
        /// 同じ攻撃者の連続予約だけを順番に開始し、他の攻撃者は待たせません。
        /// [KO] 같은 공격자의 연속 예약만 순서대로 시작하고 다른 공격자는 대기시키지 않습니다.
        /// [EN] Starts consecutive cues from one actor in order without blocking another actor.
        /// </summary>
        [Test]
        public void SameActorWaitsForPreviousMotionAndProjectile()
        {
            var timeline = new BattlePresentationTimeline();
            timeline.BeginAttack(0, 10, 0.5f, 0.1f, 0.75f, 1, true);
            timeline.BeginAttack(0, 14, 0.5f, 0.1f, 0.25f, 3);
            timeline.BeginAttack(1, 10, 0.5f, 0.1f, 0.25f);
            Assert.That(timeline.TryStartMotion(0), Is.True);
            Assert.That(timeline.TryStartMotion(0), Is.False);
            Assert.That(timeline.TryStartMotion(1), Is.False);
            Assert.That(timeline.TryStartMotion(2), Is.True);
            Assert.That(timeline.Attacks[0].Target, Is.EqualTo(1));
            Assert.That(timeline.Attacks[0].IsSpecial, Is.True);
            Assert.That(timeline.Attacks[1].IsSpecial, Is.False);
            timeline.Advance(0.5);
            Assert.That(timeline.TryStartMotion(1), Is.False);
            Assert.That(timeline.TryGetProjectileProgress(1, out _), Is.False);
            Assert.That(timeline.TryImpact(1), Is.False);
            timeline.Advance(0.25);
            Assert.That(timeline.TryImpact(0), Is.True);
            Assert.That(timeline.RemoveIfFinished(0), Is.True);
            Assert.That(timeline.TryStartMotion(0), Is.True);
            Assert.That(timeline.TryImpact(0), Is.False);
            timeline.Advance(0.25);
            Assert.That(timeline.TryImpact(0), Is.True);
        }

        /// <summary>
        /// 死亡は自分の最終命中だけを待ち、他人の未到着攻撃には待たされません。
        /// [KO] 사망은 자신의 최종 명중만 기다리고 다른 유닛의 미도착 공격은 기다리지 않습니다.
        /// [EN] Death waits only for the actor's own final impact, not pending attacks from other actors.
        /// </summary>
        [Test]
        public void DeathWaitsForOwnImpactOnly()
        {
            var timeline = new BattlePresentationTimeline();
            timeline.BeginAttack(0, 8, 0.5f, 0.125f, 0.25f);
            timeline.BeginAttack(1, 8, 2f, 0.5f, 1.25f);
            Assert.That(timeline.CanPresentDeath(0, 0, false), Is.False);
            timeline.Advance(0.25);
            Assert.That(timeline.TryImpact(0), Is.True);
            Assert.That(timeline.CanPresentDeath(0, 0, false), Is.True);
            Assert.That(timeline.CanPresentDeath(1, 0, false), Is.False);
            Assert.That(timeline.CanPresentDeath(0, 1, false), Is.False);
            Assert.That(timeline.CanPresentDeath(0, 0, true), Is.False);
        }

        /// <summary>
        /// 結果は全攻撃と死亡後の待機を両方終えてから公開でき、死亡なしなら余分に待ちません。
        /// [KO] 결과는 모든 공격과 사망 후 대기를 모두 끝내야 공개 가능하며 사망이 없으면 추가로 기다리지 않습니다.
        /// [EN] Result readiness requires finished attacks and the death delay, without an extra delay when no death occurred.
        /// </summary>
        [Test]
        public void ResultWaitsForAttackCompletionAndDeathDelay()
        {
            var timeline = new BattlePresentationTimeline();
            Assert.That(timeline.CanComplete, Is.False);
            timeline.CommitResult(Define.BattleResult.Victory);
            Assert.That(timeline.CanComplete, Is.True);
            timeline.BeginAttack(0, 8, 0.5f, 0.125f, 0.25f);
            Assert.That(timeline.CanComplete, Is.False);
            timeline.Advance(0.25);
            Assert.That(timeline.TryImpact(0), Is.True);
            Assert.That(timeline.CanComplete, Is.False);
            timeline.RecordDeath();
            Assert.That(timeline.CanComplete, Is.False);
            timeline.Advance(0.25);
            Assert.That(timeline.RemoveIfFinished(0), Is.True);
            timeline.Advance(0.56);
            Assert.That(timeline.CanComplete, Is.False);
            timeline.Advance(0.02);
            Assert.That(timeline.CanComplete, Is.True);
        }

        /// <summary>
        /// 後から予約した攻撃が先に届いても、実損失を引き、HP と予告を巻き戻しません。
        /// [KO] 나중에 예약한 공격이 먼저 도착해도 실제 손실을 차감하여 HP와 예고를 되돌리지 않습니다.
        /// [EN] Subtracts actual losses without restoring health or preview when a later cue arrives first.
        /// </summary>
        [Test]
        public void ReversedArrivalsPreserveHealthAndPreview()
        {
            var slow = DamageOf(25);
            var fast = DamageOf(35, 3);
            var timeline = new BattlePresentationTimeline();
            timeline.BeginAttack(0, slow.Tick, 1f, 0.125f, 0.75f);
            timeline.BeginAttack(1, fast.Tick, 1f, 0.125f, 0.25f);
            timeline.ReserveDamage(0, slow);
            timeline.ReserveDamage(1, fast);
            using var state = new UnitPresentationReadModel(new BattleUnitInput(2, Define.BattleTeam.Enemy,
                Define.BattleSlot.Front, Define.UnitRole.SkeletonMelee, 100, 1, 32));
            state.ReserveDamage(slow.HealthDamage);
            state.ReserveDamage(fast.HealthDamage);
            Assert.That(state.Health.CurrentValue, Is.EqualTo(100));
            Assert.That(state.PreviewHealth.CurrentValue, Is.EqualTo(40));
            timeline.Advance(0.25);
            Assert.That(timeline.TryImpact(0), Is.False);
            Assert.That(timeline.TryImpact(1), Is.True);
            var hit = timeline.Attacks[1].Damage[0];
            state.ApplyDamage(hit.HealthDamage, hit.ShieldDamage);
            Assert.That(state.Health.CurrentValue, Is.EqualTo(65));
            Assert.That(state.PreviewHealth.CurrentValue, Is.EqualTo(40));
            timeline.Advance(0.5);
            Assert.That(timeline.TryImpact(0), Is.True);
            hit = timeline.Attacks[0].Damage[0];
            state.ApplyDamage(hit.HealthDamage, hit.ShieldDamage);
            Assert.That(state.Health.CurrentValue, Is.EqualTo(40));
            Assert.That(state.PreviewHealth.CurrentValue, Is.EqualTo(40));
        }

        /// <summary>
        /// 予約後に入力配列を上書きしてもコピーは変わらず、解除後には対応しない被害を受け付けません。
        /// [KO] 예약 후 입력 배열을 덮어써도 복사본은 바뀌지 않고 해제 후 대응하지 않는 피해는 거부합니다.
        /// [EN] Keeps copied damage intact after the source array is overwritten and rejects unmatched damage after clearing.
        /// </summary>
        [Test]
        public void CuesCopyEventsAndClearPendingAttacks()
        {
            var events = new[] { DamageOf(25) };
            var timeline = new BattlePresentationTimeline();
            timeline.BeginAttack(0, events[0].Tick, 1f, 0.125f, 0.75f);
            timeline.ReserveDamage(0, events[0]);
            events[0] = DamageOf(70);
            Assert.That(timeline.Attacks[0].Damage[0].HealthDamage, Is.EqualTo(25));
            timeline.Clear();
            Assert.That(timeline.Attacks.Count, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => timeline.ReserveDamage(0, events[0]));
        }

        /// <summary>
        /// 無効な攻撃設定は、原因となった引数名で報告し予約を作らないことを確認します。
        /// [KO] 잘못된 공격 설정은 원인 인자명으로 보고하고 예약을 만들지 않는지 확인합니다.
        /// [EN] Checks that invalid attack settings report the offending parameter without creating a cue.
        /// </summary>
        [TestCase(-1, 8, 0.5f, 0.125f, 0.25f, "actor")]
        [TestCase(0, -1, 0.5f, 0.125f, 0.25f, "tick")]
        [TestCase(0, 8, 0f, 0.125f, 0.25f, "duration")]
        [TestCase(0, 8, 0.5f, 0.5f, 0.75f, "releaseDelay")]
        [TestCase(0, 8, 0.5f, 0.125f, 0.1f, "impactDelay")]
        public void InvalidAttackSettingsIdentifyTheOffendingParameter(int actor, int tick, float duration,
            float releaseDelay, float impactDelay, string expectedParameter)
        {
            var timeline = new BattlePresentationTimeline();
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                timeline.BeginAttack(actor, tick, duration, releaseDelay, impactDelay));
            Assert.That(error.ParamName, Is.EqualTo(expectedParameter));
            Assert.That(timeline.Attacks, Is.Empty);
        }

        /// <summary>
        /// 無効な経過秒を拒否し、同じ表示秒の分割で命中判断が変わらないことを確認します。
        /// [KO] 잘못된 경과 시간을 거부하고 같은 표시 시간을 나누어 전달해도 명중 판단이 같은지 확인합니다.
        /// [EN] Rejects invalid elapsed time and checks equivalent impact decisions when presentation time is partitioned.
        /// </summary>
        [Test]
        public void ClockRejectsInvalidTimeAndPreservesPartitionedProgress()
        {
            var whole = new BattlePresentationTimeline();
            var partitioned = new BattlePresentationTimeline();
            whole.BeginAttack(0, 8, 0.5f, 0.125f, 0.25f);
            partitioned.BeginAttack(0, 8, 0.5f, 0.125f, 0.25f);
            Assert.Throws<ArgumentOutOfRangeException>(() => whole.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => whole.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => whole.Advance(double.PositiveInfinity));
            whole.Advance(0.25);
            partitioned.Advance(0.125);
            partitioned.Advance(0.125);
            Assert.That(partitioned.Time, Is.EqualTo(whole.Time));
            Assert.That(partitioned.TryImpact(0), Is.EqualTo(whole.TryImpact(0)));
        }
    }
}
