using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.ReadModels;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 到着順が変わる被害と回復について、HP と表示通知が一致することを確認します。
    /// [KO] 도착 순서가 다른 피해·회복에서 HP와 표시 알림이 일치하는지 확인합니다.
    /// [EN] Checks agreement between health and presentation notifications for reordered damage and healing.
    /// </summary>
    public sealed class UnitPresentationReadModelTests
    {
        /// <summary>
        /// 魔法使いが前列でも、先に到着した騎士の被害を待たせないことを確認します。
        /// [KO] 마법사가 전열이어도 먼저 도착한 기사 피해를 기다리게 하지 않는지 확인합니다.
        /// [EN] Presents the knight's earlier impact immediately even with the mage in front.
        /// </summary>
        [Test]
        public void DamageOnlyChangesFollowArrivalOrder()
        {
            var enemy = BattleSimulationTests.Unit(2, Define.UnitRole.SkeletonMelee, hp: 1000, attack: 0);
            var battle = new BattleSimulation
            (BattleSimulationTests.RunId,
                1,
                new[]
                {
                    BattleSimulationTests.Unit(1, Define.UnitRole.Mage, attack: 58),
                    enemy,
                    BattleSimulationTests.Unit(3, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper, attack: 34),
                    BattleSimulationTests.Unit(5, Define.UnitRole.Knight, Define.BattleSlot.BackLower, attack: 40)
                });
            BattleSimulationTests.Advance(battle, Define.FirstAttackTick);
            var damage = Enumerable.Range(0, battle.EventCount).Select(battle.GetEvent)
                .Where(item => item.Kind == Define.BattleEventKind.DamageApplied && item.TargetId == 2).ToArray();
            using var model = new UnitPresentationReadModel(enemy);
            var health = model.Health;
            var shown = new List<(int Actor, int Health)>();
            // 実際の HP 変更後に通知されるかを、数字を出す側と同じ購読で観測する。
            model.ChangePresented += item => shown.Add((item.ActorId, health.CurrentValue));
            foreach (var item in damage)
                model.ReserveChange(item);

            model.PresentChange(damage[2].Sequence);
            Assert.That(shown, Is.EqualTo(new[] { (5, 960) }));
            model.PresentChange(damage[1].Sequence);
            Assert.That(model.Health.CurrentValue, Is.EqualTo(926));
            model.PresentChange(damage[0].Sequence);
            Assert.That(shown, Is.EqualTo(new[] { (5, 960), (3, 926), (1, 868) }));
            Assert.That(model.PreviewHealth.CurrentValue, Is.EqualTo(868));
            Assert.Throws<InvalidOperationException>(() => model.PresentChange(damage[2].Sequence));
        }

        /// <summary>
        /// 回復の後の被害が先着しても、HP 範囲と被害・回復通知の順を守ることを確認します。
        /// [KO] 회복 뒤 피해가 먼저 도착해도 HP 범위와 피해·회복 알림 순서를 지키는지 확인합니다.
        /// [EN] Preserves health bounds and damage/healing notification order when later damage arrives first.
        /// </summary>
        [Test]
        public void HealingSeparatesDamageGroupsAndNotificationsWaitForApplication()
        {
            var knight = BattleSimulationTests.Unit(1, Define.UnitRole.Knight, hp: 100, attack: 10);
            var battle = new BattleSimulation
            (BattleSimulationTests.RunId,
                2,
                new[]
                {
                    knight,
                    BattleSimulationTests.Unit(2, Define.UnitRole.SkeletonMelee, hp: 11, attack: 20),
                    BattleSimulationTests.Unit(3, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper, attack: 10),
                    BattleSimulationTests.Unit(4, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackUpper, attack: 20)
                },
                new RunRewards(new[] { new RewardChoice(1, Define.RewardKind.Renewal, 800) }));
            BattleSimulationTests.Advance(battle, Define.FirstAttackTick);
            var changes = Enumerable.Range(0, battle.EventCount).Select(battle.GetEvent)
                .Where(item => item.TargetId == 1 &&
                    (item.Kind == Define.BattleEventKind.DamageApplied || item.Kind == Define.BattleEventKind.HealthRestored)).ToArray();
            Assert.That(changes.Select(item => item.Kind), Is.EqualTo(new[]
            {
                Define.BattleEventKind.DamageApplied, Define.BattleEventKind.HealthRestored, Define.BattleEventKind.DamageApplied
            }));
            using var model = new UnitPresentationReadModel(knight);
            var health = model.Health;
            var shown = new List<int>();
            model.ChangePresented += _ => shown.Add(health.CurrentValue);
            foreach (var item in changes)
                model.ReserveChange(item);

            model.PresentChange(changes[2].Sequence);
            model.PresentChange(changes[1].Sequence);
            Assert.That(shown, Is.Empty);
            Assert.That(model.Health.CurrentValue, Is.EqualTo(100));
            model.PresentChange(changes[0].Sequence);
            Assert.That(shown, Is.EqualTo(new[] { 84, 92, 76 }));
            Assert.That(model.PreviewHealth.CurrentValue, Is.EqualTo(76));
        }
    }
}
