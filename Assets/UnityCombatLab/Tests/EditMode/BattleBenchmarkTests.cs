using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 描画せずコアの反復実行を測定します。
    /// [KO] 렌더링 없이 코어 반복 실행 측정.
    /// [EN] Measures repeated core execution without rendering.
    /// </summary>
    public sealed class BattleBenchmarkTests
    {
        /// <summary>
        /// FNV-1a 64の初期値。
        /// [KO] FNV-1a 64의 초기값.
        /// [EN] Initial offset basis of FNV-1a 64.
        /// </summary>
        private const ulong FnvOffsetBasis = 14695981039346656037UL;

        /// <summary>
        /// FNV-1a 64の素数。
        /// [KO] FNV-1a 64의 소수.
        /// [EN] Prime multiplier of FNV-1a 64.
        /// </summary>
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>
        /// 購入エセットに依存しない数値 fixture。
        /// [KO] 구매 에셋과 무관한 수치 fixture.
        /// [EN] Numeric fixture independent of purchased assets.
        /// </summary>
        internal static BattleUnitInput[] CreateInputs()
        {
            return new[]
            {
                BattleSimulationTests.Unit(1, Define.UnitRole.Knight, hp: 600, attack: 40, interval: 24),
                BattleSimulationTests.Unit(2, Define.UnitRole.Ranger, Define.BattleSlot.BackUpper,
                    hp: 340, attack: 34, interval: 18),
                BattleSimulationTests.Unit(3, Define.UnitRole.Mage, Define.BattleSlot.BackLower,
                    hp: 300, attack: 58, interval: 32),
                BattleSimulationTests.Unit(4, Define.UnitRole.SkeletonCaptain, hp: 900, attack: 72, interval: 40),
                BattleSimulationTests.Unit(5, Define.UnitRole.SkeletonRanged, Define.BattleSlot.BackUpper,
                    hp: 270, attack: 40, interval: 36),
                BattleSimulationTests.Unit(6, Define.UnitRole.SkeletonMelee, Define.BattleSlot.BackLower,
                    hp: 360, attack: 40, interval: 32)
            };
        }

        /// <summary>
        /// ウォームアップ後に構築と Step を別々に計測し証拠を保存。
        /// [KO] 워밍업 후 생성·Step 분리 측정 및 증거 저장.
        /// [EN] Warms up, measures construction and Step separately, and saves evidence.
        /// </summary>
        [Test]
        public void HeadlessRepeatedBattlesRecordTimeAllocationAndDeterminism()
        {
            const int Warmup = 100;
            const int Repetitions = 1000;
            const int LargeProbeBytes = 1024 * 1024;
            const int SmallProbeBytes = 2 * 1024;
            var inputs = CreateInputs();
            var recorder = Recorder.Get("GC.Alloc");
            var wasEnabled = recorder.enabled;
            try
            {
                recorder.enabled = false;
                recorder.FilterToCurrentThread();
                recorder.enabled = true;
                var beforeProbe = GC.GetAllocatedBytesForCurrentThread();
                var probeOne = new byte[LargeProbeBytes];
                var probeTwo = new byte[SmallProbeBytes];
                GC.KeepAlive(probeOne);
                GC.KeepAlive(probeTwo);
                var counterProbeBytes = GC.GetAllocatedBytesForCurrentThread() - beforeProbe;
                recorder.enabled = false;
                var probeSamples = recorder.sampleBlockCount;
                recorder.CollectFromAllThreads();
                recorder.enabled = wasEnabled;
                Assert.That(recorder.isValid && probeSamples >= 2, Is.True, "GC.Alloc must detect known allocations.");
                var expected = new BattleSimulation(BattleSimulationTests.RunId, 1, inputs);
                var expectedHash = Execute(expected);
                for (var i = 0; i < Warmup; i++)
                {
                    Execute(new BattleSimulation(BattleSimulationTests.RunId, 1, inputs));
                }

                var battles = new BattleSimulation[Repetitions];
                var stopwatch = new Stopwatch();
                var beforeConstruction = GC.GetAllocatedBytesForCurrentThread();
                recorder.enabled = false;
                recorder.FilterToCurrentThread();
                recorder.enabled = true;
                stopwatch.Start();
                for (var i = 0; i < Repetitions; i++)
                {
                    battles[i] = new BattleSimulation(BattleSimulationTests.RunId, 1, inputs);
                }

                stopwatch.Stop();
                recorder.enabled = false;
                var constructionSamples = recorder.sampleBlockCount;
                var constructionBytes = GC.GetAllocatedBytesForCurrentThread() - beforeConstruction;
                var constructionMs = stopwatch.Elapsed.TotalMilliseconds;

                var hasMatch = true;
                var totalTicks = 0L;
                var beforeStep = GC.GetAllocatedBytesForCurrentThread();
                recorder.enabled = true;
                stopwatch.Restart();
                for (var i = 0; i < Repetitions; i++)
                {
                    var battle = battles[i];
                    var hash = Execute(battle);
                    hasMatch &= hash == expectedHash && battle.Result == expected.Result &&
                                battle.CurrentTick == expected.CurrentTick;
                    totalTicks += battle.CurrentTick;
                }

                stopwatch.Stop();
                recorder.enabled = false;
                var stepSamples = recorder.sampleBlockCount;
                recorder.CollectFromAllThreads();
                recorder.enabled = wasEnabled;
                var stepBytes = GC.GetAllocatedBytesForCurrentThread() - beforeStep;
                var stepMs = stopwatch.Elapsed.TotalMilliseconds;
                var report = new StringBuilder();
                report.AppendLine("Headless Battle Benchmark (Editor EditMode, synchronous core only)");
                report.AppendLine("Unity=" + Application.unityVersion + "; Runtime=" + Environment.Version + "; OS=" +
                                  SystemInfo.operatingSystem);
                report.AppendLine("CPU=" + SystemInfo.processorType + "; StopwatchFrequency=" + Stopwatch.Frequency);
                report.AppendLine("Warmup=" + Warmup + "; Repetitions=" + Repetitions + "; RunId=" +
                                  BattleSimulationTests.RunId + "; Wave=1; Randomness=none");
                var fixtureDescription = string.Join(", ", inputs.Select(unit =>
                    $"{unit.Role}[{unit.Team}/{unit.Slot}]={unit.MaxHealth}/{unit.Attack}/{unit.IntervalTicks}" +
                    $";shield={unit.Shield}"));
                report.AppendLine("Fixture=HP/attack/interval: " + fixtureDescription);
                report.AppendLine("Result=" + expected.Result + "; TicksPerBattle=" + expected.CurrentTick +
                                  "; TotalTicks=" + totalTicks + "; EventHashFNV1a64=" + expectedHash.ToString("X16"));
                report.AppendLine("AllocationCalibration: 1MiB+2KiB arrays; GC.Alloc samples=" + probeSamples +
                                  "; ThreadCounterDelta=" + counterProbeBytes + "; ThreadCounterValid=" +
                                  (counterProbeBytes >= LargeProbeBytes + SmallProbeBytes));
                report.AppendLine("ConstructionMs=" + constructionMs.ToString("F3", CultureInfo.InvariantCulture) +
                                  "; GC.AllocSamples=" + constructionSamples + "; RawThreadCounterDelta=" +
                                  constructionBytes);
                report.AppendLine("StepAndHashMs=" + stepMs.ToString("F3", CultureInfo.InvariantCulture) +
                                  "; GC.AllocSamples=" + stepSamples + "; RawThreadCounterDelta=" + stepBytes +
                                  "; AllMatched=" + hasMatch);
                report.AppendLine(
                    "Allocation=Unity GC.Alloc Recorder, current thread, calibrated positive control. " +
                    "Raw thread-counter deltas are INVALID when calibration is false. " +
                    "GC.Alloc measures allocation occurrences, not byte sizes: " +
                    "zero samples means no observed managed allocations (0B); " +
                    "construction byte total unavailable on this runtime. Time=Stopwatch. No forced GC. " +
                    "Fixture/batch array/report/assertions/file I/O excluded; " +
                    "Step includes event value consumption/FNV/acknowledgement. " +
                    "Editor/hardware dependent; not Android/WebGL/FPS/peak memory evidence.");
                var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../_Development/Evidence/M2"));
                Directory.CreateDirectory(folder);
                File.WriteAllText(
                    Path.Combine(folder, "benchmark-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"),
                    report.ToString());
                TestContext.Out.WriteLine(report.ToString());
                Assert.That(hasMatch, Is.True);
                Assert.That(totalTicks, Is.EqualTo((long)expected.CurrentTick * Repetitions));
                Assert.That(constructionSamples, Is.GreaterThan(0), "Construction provides another positive control.");
                Assert.That(stepSamples, Is.Zero, "Steady-state Step and value consumption must not allocate.");
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
                recorder.enabled = wasEnabled;
            }
        }

        /// <summary>
        /// イベントの固定フィールド順を FNV-1a 64 で集約。
        /// [KO] 이벤트 고정 필드 순서를 FNV-1a 64로 집계.
        /// [EN] Folds fixed-order event fields using FNV-1a 64.
        /// </summary>
        private static ulong Execute(BattleSimulation battle)
        {
            var hash = FnvOffsetBasis;
            while (battle.Result == Define.BattleResult.Ongoing)
            {
                battle.Step();
                for (var i = 0; i < battle.EventCount; i++)
                {
                    var item = battle.GetEvent(i);
                    // RunId と wave は fixture 全体で固定し、数値は little endian で正規化する。
                    hash = HashInt(hash, item.Wave);
                    hash = HashInt(hash, item.Tick);
                    hash = HashInt(hash, item.Sequence);
                    hash = HashInt(hash, item.ActorId);
                    hash = HashInt(hash, item.TargetId);
                    hash = HashInt(hash, (int)item.Kind);
                    hash = HashInt(hash, item.HealthDamage);
                    hash = HashInt(hash, item.ShieldDamage);
                    hash = HashInt(hash, (int)item.Result);
                }

                battle.AcknowledgeEvents();
            }

            return hash;
        }

        /// <summary>
        /// 整数を固定の4バイト順に変換。
        /// [KO] 정수를 고정 4바이트 순서로 변환.
        /// [EN] Hashes an integer in fixed four-byte order.
        /// </summary>
        private static ulong HashInt(ulong hash, int value)
        {
            unchecked
            {
                for (var shift = 0; shift < 32; shift += 8)
                {
                    hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
                }
            }

            return hash;
        }
    }
}
