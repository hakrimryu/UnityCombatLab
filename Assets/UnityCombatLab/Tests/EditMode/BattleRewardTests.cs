using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 開始六秒の強化と撃破回復を実際の戦闘イベントで確認します。
    /// [KO] 시작 6초 강화와 처치 회복을 실제 전투 이벤트로 확인합니다.
    /// [EN] Checks the opening six-second bonus and kill healing through actual battle events.
    /// </summary>
    public sealed class BattleRewardTests
    {
        /// <summary>
        /// 119 tick の攻撃には先制を適用し、120 tick では適用しないことを確認します。
        /// [KO] 119tick 공격에는 선제를 적용하고 120tick에는 적용하지 않는지 확인합니다.
        /// [EN] Applies ambush at tick 119 and excludes it at tick 120.
        /// </summary>
        [TestCase(119, 120)]
        [TestCase(120, 100)]
        public void AmbushEndsExactlyAtTick120(int tick, int expectedDamage)
        {
            var rewards = new RunRewards(new[] { new RewardChoice(1, Define.RewardKind.Ambush, 2000) });
            var battle = new BattleSimulation
            (BattleSimulationTests.RunId,
                2,
                new[]
                {
                    BattleSimulationTests.Unit(1, Define.UnitRole.Knight, attack: 100,
                        interval: tick - Define.FirstAttackTick),
                    BattleSimulationTests.Unit(2, Define.UnitRole.SkeletonMelee, attack: 100, interval: 4)
                },
                rewards);
            BattleSimulationTests.Advance(battle, Define.FirstAttackTick);
            var opening = Enumerable.Range(0, battle.EventCount).Select(battle.GetEvent).ToArray();
            Assert.That(opening.Single(item => item.Kind == Define.BattleEventKind.DamageApplied && item.ActorId == 2).HealthDamage,
                Is.EqualTo(80));
            BattleSimulationTests.Advance(battle, tick);
            var events = Enumerable.Range(0, battle.EventCount).Select(battle.GetEvent).ToArray();
            Assert.That(events.Single(item => item.Kind == Define.BattleEventKind.DamageApplied && item.ActorId == 1).HealthDamage,
                Is.EqualTo(expectedDamage));
        }

        /// <summary>
        /// 広域攻撃の三撃破を三回回復し、最大 HP と死亡者を守ることを確認します。
        /// [KO] 광역 공격의 세 처치를 세 번 회복하고 최대 HP·사망자를 지키는지 확인합니다.
        /// [EN] Heals three times for three area kills, respecting maximum health and dead allies.
        /// </summary>
        [Test]
        public void AreaKillsHealEachLivingAllyWithoutRevivingOrOverflowing()
        {
            var rewards = new RunRewards(new[] { new RewardChoice(1, Define.RewardKind.Renewal, 800) });
            var battle = new BattleSimulation
            (BattleSimulationTests.RunId,
                2,
                new[]
                {
                    BattleSimulationTests.Unit(1, Define.UnitRole.Mage, hp: 1000, health: 50, attack: 100),
                    BattleSimulationTests.Unit(2, Define.UnitRole.SkeletonMelee, hp: 1000, health: 250, attack: 0),
                    BattleSimulationTests.Unit(3, Define.UnitRole.Knight, Define.BattleSlot.BackUpper, hp: 1000, health: 990, attack: 0),
                    BattleSimulationTests.Unit(4, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackUpper, hp: 1000, health: 1, attack: 0),
                    BattleSimulationTests.Unit(5, Define.UnitRole.Ranger, Define.BattleSlot.BackLower, hp: 1000, health: 0, attack: 0),
                    BattleSimulationTests.Unit(6, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackLower, hp: 1000, health: 1, attack: 0)
                },
                rewards);
            BattleSimulationTests.Advance(battle, Define.FirstAttackTick + 2 * Define.MinimumAttackIntervalTicks);
            var healing = Enumerable.Range(0, battle.EventCount).Select(battle.GetEvent)
                .Where(item => item.Kind == Define.BattleEventKind.HealthRestored).ToArray();
            Assert.That(battle.Result, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(healing.Where(item => item.TargetId == 1).Select(item => item.HealthRestored),
                Is.EqualTo(new[] { 80, 80, 80 }));
            Assert.That(healing.Where(item => item.TargetId == 3).Select(item => item.HealthRestored), Is.EqualTo(new[] { 10 }));
            Assert.That(healing.Any(item => item.TargetId == 5), Is.False);
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.BackUpper).Health, Is.EqualTo(1000));
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.BackLower).Health, Is.Zero);
        }
    }
}
