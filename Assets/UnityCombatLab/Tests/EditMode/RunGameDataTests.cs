using System;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 実際のウェーブ入力生成で、編成・難易度・役割定義の検査を確認します。
    /// [KO] 실제 웨이브 입력 생성에서 편성·난이도·역할 정의 검사를 확인합니다.
    /// [EN] Checks formation, difficulty and role validation through actual wave input generation.
    /// </summary>
    public sealed class RunGameDataTests
    {
        /// <summary>
        /// 三種類の敵を並べ、指定した基本能力と役割定義で五ウェーブの入力を作ります。
        /// [KO] 적 세 종류를 배치하고 지정한 기본 능력과 역할 정의로 다섯 웨이브 입력을 만듭니다.
        /// [EN] Creates five-wave input with all three enemy kinds and the supplied base stats and role templates.
        /// </summary>
        private static RunGameData CreateData(BattleUnitInput[] roles, int enemyHealth = 103, int enemyAttack = 13,
            Define.Difficulty difficulty = Define.Difficulty.Standard)
        {
            var wave = new WaveData
            (new[] { Define.WaveEnemy.Melee, Define.WaveEnemy.Ranged, Define.WaveEnemy.Captain },
                enemyHealth,
                enemyAttack,
                Define.BasisPoints);
            var rewards = Enumerable.Range(0, 6)
                .Select(index => new RewardData((Define.RewardKind)index, 1000)).ToArray();
            return new RunGameData(Enumerable.Repeat(wave, Define.WavesPerRun).ToArray(), rewards, roles, difficulty);
        }

        /// <summary>
        /// 役割定義の順序に依存せず、交換した英雄の能力を配置先へ移し、元の定義と設定を保持します。
        /// [KO] 역할 정의 순서와 무관하게 교환한 영웅의 능력을 배치 위치에 적용하고 원본 정의와 설정을 보존합니다.
        /// [EN] Places swapped heroes with matching stats regardless of template order while preserving the original definitions and configuration.
        /// </summary>
        [Test]
        public void SwappedFormationUsesMatchingRoleStatsWithoutMutatingSources()
        {
            var roles = BattleBenchmarkTests.CreateInputs();
            var mage = roles.Single(input => input.Role == Define.UnitRole.Mage);
            Array.Reverse(roles);
            var data = CreateData(roles);
            var configuration = RunConfiguration.CreateDefault();
            var swapped = configuration.Swap(Define.UnitRole.Mage, Define.BattleSlot.Front);
            var allies = data.CreateUnits(swapped, 1, RunRewards.Empty)
                .Where(input => input.Team == Define.BattleTeam.Ally).ToArray();

            Assert.That(allies.Select(input => input.Role),
                Is.EqualTo(new[] { Define.UnitRole.Mage, Define.UnitRole.Ranger, Define.UnitRole.Knight }));
            Assert.That(allies.Select(input => input.Slot),
                Is.EqualTo(new[] { Define.BattleSlot.Front, Define.BattleSlot.BackUpper, Define.BattleSlot.BackLower }));
            Assert.That(allies.Select(input => input.Id), Is.EqualTo(new[] { 1, 3, 5 }));
            foreach (var ally in allies)
            {
                var source = roles.Single(input => input.Role == ally.Role);
                Assert.That((ally.MaxHealth, ally.Attack, ally.IntervalTicks),
                    Is.EqualTo((source.MaxHealth, source.Attack, source.IntervalTicks)));
            }

            Assert.That(mage.Slot, Is.EqualTo(Define.BattleSlot.BackLower));
            roles[Array.IndexOf(roles, mage)] = roles.Single(input => input.Role == Define.UnitRole.Knight);
            Assert.That(data.CreateUnits(swapped, 1, RunRewards.Empty)[0].MaxHealth, Is.EqualTo(mage.MaxHealth));
            Assert.That(data.CreateUnits(configuration, 1, RunRewards.Empty)[0].Role, Is.EqualTo(Define.UnitRole.Knight));
        }

        /// <summary>
        /// 選択済みの難易度値に役割倍率だけを適用し、味方能力・盾・攻撃周期・配置を維持します。
        /// [KO] 선택한 난이도 값에 역할 배율만 적용하고 아군 능력·방어막·공격 주기·배치를 유지합니다.
        /// [EN] Applies only role multipliers to selected difficulty values while preserving ally stats, shields, intervals and formation.
        /// </summary>
        [Test]
        public void EasySnapshotIsNotScaledAgainBeforeRoleMultipliers()
        {
            var data = CreateData(BattleBenchmarkTests.CreateInputs());
            var configuration = RunConfiguration.CreateDefault();
            var rewards = new RunRewards(new[] { new RewardChoice(1, Define.RewardKind.Barrier, 1500) });
            var standard = data.CreateUnits(configuration, 2, rewards);
            var easyData = CreateData(BattleBenchmarkTests.CreateInputs(), 82, 10, Define.Difficulty.Easy);
            var easy = easyData.CreateUnits(configuration.WithDifficulty(Define.Difficulty.Easy), 2, rewards);
            var standardEnemies = standard.Where(input => input.Team == Define.BattleTeam.Enemy).ToArray();
            var easyEnemies = easy.Where(input => input.Team == Define.BattleTeam.Enemy).ToArray();

            // 前列・後列上・後列下は近接・遠距離・隊長。選択済みHP82に隊長係数2.5を掛けると205になる。
            Assert.That(standardEnemies.Select(input => input.MaxHealth), Is.EqualTo(new[] { 103, 77, 257 }));
            Assert.That(standardEnemies.Select(input => input.Attack), Is.EqualTo(new[] { 13, 13, 23 }));
            Assert.That(easyEnemies.Select(input => input.MaxHealth), Is.EqualTo(new[] { 82, 61, 205 }));
            Assert.That(easyEnemies.Select(input => input.Attack), Is.EqualTo(new[] { 10, 10, 18 }));
            Assert.That(easy.Count, Is.EqualTo(standard.Count));
            for (var index = 0; index < standard.Count; index++)
            {
                var original = standard[index];
                var scaled = easy[index];
                Assert.That((scaled.Id, scaled.Team, scaled.Slot, scaled.Role),
                    Is.EqualTo((original.Id, original.Team, original.Slot, original.Role)));
                Assert.That((scaled.Shield, scaled.IntervalTicks), Is.EqualTo((original.Shield, original.IntervalTicks)));
                if (original.Team == Define.BattleTeam.Ally)
                {
                    Assert.That((scaled.Health, scaled.MaxHealth, scaled.Attack),
                        Is.EqualTo((original.Health, original.MaxHealth, original.Attack)));
                }
            }
        }

        /// <summary>
        /// 基本能力が1でも、役割倍率の切り捨て後に敵能力の下限を維持します。
        /// [KO] 기본 능력이 1이어도 역할 배율을 내림한 뒤 적 능력의 하한을 유지합니다.
        /// [EN] Keeps enemy stats above zero after role scaling even with base stats of one.
        /// </summary>
        [Test]
        public void EasyKeepsMinimumStatsAfterRoleScaling()
        {
            var data = CreateData(BattleBenchmarkTests.CreateInputs(), enemyHealth: 1, enemyAttack: 1, difficulty: Define.Difficulty.Easy);
            var configuration = RunConfiguration.CreateDefault().WithDifficulty(Define.Difficulty.Easy);
            var enemies = data.CreateUnits(configuration, 1, RunRewards.Empty)
                .Where(input => input.Team == Define.BattleTeam.Enemy).ToArray();

            // 選択済みHP1に役割倍率を掛け、遠距離は下限1、隊長は2へ切り捨てる。
            Assert.That(enemies.Select(input => input.MaxHealth), Is.EqualTo(new[] { 1, 1, 2 }));
            Assert.That(enemies.Select(input => input.Attack), Is.EqualTo(new[] { 1, 1, 1 }));
        }

        /// <summary>
        /// 六役割の欠落・null・重複を、ウェーブ入力の生成前に拒否します。
        /// [KO] 여섯 역할의 누락·null·중복을 웨이브 입력 생성 전에 거부합니다.
        /// [EN] Rejects missing, null or duplicate role templates before generating wave inputs.
        /// </summary>
        [Test]
        public void RejectsMissingOrDuplicateRoleTemplates()
        {
            Assert.Throws<ArgumentException>(() => CreateData(null));
            Assert.Throws<ArgumentException>(() => CreateData(new BattleUnitInput[5]));
            var roles = BattleBenchmarkTests.CreateInputs();
            roles[1] = roles[0];
            Assert.Throws<ArgumentException>(() => CreateData(roles));
            roles = BattleBenchmarkTests.CreateInputs();
            roles[0] = null;
            Assert.Throws<ArgumentException>(() => CreateData(roles));
        }
    }
}
