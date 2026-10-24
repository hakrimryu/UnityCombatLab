using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 描画なしで戦闘契約と境界を検証します。
    /// [KO] 렌더링 없이 전투 계약과 경계 검증.
    /// [EN] Verifies combat contracts and boundaries without rendering.
    /// </summary>
    public sealed class BattleSimulationTests
    {
        /// <summary>
        /// 再現用の固定 ID。
        /// [KO] 재현용 고정 ID.
        /// [EN] Fixed reproducibility ID.
        /// </summary>
        internal static readonly Guid RunId = new Guid("c6607675-6cda-47a3-8498-295d87d59520");

        /// <summary>
        /// 小さな入力を組み立てます。
        /// [KO] 작은 입력 구성.
        /// [EN] Builds a small input.
        /// </summary>
        internal static BattleUnitInput Unit(int id, Define.UnitRole role,
            Define.BattleSlot slot = Define.BattleSlot.Front,
            int hp = 10000, int attack = 10, int interval = 4, int shield = 0, int? health = null)
        {
            return new BattleUnitInput(id,
                role is Define.UnitRole.Knight or Define.UnitRole.Ranger or Define.UnitRole.Mage
                    ? Define.BattleTeam.Ally
                    : Define.BattleTeam.Enemy,
                slot, role, hp, attack, interval, shield, health);
        }

        /// <summary>
        /// イベントを同期消費して指定 tick まで進めます。
        /// [KO] 이벤트 동기 소비 후 지정 틱까지 진행.
        /// [EN] Consumes events synchronously while advancing to a tick.
        /// </summary>
        internal static void Advance(BattleSimulation battle, int tick)
        {
            while (battle.CurrentTick < tick && battle.Result == Define.BattleResult.Ongoing)
            {
                battle.AcknowledgeEvents();
                battle.Step();
            }
        }

        /// <summary>
        /// 最初の攻撃と周期を確認します。
        /// [KO] 첫 공격과 주기 검증.
        /// [EN] Checks first attacks and intervals.
        /// </summary>
        [TestCase(Define.UnitRole.Knight, 24)]
        [TestCase(Define.UnitRole.Ranger, 18)]
        [TestCase(Define.UnitRole.Mage, 32)]
        [TestCase(Define.UnitRole.SkeletonMelee, 32)]
        [TestCase(Define.UnitRole.SkeletonRanged, 36)]
        [TestCase(Define.UnitRole.SkeletonCaptain, 40)]
        public void FirstAttackAndIntervals(Define.UnitRole role, int interval)
        {
            var subject = Unit(1, role, interval: interval);
            var opponent = Unit(2,
                subject.Team == Define.BattleTeam.Ally ? Define.UnitRole.SkeletonMelee : Define.UnitRole.Knight);
            var battle = new BattleSimulation(RunId, 1, new[] { subject, opponent });
            Advance(battle, 9);
            Assert.That(battle.GetUnit(subject.Team, subject.Slot).AttackCount, Is.Zero);
            Assert.That(battle.EventCount, Is.Zero);
            Advance(battle, 10);
            Assert.That(battle.GetUnit(subject.Team, subject.Slot).AttackCount, Is.EqualTo(1));
            Assert.That(battle.GetUnit(subject.Team, subject.Slot).NextAttackTick, Is.EqualTo(10 + interval));
            Advance(battle, 10 + interval);
            Assert.That(battle.GetUnit(subject.Team, subject.Slot).AttackCount, Is.EqualTo(2));
            Assert.That(Define.TicksPerSecond, Is.EqualTo(20));
        }

        /// <summary>
        /// 入力列順に依存せず交互順になります。
        /// [KO] 입력 순서와 무관한 교차 순서.
        /// [EN] Interleaving does not depend on input enumeration order.
        /// </summary>
        [Test]
        public void InterleavedOrderIsCanonical()
        {
            var battle = new BattleSimulation(RunId, 2, new[]
            {
                Unit(6, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackLower),
                Unit(4, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackUpper),
                Unit(2, Define.UnitRole.SkeletonMelee),
                Unit(5, Define.UnitRole.Mage, Define.BattleSlot.BackLower),
                Unit(3, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper), Unit(1, Define.UnitRole.Knight)
            });
            Advance(battle, 10);
            var actors = new List<int>();
            for (var i = 0; i < battle.EventCount; i++)
            {
                var item = battle.GetEvent(i);
                Assert.That(item.RunId, Is.EqualTo(RunId));
                Assert.That(item.Wave, Is.EqualTo(2));
                Assert.That(item.Tick, Is.EqualTo(10));
                Assert.That(item.Sequence, Is.EqualTo(i + 1));
                if (item.Kind == Define.BattleEventKind.AttackExecuted)
                {
                    actors.Add(item.ActorId);
                }
            }

            Assert.That(actors, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
        }

        /// <summary>
        /// 前列/後列の優先と死亡対象のスキップを検証します。
        /// [KO] 전열/후열 우선과 사망 대상 제외 검증.
        /// [EN] Verifies front/back priority and skipping dead targets.
        /// </summary>
        [TestCase(false, 100, 100, 1)]
        [TestCase(false, 0, 100, 2)]
        [TestCase(false, 0, 0, 3)]
        [TestCase(true, 100, 100, 2)]
        [TestCase(true, 100, 0, 3)]
        public void TargetPriority(bool ranged, int frontHealth, int backHealth, int expected)
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Knight, hp: 100, attack: 0, health: frontHealth),
                Unit(2, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper, 100, 0, health: backHealth),
                Unit(3, Define.UnitRole.Mage, Define.BattleSlot.BackLower, 100, 0),
                Unit(4, ranged ? Define.UnitRole.SkeletonRanged : Define.UnitRole.SkeletonMelee)
            });
            Advance(battle, 10);
            for (var i = 0; i < battle.EventCount; i++)
            {
                if (battle.GetEvent(i).ActorId == 4 && battle.GetEvent(i).Kind == Define.BattleEventKind.AttackExecuted)
                {
                    Assert.That(battle.GetEvent(i).TargetId, Is.EqualTo(expected));
                    return;
                }
            }

            Assert.Fail("Expected enemy attack.");
        }

        /// <summary>
        /// 同 tick の死亡は後続行動を取り消し、次の攻撃は再選択します。
        /// [KO] 같은 틱 사망 시 후속 행동 취소 및 재타깃.
        /// [EN] Same-tick death cancels a later action and forces retargeting.
        /// </summary>
        [Test]
        public void DeadActorCannotAttackAndNextActorRetargets()
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Knight, attack: 100), Unit(2, Define.UnitRole.SkeletonMelee, hp: 1),
                Unit(3, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper, attack: 100),
                Unit(4, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackUpper, hp: 1)
            });
            Advance(battle, 10);
            Assert.That(battle.Result, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(battle.GetUnit(Define.BattleTeam.Enemy, Define.BattleSlot.Front).AttackCount, Is.Zero);
            var deaths = 0;
            var ends = 0;
            for (var i = 0; i < battle.EventCount; i++)
            {
                var item = battle.GetEvent(i);
                if (item.Kind == Define.BattleEventKind.UnitDied)
                {
                    deaths++;
                }

                if (item.Kind == Define.BattleEventKind.BattleEnded)
                {
                    ends++;
                }

                if (item.Kind == Define.BattleEventKind.AttackExecuted && item.ActorId == 3)
                {
                    Assert.That(item.TargetId, Is.EqualTo(4));
                }
            }

            Assert.That(deaths, Is.EqualTo(2));
            Assert.That(ends, Is.EqualTo(1));
            var count = battle.EventCount;
            battle.Step();
            Assert.That(battle.EventCount, Is.EqualTo(count));
            battle.AcknowledgeEvents();
            battle.Step();
            Assert.That(battle.EventCount, Is.Zero);
            Assert.That(battle.CurrentTick, Is.EqualTo(10));
        }

        /// <summary>
        /// 敵の撃破でも即座に敗北し後続行動を止めます。
        /// [KO] 적 처치 시 즉시 패배 및 후속 행동 중단.
        /// [EN] Enemy kills resolve defeat immediately and stop later actions.
        /// </summary>
        [Test]
        public void EnemyKillEndsBattleOnce()
        {
            var battle = new BattleSimulation(RunId, 1,
                new[] { Unit(1, Define.UnitRole.Ranger, hp: 1), Unit(2, Define.UnitRole.SkeletonMelee) });
            Advance(battle, 10);
            Assert.That(battle.Result, Is.EqualTo(Define.BattleResult.Defeat));
            Assert.That(battle.GetEvent(battle.EventCount - 1).Kind, Is.EqualTo(Define.BattleEventKind.BattleEnded));
        }

        /// <summary>
        /// 盾吸収、軽減、最小被害と HP clamp。
        /// [KO] 방어막 흡수·경감·최소 피해·HP 제한.
        /// [EN] Shield absorption, mitigation, minimum damage, and health clamp.
        /// </summary>
        [TestCase(10, 5, 100, 3, 5)]
        [TestCase(1, 0, 100, 1, 0)]
        [TestCase(0, 0, 100, 1, 0)]
        [TestCase(10, 100, 100, 0, 8)]
        [TestCase(100, 5, 2, 2, 5)]
        public void DamageIsFlooredMitigatedAndClamped(int attack, int shield, int hp, int healthDamage,
            int shieldDamage)
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Knight, hp: hp, shield: shield),
                Unit(2, Define.UnitRole.SkeletonMelee, attack: attack)
            });
            Advance(battle, 10);
            for (var i = 0; i < battle.EventCount; i++)
            {
                var item = battle.GetEvent(i);
                if (item.Kind != Define.BattleEventKind.DamageApplied || item.ActorId != 2)
                {
                    continue;
                }

                Assert.That(item.HealthDamage, Is.EqualTo(healthDamage));
                Assert.That(item.ShieldDamage, Is.EqualTo(shieldDamage));
                Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).Health,
                    Is.EqualTo(hp - healthDamage));
                return;
            }

            Assert.Fail("Expected damage event.");
        }

        /// <summary>
        /// 4回目の倍化、5回目の通常攻撃復帰、8回目の再発動を確認します。
        /// [KO] 4번째 배율 증가, 5번째 일반 공격 복귀, 8번째 재발동을 확인합니다.
        /// [EN] Verifies doubling on attack four, normal damage on five, and activation again on eight.
        /// </summary>
        [Test]
        public void RangerSpecialAttackReturnsToNormalAndRepeats()
        {
            var battle = new BattleSimulation(RunId, 1,
                new[] { Unit(1, Define.UnitRole.Ranger, attack: 34), Unit(2, Define.UnitRole.SkeletonMelee) });
            var expectedDamage = new[] { 34, 34, 34, 68, 34, 34, 34, 68 };
            for (var attackIndex = 0; attackIndex < expectedDamage.Length; attackIndex++)
            {
                Advance(battle, 10 + attackIndex * 4);

                var damage = Enumerable.Range(0, battle.EventCount)
                    .Select(battle.GetEvent)
                    .Single(item => item.ActorId == 1 && item.Kind == Define.BattleEventKind.DamageApplied);
                Assert.That(damage.HealthDamage, Is.EqualTo(expectedDamage[attackIndex]),
                    $"Attack {attackIndex + 1}");
                Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).AttackCount,
                    Is.EqualTo(attackIndex + 1));
            }
        }

        /// <summary>
        /// 範囲攻撃の次は単体へ戻り、次の周期で再び全員を攻撃します。
        /// [KO] 광역 공격 다음에는 단일 공격으로 돌아오고 다음 주기에 다시 전원을 공격합니다.
        /// [EN] Returns to single-target attacks after area damage and attacks all targets again next cycle.
        /// </summary>
        [TestCase(Define.UnitRole.Mage, 58, 40)]
        [TestCase(Define.UnitRole.SkeletonCaptain, 100, 60)]
        public void AreaAttackReturnsToNormalAndRepeats(Define.UnitRole role, int attack, int areaDamage)
        {
            var actor = Unit(1, role, attack: attack);
            var opponentRole = actor.Team == Define.BattleTeam.Ally
                ? Define.UnitRole.SkeletonMelee
                : Define.UnitRole.Ranger;
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                actor,
                Unit(2, opponentRole),
                Unit(3, opponentRole, Define.BattleSlot.BackLower)
            });

            foreach (var attackNumber in new[] { 1, 2, 3, 4, 5, 6 })
            {
                Advance(battle, 10 + (attackNumber - 1) * 4);

                var events = Enumerable.Range(0, battle.EventCount)
                    .Select(battle.GetEvent)
                    .Where(item => item.ActorId == 1)
                    .ToArray();
                var executed = events.Single(item => item.Kind == Define.BattleEventKind.AttackExecuted);
                var damages = events.Where(item => item.Kind == Define.BattleEventKind.DamageApplied).ToArray();
                var isArea = attackNumber is 3 or 6;
                var expectedTargets = isArea ? new[] { 2, 3 } : new[] { 2 };
                var expectedDamage = isArea ? areaDamage : attack;

                Assert.That(executed.TargetId, Is.EqualTo(isArea ? 0 : 2), $"Attack {attackNumber}");
                Assert.That(damages.Select(item => item.TargetId), Is.EqualTo(expectedTargets),
                    $"Attack {attackNumber}");
                Assert.That(damages.Select(item => item.HealthDamage), Is.All.EqualTo(expectedDamage),
                    $"Attack {attackNumber}");
                Assert.That(battle.GetUnit(actor.Team, actor.Slot).AttackCount, Is.EqualTo(attackNumber));
            }
        }

        /// <summary>
        /// 最大攻撃力でもlong中間値と残量clampを維持します。
        /// [KO] 최대 공격력에서도 long 중간값과 잔량 제한을 유지합니다.
        /// [EN] Preserves long arithmetic and remaining-health clamping at maximum attack power.
        /// </summary>
        [Test]
        public void RangerMaximumAttackUsesLongArithmeticAndClamps()
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Ranger, attack: 1073741823),
                Unit(2, Define.UnitRole.SkeletonMelee, hp: int.MaxValue, shield: int.MaxValue)
            });

            Advance(battle, 22);

            var damage = battle.GetEvent(1);
            Assert.That(damage.HealthDamage + (long)damage.ShieldDamage, Is.EqualTo(1073741825));
        }

        /// <summary>
        /// 3回目の範囲攻撃は通常攻撃を置換し、生存者だけを順番に攻撃。
        /// [KO] 세 번째 광역은 일반 공격 대체 및 생존자만 순서대로 공격.
        /// [EN] Third area attack replaces normal damage and visits living targets in slot order.
        /// </summary>
        [TestCase(Define.UnitRole.Mage, 58, 40)]
        [TestCase(Define.UnitRole.SkeletonCaptain, 100, 60)]
        public void AreaAttackReplacesNormalAttack(Define.UnitRole role, int attack, int expected)
        {
            var actor = Unit(1, role, attack: attack);
            var opponentRole = actor.Team == Define.BattleTeam.Ally
                ? Define.UnitRole.SkeletonMelee
                : Define.UnitRole.Ranger;
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                actor, Unit(2, opponentRole), Unit(3, opponentRole, Define.BattleSlot.BackUpper, health: 0),
                Unit(4, opponentRole, Define.BattleSlot.BackLower)
            });
            Advance(battle, 18);
            var targets = new List<int>();
            for (var i = 0; i < battle.EventCount; i++)
            {
                var item = battle.GetEvent(i);
                if (item.ActorId != 1)
                {
                    continue;
                }

                if (item.Kind == Define.BattleEventKind.AttackExecuted)
                {
                    Assert.That(item.TargetId, Is.Zero);
                }

                if (item.Kind != Define.BattleEventKind.DamageApplied)
                {
                    continue;
                }

                targets.Add(item.TargetId);
                Assert.That(item.HealthDamage, Is.EqualTo(expected));
            }

            Assert.That(targets, Is.EqualTo(new[] { 2, 4 }));
        }

        /// <summary>
        /// 範囲で最後の複数対象を倒しても終了は一度。
        /// [KO] 광역으로 마지막 복수 대상 처치해도 종료는 한 번.
        /// [EN] Multiple final area kills still emit one end.
        /// </summary>
        [Test]
        public void AreaKillCompletesAllDamageBeforeEnd()
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Mage, attack: 100), Unit(2, Define.UnitRole.SkeletonMelee, hp: 201),
                Unit(3, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackLower, hp: 50)
            });
            Advance(battle, 18);
            Assert.That(battle.Result, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(battle.EventCount, Is.EqualTo(6));
            Assert.That(battle.GetEvent(2).Kind, Is.EqualTo(Define.BattleEventKind.UnitDied));
            Assert.That(battle.GetEvent(4).Kind, Is.EqualTo(Define.BattleEventKind.UnitDied));
            Assert.That(battle.GetEvent(5).Kind, Is.EqualTo(Define.BattleEventKind.BattleEnded));
        }

        /// <summary>
        /// 全スロットの範囲攻撃でもバッファを越えず全イベントを保持します。
        /// [KO] 전체 슬롯의 광역 공격에서도 버퍼를 넘지 않고 모든 이벤트를 보존합니다.
        /// [EN] Preserves every event without overflow when all slots execute area attacks.
        /// </summary>
        [Test]
        public void FullFormationAreaAttacksPreserveAllEvents()
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Mage),
                Unit(2, Define.UnitRole.SkeletonCaptain),
                Unit(3, Define.UnitRole.Mage, Define.BattleSlot.BackUpper),
                Unit(4, Define.UnitRole.SkeletonCaptain, Define.BattleSlot.BackUpper),
                Unit(5, Define.UnitRole.Mage, Define.BattleSlot.BackLower),
                Unit(6, Define.UnitRole.SkeletonCaptain, Define.BattleSlot.BackLower)
            });

            Advance(battle, 18);

            var attacks = 0;
            var damages = 0;
            var previousSequence = battle.GetEvent(0).Sequence - 1;
            for (var i = 0; i < battle.EventCount; i++)
            {
                var item = battle.GetEvent(i);
                Assert.That(item.Sequence, Is.EqualTo(++previousSequence));
                if (item.Kind == Define.BattleEventKind.AttackExecuted)
                {
                    attacks++;
                    Assert.That(item.TargetId, Is.Zero);
                }
                else if (item.Kind == Define.BattleEventKind.DamageApplied)
                {
                    damages++;
                }
            }

            Assert.That(attacks, Is.EqualTo(6));
            Assert.That(damages, Is.EqualTo(18));
            Assert.That(battle.EventCount, Is.EqualTo(24));
            Assert.That(battle.Result, Is.EqualTo(Define.BattleResult.Ongoing));
        }

        /// <summary>
        /// 範囲係数と騎士軽減を別々に切り捨ててから盾を適用します。
        /// [KO] 광역 계수와 기사 경감을 각각 내림한 뒤 방어막을 적용합니다.
        /// [EN] Floors area scaling and knight mitigation separately before applying the shield.
        /// </summary>
        [Test]
        public void AreaDamageFloorsBeforeKnightReductionAndShield()
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Knight, hp: 100, shield: 22),
                Unit(2, Define.UnitRole.SkeletonCaptain, attack: 13)
            });

            Advance(battle, 18);

            var damage = battle.GetEvent(3);
            Assert.That(damage.Kind, Is.EqualTo(Define.BattleEventKind.DamageApplied));
            Assert.That(damage.ActorId, Is.EqualTo(2));
            Assert.That(damage.ShieldDamage, Is.EqualTo(2));
            Assert.That(damage.HealthDamage, Is.EqualTo(3));
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).Health, Is.EqualTo(97));
        }

        /// <summary>
        /// 空陣営や全員死亡は tick0 で一度だけ終了。
        /// [KO] 빈 팀·전원 사망 시 tick0 한 번 종료.
        /// [EN] Empty/dead teams resolve once at tick zero.
        /// </summary>
        [TestCase(0, 0, Define.BattleResult.Defeat)]
        [TestCase(1, 0, Define.BattleResult.Victory)]
        [TestCase(0, 1, Define.BattleResult.Defeat)]
        public void EmptyTeamsDoNotAttack(int allies, int enemies, Define.BattleResult expected)
        {
            var inputs = new List<BattleUnitInput>();
            if (allies != 0)
            {
                inputs.Add(Unit(1, Define.UnitRole.Knight));
            }

            if (enemies != 0)
            {
                inputs.Add(Unit(2, Define.UnitRole.SkeletonMelee));
            }

            inputs.Add(Unit(3, Define.UnitRole.Mage, Define.BattleSlot.BackLower, health: 0));
            var battle = new BattleSimulation(RunId, 1, inputs);
            battle.Step();
            Assert.That(battle.CurrentTick, Is.Zero);
            Assert.That(battle.Result, Is.EqualTo(expected));
            Assert.That(battle.EventCount, Is.EqualTo(1));
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).AttackCount, Is.Zero);
        }

        /// <summary>
        /// 1800境界では攻撃の終了判定を時間超過より優先。
        /// [KO] 1800 경계에서 공격 종료 판정이 시간 초과 우선.
        /// [EN] At tick 1800, attack outcomes take precedence over timeout.
        /// </summary>
        [TestCase(1, Define.BattleResult.Victory)]
        [TestCase(3, Define.BattleResult.Timeout)]
        public void TimeoutBoundaryAllowsFinalKill(int enemyHp, Define.BattleResult expected)
        {
            var battle = new BattleSimulation(RunId, 1, new[]
            {
                Unit(1, Define.UnitRole.Knight, attack: 1, interval: 1790),
                Unit(2, Define.UnitRole.SkeletonMelee, hp: enemyHp + 1, attack: 0)
            });
            Advance(battle, 1799);
            Assert.That(battle.Result, Is.EqualTo(Define.BattleResult.Ongoing));
            Advance(battle, 1800);
            Assert.That(battle.Result, Is.EqualTo(expected));
            Assert.That(battle.CurrentTick, Is.EqualTo(1800));
            battle.AcknowledgeEvents();
            battle.Step();
            Assert.That(battle.CurrentTick, Is.EqualTo(1800));
            Assert.That(battle.EventCount, Is.Zero);
        }

        /// <summary>
        /// 未消費バッファの上書き拒否とコピー寿命。
        /// [KO] 미소비 버퍼 덮어쓰기 거부와 복사본 수명.
        /// [EN] Prevents overwriting unread events and preserves copied values.
        /// </summary>
        [Test]
        public void EventOwnershipRequiresAcknowledgement()
        {
            var battle = new BattleSimulation(RunId, 1,
                new[] { Unit(1, Define.UnitRole.Knight), Unit(2, Define.UnitRole.SkeletonMelee) });
            Advance(battle, 10);
            var copy = battle.GetEvent(0);
            Assert.Throws<InvalidOperationException>(() => battle.Step());
            Assert.That(battle.CurrentTick, Is.EqualTo(10));
            battle.AcknowledgeEvents();
            Assert.Throws<ArgumentOutOfRangeException>(() => battle.GetEvent(0));
            Advance(battle, 14);
            Assert.That(copy.Tick, Is.EqualTo(10));
            Assert.That(battle.GetEvent(0).Sequence, Is.GreaterThan(copy.Sequence));
        }

        /// <summary>
        /// 入力配列や以前の状態コピーの変更はコアに影響しません。
        /// [KO] 입력 배열·이전 상태 복사본은 코어에 영향 없음.
        /// [EN] Input array replacement and old state copies cannot mutate the core.
        /// </summary>
        [Test]
        public void InputStorageAndStateSnapshotsAreIsolated()
        {
            var input = new[] { Unit(1, Define.UnitRole.Knight), Unit(2, Define.UnitRole.SkeletonRanged) };
            var battle = new BattleSimulation(RunId, 1, input);
            var snapshot = battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front);
            input[0] = Unit(3, Define.UnitRole.Ranger);
            Advance(battle, 10);
            Assert.That(snapshot.Health, Is.EqualTo(10000));
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).Input.Id, Is.EqualTo(1));
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).Health, Is.EqualTo(9992));
            Assert.That(battle.GetEvent(2).TargetId, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                battle.GetUnit((Define.BattleTeam)2, Define.BattleSlot.Front));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                battle.GetUnit(Define.BattleTeam.Ally, (Define.BattleSlot)(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => battle.GetEvent(-1));
        }

        /// <summary>
        /// 入力の重複と無効範囲を拒否。
        /// [KO] 입력 중복·잘못된 범위 거부.
        /// [EN] Rejects duplicate inputs and invalid ranges.
        /// </summary>
        [Test]
        public void RejectsInvalidInputs()
        {
            var ally = Unit(1, Define.UnitRole.Knight);
            Assert.Throws<ArgumentException>(() => _ = new BattleSimulation(RunId, 1, new[] { ally, ally }));
            Assert.Throws<ArgumentException>(() => _ = new BattleSimulation(RunId, 1,
                new[] { ally, Unit(1, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper) }));
            Assert.Throws<ArgumentException>(() =>
                _ = new BattleSimulation(Guid.Empty, 1, Array.Empty<BattleUnitInput>()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new BattleSimulation(RunId, 0, Array.Empty<BattleUnitInput>()));
            Assert.Throws<ArgumentNullException>(() => _ = new BattleSimulation(RunId, 1, null));
            Assert.Throws<ArgumentException>(() => _ = new BattleSimulation(RunId, 1, new BattleUnitInput[] { null }));
            Assert.Throws<ArgumentException>(() => _ = new BattleSimulation(RunId, 1, new BattleUnitInput[7]));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new BattleSimulation(RunId, 6, Array.Empty<BattleUnitInput>()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new BattleUnitInput(1, (Define.BattleTeam)2, Define.BattleSlot.Front, Define.UnitRole.Knight, 1, 1,
                    4));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(0, Define.UnitRole.Knight));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, hp: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, attack: int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, attack: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, interval: 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, interval: int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, shield: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, health: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, health: 10001));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, (Define.UnitRole)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit(1, Define.UnitRole.Knight, (Define.BattleSlot)99));
            Assert.Throws<ArgumentException>(() => _ = new BattleUnitInput(1, Define.BattleTeam.Enemy,
                Define.BattleSlot.Front, Define.UnitRole.Knight, 1, 1, 4));
        }

        /// <summary>
        /// 順序や疑似フレームの分割が全状態・イベントを変えない。
        /// [KO] 순서·가상 프레임 분할과 무관하게 상태·이벤트 동일.
        /// [EN] Input order and simulated frame batching cannot change state or events.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(8)]
        public void RepeatedInputsProduceIdenticalEventsAndState(int ticksPerFrame)
        {
            var inputs = BattleBenchmarkTests.CreateInputs();
            var baseline = Record(inputs, 1, out var result, out var tick);
            Array.Reverse(inputs);
            for (var repeat = 0; repeat < 10; repeat++)
            {
                var actual = Record(inputs, ticksPerFrame, out var actualResult, out var actualTick);
                Assert.That(actual, Is.EqualTo(baseline));
                Assert.That(actualResult, Is.EqualTo(result));
                Assert.That(actualTick, Is.EqualTo(tick));
            }
        }

        /// <summary>
        /// 値型の全フィールドと毎 tick の状態を比較用に保存。
        /// [KO] 값 타입의 전체 필드와 매 틱 상태를 비교용 저장.
        /// [EN] Captures all event fields and per-tick state for comparison.
        /// </summary>
        private static List<object> Record(BattleUnitInput[] inputs, int ticksPerFrame, out Define.BattleResult result,
            out int tick)
        {
            var battle = new BattleSimulation(RunId, 1, inputs);
            var record = new List<object>();
            while (battle.Result == Define.BattleResult.Ongoing)
            {
                for (var i = 0; i < ticksPerFrame && battle.Result == Define.BattleResult.Ongoing; i++)
                {
                    battle.Step();
                    for (var j = 0; j < battle.EventCount; j++)
                    {
                        record.Add(battle.GetEvent(j));
                    }

                    for (var slot = 0; slot < Define.SlotsPerTeam; slot++)
                    {
                        for (var team = 0; team < Define.BattleTeamCount; team++)
                        {
                            record.Add(battle.GetUnit((Define.BattleTeam)team, (Define.BattleSlot)slot));
                        }
                    }

                    battle.AcknowledgeEvents();
                }
            }

            result = battle.Result;
            tick = battle.CurrentTick;
            return record;
        }
    }
}
