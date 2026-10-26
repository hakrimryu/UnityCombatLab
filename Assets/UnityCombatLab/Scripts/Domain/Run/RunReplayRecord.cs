using System;
using System.Collections.Generic;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 終了した Run の固定入力・強化履歴・正規化比較用イベントを保存します。
    /// [KO] 종료한 Run의 고정 입력·강화 이력·정규화 비교용 이벤트를 보관합니다.
    /// [EN] Stores a finished run's fixed inputs, upgrade history and events for normalized comparison.
    /// </summary>
    public sealed class RunReplayRecord
    {
        /// <summary>
        /// 実際の Run が読んだ設定スナップショット。
        /// [KO] 실제 Run이 읽은 설정 스냅샷입니다.
        /// [EN] Settings snapshot consumed by the actual run.
        /// </summary>
        public RunGameData Data { get; }

        /// <summary>
        /// 編成・難易度・シードと確定強化を含む終了結果。
        /// [KO] 편성·난이도·시드·확정 강화를 포함한 종료 결과입니다.
        /// [EN] Final result including formation, difficulty, seed and committed upgrades.
        /// </summary>
        public RunResult Result { get; }

        /// <summary>
        /// バッファから値としてコピーした全ウェーブのイベント。
        /// [KO] 버퍼에서 값으로 복사한 모든 웨이브 이벤트입니다.
        /// [EN] Events from every wave copied as values from the reusable buffer.
        /// </summary>
        public IReadOnlyList<BattleEvent> Events { get; }

        /// <summary>
        /// 読み取り専用の終了記録を作ります。Unity オブジェクト参照は保持しません。
        /// [KO] 읽기 전용 종료 기록을 만들며 Unity 객체 참조는 보관하지 않습니다.
        /// [EN] Creates a read-only finished record without retaining Unity object references.
        /// </summary>
        public RunReplayRecord(RunGameData data, RunResult result, IReadOnlyList<BattleEvent> events)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            var copy = new BattleEvent[events.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = events[index];

            Events = Array.AsReadOnly(copy);
        }
    }
}
