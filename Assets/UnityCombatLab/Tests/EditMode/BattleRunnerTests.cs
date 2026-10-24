using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 実時間・イベント消費・通知の境界を検証します。
    /// [KO] 실시간·이벤트 소비·알림 경계를 검증합니다.
    /// [EN] Verifies real-time, event-consumption and notification boundaries.
    /// </summary>
    public sealed class BattleRunnerTests
    {
        /// <summary>
        /// 最初の攻撃で勝つ入力を独立値で作ります。
        /// [KO] 첫 공격으로 승리하는 입력을 독립값으로 만듭니다.
        /// [EN] Creates independent inputs that win on the first attack.
        /// </summary>
        private static BattleUnitInput[] LethalInputs()
        {
            return new[]
            {
                new BattleUnitInput(1, Define.BattleTeam.Ally, Define.BattleSlot.Front,
                    Define.UnitRole.Knight, 100, 40, 24),
                new BattleUnitInput(2, Define.BattleTeam.Enemy, Define.BattleSlot.Front,
                    Define.UnitRole.SkeletonMelee, 20, 1, 32, 5)
            };
        }

        /// <summary>
        /// 一フレーム上限を超えた時間を次回に残します。
        /// [KO] 프레임 상한을 넘긴 시간을 다음 호출에 남깁니다.
        /// [EN] Retains time exceeding the per-frame cap.
        /// </summary>
        [Test]
        public void CatchUpCapRetainsRemainder()
        {
            using var runner = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            runner.Advance(0.45);
            Assert.That(runner.CurrentTick, Is.EqualTo(8));
            runner.Advance(0);
            Assert.That(runner.CurrentTick, Is.EqualTo(9));
            runner.Advance(0.025);
            Assert.That(runner.CurrentTick, Is.EqualTo(9));
            runner.Advance(0.025);
            Assert.That(runner.CurrentTick, Is.EqualTo(10));
            Assert.That(runner.Result, Is.EqualTo(Define.BattleResult.Victory));
        }

        /// <summary>
        /// 最後のダメージ・死亡・終了を一度だけ届けます。
        /// [KO] 마지막 피해·사망·종료를 한 번만 전달합니다.
        /// [EN] Delivers final damage, death and outcome exactly once.
        /// </summary>
        [Test]
        public void FinalEventsReachReadModelBeforeStopping()
        {
            using var runner = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            var events = new List<BattleEvent>();
            runner.EventOccurred += events.Add;
            runner.Advance(0.5);
            runner.Advance(0);
            Assert.That(events.Select(item => item.Kind), Is.EqualTo(new[]
            {
                Define.BattleEventKind.AttackExecuted, Define.BattleEventKind.DamageApplied,
                Define.BattleEventKind.UnitDied, Define.BattleEventKind.BattleEnded
            }));
            Assert.That(events[1].HealthDamage, Is.EqualTo(20));
            Assert.That(events[1].ShieldDamage, Is.EqualTo(5));
            Assert.That(runner.ReadModel.Units[1].Health.CurrentValue, Is.Zero);
            Assert.That(runner.ReadModel.Units[1].Shield.CurrentValue, Is.Zero);
            Assert.That(runner.ReadModel.Result.CurrentValue, Is.EqualTo(Define.BattleResult.Victory));
            runner.Advance(10);
            Assert.That(events.Count, Is.EqualTo(4));
            Assert.That(runner.CurrentTick, Is.EqualTo(10));
        }

        /// <summary>
        /// HP が変わらない tick と同値代入は通知を増やしません。
        /// [KO] HP가 안 바뀐 tick과 같은 값 대입은 알림을 늘리지 않습니다.
        /// [EN] Unchanged ticks and equal values do not add notifications.
        /// </summary>
        [Test]
        public void ReadModelNotifiesOnlyChangedValues()
        {
            using var runner = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            var allyUpdates = 0;
            var enemyUpdates = 0;
            using var ally = runner.ReadModel.Units[0].Health.Subscribe(_ => allyUpdates++);
            using var enemy = runner.ReadModel.Units[1].Health.Subscribe(_ => enemyUpdates++);
            runner.Advance(0.45);
            runner.Advance(0);
            Assert.That(allyUpdates, Is.EqualTo(1));
            Assert.That(enemyUpdates, Is.EqualTo(1));
            runner.Advance(0.05);
            Assert.That(allyUpdates, Is.EqualTo(1));
            Assert.That(enemyUpdates, Is.EqualTo(2));
        }

        /// <summary>
        /// 値コピーは再利用バッファの次 tick でも変わりません。
        /// [KO] 값 복사본은 재사용 버퍼의 다음 tick에도 바뀌지 않습니다.
        /// [EN] Copied values remain unchanged when the buffer is reused.
        /// </summary>
        [Test]
        public void FramePartitionsProduceIdenticalEventsAndSnapshots()
        {
            var inputs = new[]
            {
                new BattleUnitInput(1, Define.BattleTeam.Ally, Define.BattleSlot.Front,
                    Define.UnitRole.Knight, 600, 40, 24),
                new BattleUnitInput(2, Define.BattleTeam.Enemy, Define.BattleSlot.Front,
                    Define.UnitRole.SkeletonMelee, 420, 12, 32)
            };
            var runId = Guid.NewGuid();
            using var fine = new BattleRunner(runId, 1, inputs);
            using var coarse = new BattleRunner(runId, 1, inputs);
            var fineEvents = new List<BattleEvent>();
            var coarseEvents = new List<BattleEvent>();
            fine.EventOccurred += fineEvents.Add;
            coarse.EventOccurred += coarseEvents.Add;
            for (var frame = 0; frame < 10000 && fine.Result == Define.BattleResult.Ongoing; frame++)
            {
                fine.Advance(0.0125);
            }

            for (var frame = 0; frame < 10000 && coarse.Result == Define.BattleResult.Ongoing; frame++)
            {
                coarse.Advance(0.2);
            }

            Assert.That(fine.Result, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(coarse.CurrentTick, Is.EqualTo(fine.CurrentTick));
            Assert.That(coarseEvents, Is.EqualTo(fineEvents));
            Assert.That(fineEvents[0].Tick, Is.EqualTo(10));
            Assert.That(fineEvents[0].Sequence, Is.EqualTo(1));
        }

        /// <summary>
        /// 破棄後は tick と通知を再開しません。
        /// [KO] 해제 후 tick과 알림을 재개하지 않습니다.
        /// [EN] Does not resume ticks or notifications after disposal.
        /// </summary>
        [Test]
        public void DisposalAndConsumerFailureStopFurtherTicks()
        {
            var disposed = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            disposed.Dispose();
            disposed.Advance(1);
            Assert.That(disposed.CurrentTick, Is.Zero);
            using var faulty = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            faulty.EventOccurred += _ => throw new InvalidOperationException("consumer failure");
            faulty.Advance(0.5);
            Assert.Throws<InvalidOperationException>(() => faulty.Advance(0));
            Assert.That(faulty.HasFaulted, Is.True);
            faulty.Advance(1);
            Assert.That(faulty.CurrentTick, Is.EqualTo(10));
            Assert.That(faulty.ReadModel.Units[1].Health.CurrentValue, Is.EqualTo(20));
        }

        /// <summary>
        /// 同期通知からの再入を拒否します。
        /// [KO] 동기 알림에서의 재진입을 거부합니다.
        /// [EN] Rejects reentrant advancement from a synchronous callback.
        /// </summary>
        [Test]
        public void ReentrantAdvanceCannotOverwriteEvents()
        {
            var runner = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            try
            {
                runner.EventOccurred += _ => runner.Advance(0.05);
                runner.Advance(0.5);
                Assert.Throws<InvalidOperationException>(() => runner.Advance(0));
                Assert.That(runner.HasFaulted, Is.True);
            }
            finally
            {
                runner.Dispose();
            }
        }

        /// <summary>
        /// 通知中の破棄で残りの通知を打ち切ります。
        /// [KO] 알림 중 해제 시 남은 알림을 중단합니다.
        /// [EN] Stops remaining notifications when disposed from a callback.
        /// </summary>
        [Test]
        public void DisposalDuringCallbackPreventsFurtherNotification()
        {
            var runner = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            try
            {
                var events = 0;
                runner.EventOccurred += _ =>
                {
                    events++;
                    runner.Dispose();
                };
                runner.Advance(0.5);
                runner.Advance(0);
                Assert.That(events, Is.EqualTo(1));
            }
            finally
            {
                runner.Dispose();
            }
        }

        /// <summary>
        /// 無効な時間入力を状態変更前に拒否します。
        /// [KO] 잘못된 시간 입력을 상태 변경 전에 거부합니다.
        /// [EN] Rejects invalid time inputs before state changes.
        /// </summary>
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.MaxValue)]
        public void InvalidTimeIsRejected(double seconds)
        {
            using var runner = new BattleRunner(Guid.NewGuid(), 1, LethalInputs());
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(seconds));
            Assert.That(runner.CurrentTick, Is.Zero);
            Assert.That(runner.HasFaulted, Is.False);
        }
    }
}
