using System;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 五ウェーブの報酬・初期化と、Controller の入力制限を確認します。
    /// [KO] 다섯 웨이브의 보상·초기화와 Controller 입력 제한을 확인합니다.
    /// [EN] Checks five-wave rewards, resets and controller input guards.
    /// </summary>
    public sealed class RunProgressTests
    {
        /// <summary>
        /// Run と replay の検証が共有する、購入資産を使わない五ウェーブ入力を作ります。
        /// [KO] Run·replay 검증이 공유하는 구매 에셋 없는 다섯 웨이브 입력을 만듭니다.
        /// [EN] Creates asset-free five-wave input shared by run and replay checks.
        /// </summary>
        internal static RunGameData CreateData(int maxStacks = 3)
        {
            var waves = Enumerable.Range(0, 5).Select(index => new WaveData
            (new[] { Define.WaveEnemy.Melee, Define.WaveEnemy.Empty, Define.WaveEnemy.Empty },
                300,
                10,
                10000 + index * 1000)).ToArray();
            var increases = new[] { 1500, 2000, 1000, 1500, 800, 2000 };
            var rewards = Enumerable.Range(0, 6)
                .Select(index => new RewardData((Define.RewardKind)index, increases[index], maxStacks)).ToArray();
            var roles = Enumerable.Range(0, 6).Select(index => BattleSimulationTests.Unit
            (index + 1,
                (Define.UnitRole)index,
                hp: 1000,
                attack: 100,
                interval: 24)).ToArray();
            return new RunGameData(waves, rewards, roles);
        }

        /// <summary>
        /// 同じシードと選択で同じ候補が出て、再取得と重複選択が状態を変えないことを確認します。
        /// [KO] 같은 시드·선택에서 후보가 같고 재조회·중복 선택이 상태를 바꾸지 않는지 확인합니다.
        /// [EN] Checks repeatable offers and rejects duplicate choices without changing state.
        /// </summary>
        [Test]
        public void OffersAreStableAndChoicesAreAppliedOnce()
        {
            var data = CreateData();
            var first = new RunProgress(data, 123);
            var second = new RunProgress(data, 123);
            for (var wave = 1; wave < 5; wave++)
            {
                Assert.That(first.TryCompleteWave(wave, Define.BattleResult.Victory, 10), Is.True);
                Assert.That(second.TryCompleteWave(wave, Define.BattleResult.Victory, 10), Is.True);
                var offers = first.GetOffers();
                Assert.That(first.GetOffers(), Is.SameAs(offers));
                Assert.That(offers, Is.EqualTo(second.GetOffers()));
                Assert.That(offers.Distinct().Count(), Is.EqualTo(3));
                var unoffered = Enumerable.Range(0, 6).Select(index => (Define.RewardKind)index)
                    .First(kind => !offers.Contains(kind));
                Assert.That(first.TryChoose(wave, unoffered), Is.False);
                Assert.That(first.TryChoose(wave, null), Is.False);
                Assert.That(first.TryChoose(wave, offers[0]), Is.True);
                Assert.That(second.TryChoose(wave, offers[0]), Is.True);
                Assert.That(first.TryChoose(wave, offers[0]), Is.False);
                Assert.That(first.Rewards.History.Count, Is.EqualTo(wave));
                Assert.That(first.Rewards.History[wave - 1].IncreaseBp,
                    Is.EqualTo(data.GetReward(offers[0]).GetIncrease(10000 + (wave - 1) * 1000)));
            }
        }

        /// <summary>
        /// 上限一回の候補を再提示せず、五戦目には報酬を作らないことを確認します。
        /// [KO] 상한 한 번인 후보를 다시 제시하지 않고 다섯 번째 전투에는 보상을 만들지 않는지 확인합니다.
        /// [EN] Excludes capped rewards and offers no reward after the fifth battle.
        /// </summary>
        [Test]
        public void CappedKindsAreExcludedAndWaveFiveEndsTheRun()
        {
            var progress = new RunProgress(CreateData(1), 321);
            for (var wave = 1; wave <= 5; wave++)
            {
                Assert.That(progress.TryCompleteWave(wave, Define.BattleResult.Victory, wave * 10), Is.True);
                Assert.That(progress.TryCompleteWave(wave, Define.BattleResult.Victory, wave * 10), Is.False);
                if (wave == 5)
                    break;

                var offers = progress.GetOffers();
                Assert.That(offers.All(kind => progress.Rewards.GetStacks(kind) == 0), Is.True);
                Assert.That(progress.TryChoose(wave, offers[0]), Is.True);
            }

            Assert.That(progress.IsFinished, Is.True);
            Assert.Throws<InvalidOperationException>(() => progress.GetOffers());
            Assert.That(progress.TryChoose(5, null), Is.False);
            var result = progress.CreateResult(BattleSimulationTests.RunId, RunConfiguration.CreateDefault());
            Assert.That(result.ClearedWaves, Is.EqualTo(5));
            Assert.That(result.ReachedWave, Is.EqualTo(5));
            Assert.That(result.Ticks, Is.EqualTo(150));
            Assert.That(result.Rewards.History.Count, Is.EqualTo(4));
        }

        /// <summary>
        /// 強化の累積値を保持し、四回目の同種選択を拒否します。
        /// [KO] 강화 누적값을 유지하고 같은 종류 네 번째 선택을 거부합니다.
        /// [EN] Retains accumulated increments and rejects a fourth stack of the same kind.
        /// </summary>
        [Test]
        public void ThreeStacksKeepTheirOriginalIncrements()
        {
            var rewards = new RunRewards(new[]
            {
                new RewardChoice(1, Define.RewardKind.Power, 1500),
                new RewardChoice(2, Define.RewardKind.Power, 1650),
                new RewardChoice(3, Define.RewardKind.Power, 1800)
            });
            Assert.That(rewards.GetStacks(Define.RewardKind.Power), Is.EqualTo(3));
            Assert.That(rewards.GetEffect(Define.RewardKind.Power), Is.EqualTo(4950));
            Assert.Throws<ArgumentException>(() => rewards.Add(new RewardChoice(4, Define.RewardKind.Power, 1950)));
        }

        /// <summary>
        /// 最大 HP・攻撃・速度・盾の整数計算と、新ウェーブの全回復を確認します。
        /// [KO] 최대 HP·공격·속도·방어막 정수 계산과 새 웨이브의 전원 회복을 확인합니다.
        /// [EN] Checks integer stat scaling and full health at the start of a new wave.
        /// </summary>
        [Test]
        public void NewWaveUsesUpgradesAndFreshCombatState()
        {
            var rewards = new RunRewards(new[]
            {
                new RewardChoice(1, Define.RewardKind.Power, 1500),
                new RewardChoice(2, Define.RewardKind.Vitality, 2000),
                new RewardChoice(3, Define.RewardKind.Haste, 1000),
                new RewardChoice(4, Define.RewardKind.Barrier, 1500)
            });
            var data = CreateData();
            var configuration = RunConfiguration.CreateDefault();
            var first = data.CreateUnits(configuration, 5, rewards);
            var previousBattle = new BattleSimulation(BattleSimulationTests.RunId, 5, first, rewards);
            BattleSimulationTests.Advance(previousBattle, Define.FirstAttackTick);
            var previousKnight = previousBattle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front);
            Assert.That(previousKnight.AttackCount, Is.EqualTo(1));
            Assert.That(previousKnight.Shield, Is.LessThan(180));
            var next = data.CreateUnits(configuration, 5, rewards);
            var knight = next[0];
            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(knight.Health, Is.EqualTo(1200));
            Assert.That(knight.MaxHealth, Is.EqualTo(1200));
            Assert.That(knight.Attack, Is.EqualTo(115));
            Assert.That(knight.IntervalTicks, Is.EqualTo(22));
            Assert.That(knight.Shield, Is.EqualTo(180));
            Assert.That(next.Count, Is.EqualTo(4));
            Assert.That(next.Select(unit => unit.Id), Is.EqualTo(new[] { 1, 2, 3, 5 }));
            var battle = new BattleSimulation(BattleSimulationTests.RunId, 5, next, rewards);
            Assert.That(battle.CurrentTick, Is.Zero);
            Assert.That(battle.GetUnit(Define.BattleTeam.Ally, Define.BattleSlot.Front).AttackCount, Is.Zero);
            Assert.That(new RewardData(Define.RewardKind.Power, 1500).GetIncrease(11000), Is.EqualTo(1650));
        }

        /// <summary>
        /// Run ID・ウェーブ・停止状態で入力を制限し、次ウェーブでは速度だけを維持します。
        /// [KO] Run ID·웨이브·정지 상태로 입력을 제한하고 다음 웨이브에는 배속을 유지합니다.
        /// [EN] Guards input by run ID, wave and pause state while preserving speed between waves.
        /// </summary>
        [Test]
        public void ControllerGuardsPauseRewardsAndRestart()
        {
            var controller = new RunController();
            controller.TryStart(RunConfiguration.CreateDefault());
            var id = controller.Current.Id;
            controller.PrepareProgress(id, CreateData());
            controller.TryEnterBattle(id);
            Assert.That(controller.TryToggleSpeed(id), Is.False);
            Assert.That(controller.TryPause(id), Is.True);
            Assert.That(controller.TryBeginWave(id, 1), Is.False);
            Assert.That(controller.TryResume(id), Is.True);
            Assert.That(controller.TryBeginWave(id, 1), Is.True);
            Assert.That(controller.TryToggleSpeed(Guid.NewGuid()), Is.False);
            Assert.That(controller.TryToggleSpeed(id), Is.True);
            Assert.That(controller.Speed, Is.EqualTo(2));
            Assert.That(controller.TryPause(id), Is.True);
            Assert.That(controller.TryToggleSpeed(id), Is.False);
            Assert.That(controller.TrySetExitConfirmation(id, true), Is.True);
            Assert.That(controller.TryResume(id), Is.False);
            controller.TrySetExitConfirmation(id, false);
            controller.TryResume(id);
            Assert.That(controller.TryFinishWave(id, 1, Define.BattleResult.Victory, 100), Is.True);
            Assert.That(controller.TryOpenRewards(id), Is.True);
            var kind = controller.Progress.GetOffers()[0];
            Assert.That(controller.TrySelectReward(id, 2, kind), Is.False);
            Assert.That(controller.TrySelectReward(id, 1, kind), Is.True);
            Assert.That(controller.TrySelectReward(id, 1, kind), Is.False);
            Assert.That(controller.Speed, Is.EqualTo(2));
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.WaveIntro));
            controller.TryBeginCleanup(id);
            controller.TryCompleteCleanup(id);
            controller.TryStart(controller.LastConfiguration);
            Assert.That(controller.Speed, Is.EqualTo(1));
            Assert.That(controller.IsPaused, Is.False);
            Assert.That(controller.Progress, Is.Null);
            Assert.That(controller.Result, Is.Null);
        }
    }
}
