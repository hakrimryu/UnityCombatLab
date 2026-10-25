using System;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 出発時の能力とモデル選択が一致することを確認します。
    /// [KO] 출발 능력과 모델 선택이 일치하는지 확인합니다.
    /// [EN] Verifies departure stats and model selection agree.
    /// </summary>
    public sealed class RunBattleSetupTests
    {
        /// <summary>
        /// ベンチマークの能力値を、Dungeonが渡す前列・後列ごとの味方/敵順に並べます。
        /// [KO] 벤치마크 능력치를 Dungeon이 전달하는 전열·후열별 아군/적 순서로 정렬합니다.
        /// [EN] Orders benchmark stats by slot and ally/enemy, matching Dungeon's source order.
        /// </summary>
        private static BattleUnitInput[] CreateSources()
            => BattleBenchmarkTests.CreateInputs()
                .OrderBy(input => input.Slot).ThenBy(input => input.Team).ToArray();

        /// <summary>
        /// 配置交換でもモデル番号と能力が同じ英雄を指し、元の配列は変更しません。
        /// [KO] 배치 교환 시 모델 번호와 능력이 같은 영웅을 가리키고 원본 배열은 바뀌지 않는지 확인합니다.
        /// [EN] Checks swapped stats and model indices select the same hero without mutating the source.
        /// </summary>
        [Test]
        public void SwapsUseMatchingSourceStatsAndModels()
        {
            var original = CreateSources();
            var setup = new RunBattleSetup(RunConfiguration.CreateDefault().Swap(Define.UnitRole.Mage, Define.BattleSlot.Front), original);
            Assert.That(setup.GetSourceIndex(0), Is.EqualTo(4));
            Assert.That(setup.GetSourceIndex(4), Is.EqualTo(0));
            Assert.That(setup.Units[0].Role, Is.EqualTo(Define.UnitRole.Mage));
            Assert.That(setup.Units[0].MaxHealth, Is.EqualTo(original[4].MaxHealth));
            Assert.That(setup.Units[0].Attack, Is.EqualTo(original[4].Attack));
            Assert.That(setup.Units[0].Id, Is.EqualTo(1));
            Assert.That(setup.Units[0].Slot, Is.EqualTo(Define.BattleSlot.Front));
            Assert.That(original[0].Role, Is.EqualTo(Define.UnitRole.Knight));
            original[4] = original[0];
            Assert.That(setup.Units[0].Role, Is.EqualTo(Define.UnitRole.Mage));
        }

        /// <summary>
        /// やさしい設定は敵のHPと攻撃だけを80%に切り捨て、盾と周期を維持します。
        /// [KO] 쉬움 설정은 적 HP와 공격만 80%로 내림하고 방어막과 주기를 유지하는지 확인합니다.
        /// [EN] Checks easy mode floors only enemy health and attack to 80%, preserving shields and intervals.
        /// </summary>
        [Test]
        public void EasyScalesOnlyEnemyHealthAndAttack()
        {
            var originals = CreateSources();
            originals[1] = new BattleUnitInput(2, Define.BattleTeam.Enemy, Define.BattleSlot.Front,
                Define.UnitRole.SkeletonMelee, 103, 13, 32, 7);
            var setup = new RunBattleSetup(RunConfiguration.CreateDefault().WithDifficulty(Define.Difficulty.Easy), originals);
            Assert.That(setup.Units[1].MaxHealth, Is.EqualTo(82));
            Assert.That(setup.Units[1].Attack, Is.EqualTo(10));
            Assert.That(setup.Units[1].IntervalTicks, Is.EqualTo(32));
            Assert.That(setup.Units[1].Shield, Is.EqualTo(7));
            Assert.That(setup.Units[0].MaxHealth, Is.EqualTo(originals[0].MaxHealth));
            Assert.That(setup.Units[0].Attack, Is.EqualTo(originals[0].Attack));
            var standard = new RunBattleSetup(RunConfiguration.CreateDefault(), originals);
            Assert.That(standard.Units[1].MaxHealth, Is.EqualTo(103));
        }

        /// <summary>
        /// 原型の順序違いと英雄重複を出発前に拒否します。
        /// [KO] 원형 순서 오류와 영웅 중복을 출발 전에 거부하는지 확인합니다.
        /// [EN] Rejects unordered source slots and duplicate heroes before departure.
        /// </summary>
        [Test]
        public void RejectsInvalidSourceMapping()
        {
            var sources = CreateSources();
            Array.Reverse(sources);
            Assert.Throws<ArgumentException>(() => new RunBattleSetup(RunConfiguration.CreateDefault(), sources));
            sources = CreateSources();
            sources[4] = new BattleUnitInput(5, Define.BattleTeam.Ally, Define.BattleSlot.BackLower, Define.UnitRole.Knight, 100, 10, 24);
            Assert.Throws<ArgumentException>(() => new RunBattleSetup(RunConfiguration.CreateDefault(), sources));
            Assert.Throws<ArgumentException>(() => new RunBattleSetup(RunConfiguration.CreateDefault(), new BattleUnitInput[5]));
        }
    }
}
