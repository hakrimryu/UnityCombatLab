using System;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 三人の配置検証と、不変の開始設定を確認します。
    /// [KO] 세 영웅 배치 검증과 불변 시작 설정을 확인합니다.
    /// [EN] Checks three-hero validation and immutable starting configuration.
    /// </summary>
    public sealed class RunConfigurationTests
    {
        /// <summary>
        /// 入力配列の変更が、出発時の配置へ漏れないことを確認します。
        /// [KO] 입력 배열 변경이 출발 시 배치에 반영되지 않는지 확인합니다.
        /// [EN] Verifies caller array changes cannot alter departure formation.
        /// </summary>
        [Test]
        public void ConfigurationCopiesFormationAndRejectsInvalidHeroes()
        {
            var roles = new[] { Define.UnitRole.Knight, Define.UnitRole.Ranger, Define.UnitRole.Mage };
            var configuration = new RunConfiguration(roles, Define.Difficulty.Standard, 0);
            roles[0] = Define.UnitRole.Mage;
            Assert.That(configuration.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Knight));
            Assert.That(configuration.Seed, Is.EqualTo(20261024u));
            Assert.Throws<ArgumentException>(() => new RunConfiguration(roles, Define.Difficulty.Standard));
            Assert.Throws<ArgumentException>(() => new RunConfiguration(new[] { Define.UnitRole.Knight }, Define.Difficulty.Standard));
            Assert.Throws<ArgumentException>(() => new RunConfiguration(new[] { Define.UnitRole.Knight, Define.UnitRole.Ranger, Define.UnitRole.SkeletonMelee }, Define.Difficulty.Standard));
            Assert.Throws<ArgumentOutOfRangeException>(() => configuration.GetRole((Define.BattleSlot)3));
            Assert.Throws<ArgumentOutOfRangeException>(() => configuration.WithDifficulty((Define.Difficulty)7));
        }

        /// <summary>
        /// 配置交換と難易度の変更が元の設定を変えず、同じ配置への移動も有効なままであることを確認します。
        /// [KO] 배치 교환과 난이도 변경이 원본을 바꾸지 않고 같은 슬롯 이동도 유효한지 확인합니다.
        /// [EN] Verifies swaps and difficulty changes preserve the original and allow a same-slot no-op.
        /// </summary>
        [Test]
        public void SwapPreservesThreeDistinctHeroesAndOriginalSnapshot()
        {
            var original = RunConfiguration.CreateDefault();
            var swapped = original.Swap(Define.UnitRole.Mage, Define.BattleSlot.Front).WithDifficulty(Define.Difficulty.Easy);
            Assert.That(swapped.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Mage));
            Assert.That(swapped.GetRole(Define.BattleSlot.BackLower), Is.EqualTo(Define.UnitRole.Knight));
            Assert.That(swapped.GetRole(Define.BattleSlot.BackUpper), Is.EqualTo(Define.UnitRole.Ranger));
            Assert.That(original.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Knight));
            Assert.That(original.Difficulty, Is.EqualTo(Define.Difficulty.Standard));
            Assert.That(swapped.Swap(Define.UnitRole.Mage, Define.BattleSlot.Front).GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Mage));
        }
    }
}
