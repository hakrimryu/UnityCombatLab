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
    /// 実際の Runner が記録した五ウェーブを再計算し、速度差と改変検出を確認します。
    /// [KO] 실제 Runner가 기록한 다섯 웨이브를 재계산해 배속 차이·변조 검출을 확인합니다.
    /// [EN] Recomputes five waves recorded by the runner, checking speed independence and tamper detection.
    /// </summary>
    public sealed class RunReplayTests
    {
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
        /// 各 tick のイベントを消費して記録し、提示された最初の報酬で五ウェーブを進めます。
        /// [KO] 각 tick 이벤트를 소비·기록하고 첫 제시 보상으로 다섯 웨이브를 진행합니다.
        /// [EN] Records consumed events and advances five waves by choosing the first offered reward.
        /// </summary>
        private static RunReplayRecord Record(double delta, int speed)
        {
            var data = RunProgressTests.CreateData();
            var configuration = RunConfiguration.CreateDefault().Swap(Define.UnitRole.Mage, Define.BattleSlot.Front);
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
                    Assert.That(progress.TryChoose(wave, progress.GetOffers()[0]), Is.True);
            }

            return new RunReplayRecord(data, progress.CreateResult(BattleSimulationTests.RunId, configuration), events);
        }
    }
}
