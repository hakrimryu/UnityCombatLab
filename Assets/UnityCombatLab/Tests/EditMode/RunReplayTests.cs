using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 五ウェーブの固定期待値と再計算を使い、規則の回帰・速度差・改変検出を確認します。
    /// [KO] 다섯 웨이브의 고정 기대값·재계산으로 규칙 회귀·배속 차이·변조 검출을 확인합니다.
    /// [EN] Uses fixed five-wave expectations and replay to check rule regressions, speed independence and tampering.
    /// </summary>
    public sealed class RunReplayTests
    {
        /// <summary>
        /// 固定入力の全152イベントを、RunIdを除く10整数の下位バイト順でFNV-1a 64にした期待値。
        /// [KO] 고정 입력의 전체 152이벤트에서 RunId를 뺀 정수 10개를 하위 바이트 순 FNV-1a 64로 계산한 기대값입니다.
        /// [EN] Expected FNV-1a 64 of all 152 fixed-input events, using ten little-endian integers and excluding RunId.
        /// </summary>
        private const ulong GoldenEventHash = 0x135806989FA96900UL;

        /// <summary>
        /// 固定入力の終了tick合計。各ウェーブ82・82・82・82・58で386です。
        /// [KO] 고정 입력의 종료 tick 합계이며 각 웨이브 82·82·82·82·58로 386입니다.
        /// [EN] Expected total of 386 ticks from wave endings at 82, 82, 82, 82 and 58.
        /// </summary>
        private const int GoldenTotalTicks = 386;

        /// <summary>
        /// 固定入力のイベント数。各ウェーブ34・34・28・29・27件で152です。
        /// [KO] 고정 입력의 이벤트 수이며 각 웨이브 34·34·28·29·27건으로 152입니다.
        /// [EN] Expected total of 152 events from per-wave counts of 34, 34, 28, 29 and 27.
        /// </summary>
        private const int GoldenEventCount = 152;

        /// <summary>
        /// このテスト専用の入力・選択と固定結果を比較し、録画側と再現側が同時に変わる回帰も検出します。
        /// [KO] 이 테스트 전용 입력·선택을 고정 결과와 비교해 기록 측·재현 측이 함께 바뀌는 회귀도 검출합니다.
        /// [EN] Compares test-owned inputs and choices with fixed results to catch regressions shared by recording and replay.
        /// </summary>
        [TestCase(0.016, 1)]
        [TestCase(0.016, 2)]
        [TestCase(0.13, 2)]
        public void RecordedRunMatchesFixedRegressionBaseline(double delta, int speed)
        {
            // 入力と期待値は一組の仕様。変更時は規則変更の理由を記録し、失敗した実測値を自動採用しない。
            var configuration = new RunConfiguration
            (new[] { Define.UnitRole.Knight, Define.UnitRole.Mage, Define.UnitRole.Ranger },
                Define.Difficulty.Standard,
                20261024u);
            var choices = new[]
            {
                new RewardChoice(1, Define.RewardKind.Power, 1500),
                new RewardChoice(2, Define.RewardKind.Power, 1650),
                new RewardChoice(3, Define.RewardKind.Renewal, 960),
                new RewardChoice(4, Define.RewardKind.Power, 1950)
            };
            var record = Record(delta, speed, CreateGoldenData(), configuration, choices);
            var replay = RunReplayVerifier.Verify(record);
            var hash = RunReplayVerifier.Hash(record.Events);
            Assert.That(record.Result.Outcome, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(record.Result.Ticks, Is.EqualTo(GoldenTotalTicks));
            Assert.That(record.Result.ClearedWaves, Is.EqualTo(5));
            Assert.That(record.Result.ReachedWave, Is.EqualTo(5));
            Assert.That(record.Result.Rewards.History.Select(choice => (choice.Wave, choice.Kind, choice.IncreaseBp)),
                Is.EqualTo(choices.Select(choice => (choice.Wave, choice.Kind, choice.IncreaseBp))));
            Assert.That(record.Events.Count, Is.EqualTo(GoldenEventCount));
            Assert.That(record.Events.GroupBy(item => item.Wave).Select(group => group.Count()),
                Is.EqualTo(new[] { 34, 34, 28, 29, 27 }));
            Assert.That(record.Events.Where(item => item.Kind == Define.BattleEventKind.BattleEnded)
                .Select(item => item.Tick), Is.EqualTo(new[] { 82, 82, 82, 82, 58 }));
            Assert.That(hash, Is.EqualTo(GoldenEventHash),
                $"Fixed replay hash changed: {hash:X16}. Review the rule/input change before updating the baseline.");
            Assert.That(replay.IsMatch, Is.True, replay.Message);
            Assert.That(replay.ExpectedHash, Is.EqualTo(GoldenEventHash));
            Assert.That(replay.ActualHash, Is.EqualTo(GoldenEventHash));
        }

        /// <summary>
        /// 購入資産・共通fixture・実行時の既定値に依存しない、固定回帰専用の五ウェーブ入力を作ります。
        /// [KO] 구매 에셋·공통 fixture·런타임 기본값에 의존하지 않는 고정 회귀 전용 다섯 웨이브 입력을 만듭니다.
        /// [EN] Creates fixed regression inputs independent of purchased assets, shared fixtures and runtime defaults.
        /// </summary>
        private static RunGameData CreateGoldenData()
        {
            // 敵HP1250で魔術師の3回目70%攻撃と弓使いの4回目200%攻撃を通る。初撃10tick、以後24tick間隔。
            // Power後の攻撃力は100→115→131→131→151と切り捨て、Renewalは第4・5戦の撃破時に騎士を96回復する。
            var waves = new[] { 10000, 11000, 12000, 13000, 14000 }.Select(strength => new WaveData
            (new[] { Define.WaveEnemy.Melee, Define.WaveEnemy.Empty, Define.WaveEnemy.Empty },
                1250,
                100,
                strength)).ToArray();
            var rewards = new[]
            {
                new RewardData(Define.RewardKind.Power, 1500, 3),
                new RewardData(Define.RewardKind.Vitality, 2000, 3),
                new RewardData(Define.RewardKind.Haste, 1000, 3),
                new RewardData(Define.RewardKind.Barrier, 1500, 3),
                new RewardData(Define.RewardKind.Renewal, 800, 3),
                new RewardData(Define.RewardKind.Ambush, 2000, 3)
            };
            var roleOrder = new[]
            {
                Define.UnitRole.Knight, Define.UnitRole.Ranger, Define.UnitRole.Mage,
                Define.UnitRole.SkeletonMelee, Define.UnitRole.SkeletonRanged, Define.UnitRole.SkeletonCaptain
            };
            // 先頭3種は味方、後半3種は敵の能力原型。実戦のID・配置・全回復はCreateUnitsが開始設定から作る。
            var roles = roleOrder.Select((role, index) => new BattleUnitInput
            (index + 1,
                index < 3 ? Define.BattleTeam.Ally : Define.BattleTeam.Enemy,
                Define.BattleSlot.Front,
                role,
                1000,
                100,
                24,
                0)).ToArray();
            return new RunGameData(waves, rewards, roles);
        }

        /// <summary>
        /// 同じ入力を異なるフレーム幅と速度で進めても全イベントと結果が一致することを確認します。
        /// [KO] 같은 입력을 다른 프레임 간격·배속으로 진행해도 전체 이벤트·결과가 같은지 확인합니다.
        /// [EN] Checks identical events and results across different frame deltas and speeds.
        /// </summary>
        [TestCase(0.016, 1)]
        [TestCase(0.016, 2)]
        [TestCase(0.13, 2)]
        public void RecordedRunMatchesHeadlessReplay(double delta, int speed)
        {
            var baseline = Record(0.05, 1);
            var record = Record(delta, speed);
            var result = RunReplayVerifier.Verify(record);
            Assert.That(result.IsMatch, Is.True, result.Message);
            Assert.That(result.ActualHash, Is.EqualTo(result.ExpectedHash));
            Assert.That(result.ActualHash, Is.EqualTo(RunReplayVerifier.Hash(baseline.Events)));
            Assert.That(record.Result.Ticks, Is.EqualTo(baseline.Result.Ticks));
            Assert.That(record.Result.Rewards.History, Is.EqualTo(baseline.Result.Rewards.History));
            Assert.That(record.Result.ClearedWaves, Is.EqualTo(5));
        }

        /// <summary>
        /// イベントの欠落と選択増加量の改変を成功として扱わないことを確認します。
        /// [KO] 이벤트 누락·선택 증가량 변조를 성공으로 처리하지 않는지 확인합니다.
        /// [EN] Rejects missing events and modified reward increments.
        /// </summary>
        [Test]
        public void MissingEventAndAlteredChoiceAreRejected()
        {
            var record = Record(0.05, 1);
            var events = record.Events.ToList();
            events.RemoveAt(0);
            var missing = RunReplayVerifier.Verify(new RunReplayRecord(record.Data, record.Result, events));
            Assert.That(missing.IsMatch, Is.False);
            Assert.That(missing.Message, Does.Contain("Event 0"));
            var choices = record.Result.Rewards.History.ToArray();
            choices[0] = new RewardChoice(choices[0].Wave, choices[0].Kind, choices[0].IncreaseBp + 1);
            var changed = new RunResult
            (record.Result.RunId,
                record.Result.Configuration,
                record.Result.Outcome,
                record.Result.Ticks,
                record.Result.ClearedWaves,
                record.Result.ReachedWave,
                new RunRewards(choices));
            var altered = RunReplayVerifier.Verify(new RunReplayRecord(record.Data, changed, record.Events));
            Assert.That(altered.IsMatch, Is.False);
            Assert.That(altered.Message, Does.Contain("Wave 1"));
        }

        /// <summary>
        /// 各tickのイベントを記録し、指定した選択、または未指定なら最初の候補で五ウェーブを進めます。
        /// [KO] 각 tick 이벤트를 기록하고 지정한 선택 또는 미지정 시 첫 후보로 다섯 웨이브를 진행합니다.
        /// [EN] Records each tick's events and advances five waves with supplied choices or the first offer when unspecified.
        /// </summary>
        private static RunReplayRecord Record(double delta, int speed, RunGameData data = null,
            RunConfiguration configuration = null, IReadOnlyList<RewardChoice> choices = null)
        {
            data ??= RunProgressTests.CreateData();
            configuration ??= RunConfiguration.CreateDefault().Swap(Define.UnitRole.Mage, Define.BattleSlot.Front);
            var progress = new RunProgress(data, configuration.Seed);
            var events = new List<BattleEvent>();
            while (!progress.IsFinished)
            {
                var wave = progress.Wave;
                using var runner = new BattleRunner
                (BattleSimulationTests.RunId,
                    wave,
                    data.CreateUnits(configuration, wave, progress.Rewards),
                    progress.Rewards);
                runner.EventOccurred += events.Add;
                for (var frame = 0; runner.Result == Define.BattleResult.Ongoing && frame < 20000; frame++)
                {
                    if (frame % 7 == 0)
                    {
                        var tick = runner.CurrentTick;
                        var count = events.Count;
                        runner.Advance(0);
                        Assert.That(runner.CurrentTick, Is.EqualTo(tick));
                        Assert.That(events.Count, Is.EqualTo(count));
                    }

                    runner.Advance(delta * speed);
                }

                Assert.That(runner.Result, Is.Not.EqualTo(Define.BattleResult.Ongoing));
                Assert.That(progress.TryCompleteWave(wave, runner.Result, runner.CurrentTick), Is.True);
                if (!progress.IsFinished)
                {
                    var offers = progress.GetOffers();
                    var choice = choices == null ? offers[0] : choices[wave - 1].Kind;
                    Assert.That(progress.TryChoose(wave, choice), Is.True, $"Wave {wave}: {choice} must be offered.");
                }
            }

            return new RunReplayRecord(data, progress.CreateResult(BattleSimulationTests.RunId, configuration), events);
        }
    }
}
