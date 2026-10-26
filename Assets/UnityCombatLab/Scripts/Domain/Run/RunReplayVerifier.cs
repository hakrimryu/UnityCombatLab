using System;
using System.Collections.Generic;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// Unity や描画を使わず、シード・編成・難易度・強化履歴から Run 全体を再計算します。
    /// [KO] Unity·렌더링 없이 시드·편성·난이도·강화 이력으로 Run 전체를 재계산합니다.
    /// [EN] Recomputes the whole run from seed, formation, difficulty and upgrade history without Unity or rendering.
    /// </summary>
    public static class RunReplayVerifier
    {

        /// <summary>
        /// FNV-1a 64-bit の固定開始値。
        /// [KO] FNV-1a 64-bit 고정 시작값입니다.
        /// [EN] Fixed initial value for FNV-1a 64-bit.
        /// </summary>
        private const ulong FnvOffsetBasis = 14695981039346656037UL;

        /// <summary>
        /// FNV-1a 64-bit の固定乗数。
        /// [KO] FNV-1a 64-bit 고정 곱셈값입니다.
        /// [EN] Fixed multiplier for FNV-1a 64-bit.
        /// </summary>
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>
        /// 保存した入力で全ウェーブを再実行し、候補・増加量・イベント・最終結果の最初の差を返します。
        /// [KO] 저장 입력으로 모든 웨이브를 재실행하고 후보·증가량·이벤트·최종 결과의 최초 차이를 반환합니다.
        /// [EN] Replays all waves from recorded inputs and returns the first offer, increment, event or final-result difference.
        /// </summary>
        public static ReplayVerificationResult Verify(RunReplayRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var expectedHash = Hash(record.Events);
            var events = new List<BattleEvent>();
            var progress = new RunProgress(record.Data, record.Result.Configuration.Seed);
            var choiceIndex = 0;
            while (!progress.IsFinished)
            {
                var wave = progress.Wave;
                var battle = new BattleSimulation
                (record.Result.RunId,
                    wave,
                    record.Data.CreateUnits(record.Result.Configuration, wave, progress.Rewards),
                    progress.Rewards);
                while (battle.Result == Define.BattleResult.Ongoing)
                {
                    battle.Step();
                    for (var index = 0; index < battle.EventCount; index++)
                    {
                        var item = battle.GetEvent(index);
                        var position = events.Count;
                        events.Add(item);
                        if (position >= record.Events.Count)
                            return Failure(expectedHash, events, $"Event {position}: unexpected {Describe(item)}.");

                        if (!Same(item, record.Events[position]))
                            return Failure(expectedHash, events, $"Event {position}: expected {Describe(record.Events[position])}; actual {Describe(item)}.");
                    }

                    battle.AcknowledgeEvents();
                }

                if (!progress.TryCompleteWave(wave, battle.Result, battle.CurrentTick))
                    return Failure(expectedHash, events, $"Wave {wave}: duplicate or invalid completion.");

                if (progress.IsFinished)
                    break;

                var offers = progress.GetOffers();
                if (offers.Count == 0)
                {
                    if (!progress.TryChoose(wave, null))
                        return Failure(expectedHash, events, $"Wave {wave}: Continue was rejected.");

                    continue;
                }

                if (choiceIndex >= record.Result.Rewards.History.Count)
                    return Failure(expectedHash, events, $"Wave {wave}: missing upgrade choice.");

                var choice = record.Result.Rewards.History[choiceIndex++];
                if (choice.Wave != wave || choice.IncreaseBp != record.Data.GetReward(choice.Kind).GetIncrease(record.Data.GetWave(wave).RewardStrengthBp) ||
                    !progress.TryChoose(wave, choice.Kind))
                {
                    return Failure(expectedHash, events, $"Wave {wave}: upgrade {RewardData.GetId(choice.Kind)} or its increment differs from the offered choice.");
                }
            }

            var actualHash = Hash(events);
            if (events.Count != record.Events.Count)
                return new ReplayVerificationResult(false, expectedHash, actualHash, $"Event {events.Count}: missing {Describe(record.Events[events.Count])}.");

            var result = progress.CreateResult(record.Result.RunId, record.Result.Configuration);
            if (choiceIndex != record.Result.Rewards.History.Count || result.Outcome != record.Result.Outcome || result.Ticks != record.Result.Ticks ||
                result.ClearedWaves != record.Result.ClearedWaves || result.ReachedWave != record.Result.ReachedWave)
            {
                return new ReplayVerificationResult(false, expectedHash, actualHash, "Final outcome, wave counts, total ticks or choice count differs.");
            }

            return new ReplayVerificationResult(true, expectedHash, actualHash, "All normalized events, choices and final result match.");
        }

        /// <summary>
        /// RunId を除く固定フィールドを下位バイトから FNV-1a 64へ加えます。
        /// [KO] RunId를 제외한 고정 필드를 하위 바이트부터 FNV-1a 64에 더합니다.
        /// [EN] Hashes fixed fields except RunId into FNV-1a 64, least-significant byte first.
        /// </summary>
        public static ulong Hash(IReadOnlyList<BattleEvent> events)
        {
            var hash = FnvOffsetBasis;
            foreach (var item in events)
            {
                hash = HashInt(hash, item.Wave);
                hash = HashInt(hash, item.Tick);
                hash = HashInt(hash, item.Sequence);
                hash = HashInt(hash, item.ActorId);
                hash = HashInt(hash, item.TargetId);
                hash = HashInt(hash, (int)item.Kind);
                hash = HashInt(hash, item.HealthDamage);
                hash = HashInt(hash, item.ShieldDamage);
                hash = HashInt(hash, item.HealthRestored);
                hash = HashInt(hash, (int)item.Result);
            }

            return hash;
        }

        /// <summary>
        /// RunId の違いを除き、全イベント値を直接比較します。
        /// [KO] RunId 차이를 제외하고 모든 이벤트 값을 직접 비교합니다.
        /// [EN] Directly compares every event value except RunId.
        /// </summary>
        private static bool Same(BattleEvent left, BattleEvent right)
            => left.Wave == right.Wave && left.Tick == right.Tick && left.Sequence == right.Sequence && left.ActorId == right.ActorId &&
               left.TargetId == right.TargetId && left.Kind == right.Kind && left.HealthDamage == right.HealthDamage &&
               left.ShieldDamage == right.ShieldDamage && left.HealthRestored == right.HealthRestored && left.Result == right.Result;

        /// <summary>
        /// 最初の差を特定する位置・種類・対象・数値を一行にまとめます。
        /// [KO] 최초 차이를 찾을 위치·종류·대상·수치를 한 줄로 묶습니다.
        /// [EN] Formats position, kind, targets and values identifying the first difference on one line.
        /// </summary>
        private static string Describe(BattleEvent item)
            => $"W{item.Wave}/T{item.Tick}/S{item.Sequence} {item.Kind} {item.ActorId}->{item.TargetId} " +
               $"hp-{item.HealthDamage}/shield-{item.ShieldDamage}/heal+{item.HealthRestored}/{item.Result}";

        /// <summary>
        /// 最初に継続不能になった理由と、その時点のハッシュを返します。
        /// [KO] 처음 진행할 수 없게 된 이유·그 시점의 해시를 반환합니다.
        /// [EN] Returns the first unrecoverable difference and hashes at that point.
        /// </summary>
        private static ReplayVerificationResult Failure(ulong expected, IReadOnlyList<BattleEvent> events, string message)
            => new(false, expected, Hash(events), message);

        /// <summary>
        /// 整数を下位から四バイトに分け、実行環境に依存しない順でハッシュへ加えます。
        /// [KO] 정수를 하위부터 네 바이트로 나눠 환경과 무관한 순서로 해시에 더합니다.
        /// [EN] Hashes an integer in fixed least-significant-first four-byte order.
        /// </summary>
        private static ulong HashInt(ulong hash, int value)
        {
            unchecked
            {
                for (var shift = 0; shift < 32; shift += 8)
                    hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
            }

            return hash;
        }
    }
}
