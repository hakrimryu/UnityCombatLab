using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 戦闘オブジェクトを破棄した後も結果画面で使える、確定した一戦の結果。
    /// [KO] 전투 객체를 파괴한 뒤에도 결과 화면에서 사용할 수 있는 확정된 한 전투 결과입니다.
    /// [EN] Committed single-battle result usable after combat objects have been destroyed.
    /// </summary>
    public sealed class RunResult
    {
        /// <summary>
        /// 結果を確定した挑戦の ID。
        /// [KO] 결과를 확정한 도전 ID입니다.
        /// [EN] ID of the run that produced this result.
        /// </summary>
        public Guid RunId { get; }

        /// <summary>
        /// この挑戦で使った配置・難易度・シード。
        /// [KO] 이번 도전에 사용한 배치·난이도·시드입니다.
        /// [EN] Formation, difficulty and seed used by this run.
        /// </summary>
        public RunConfiguration Configuration { get; }

        /// <summary>
        /// Domain が確定した勝利・全滅・時間切れ。
        /// [KO] Domain이 확정한 승리·전멸·시간 초과입니다.
        /// [EN] Victory, defeat or timeout committed by the domain.
        /// </summary>
        public Define.BattleResult Outcome { get; }

        /// <summary>
        /// 最後に完了した tick 数。表示時間はこの値を20で割ります。
        /// [KO] 마지막으로 완료한 tick 수이며 표시 시간은 이 값을 20으로 나눕니다.
        /// [EN] Completed tick count; displayed seconds equal this value divided by twenty.
        /// </summary>
        public int Ticks { get; }

        /// <summary>
        /// 勝利したウェーブ数。
        /// [KO] 승리한 웨이브 수입니다.
        /// [EN] Number of victorious waves.
        /// </summary>
        public int ClearedWaves { get; }

        /// <summary>
        /// 最後に挑戦したウェーブ番号。
        /// [KO] 마지막으로 도전한 웨이브 번호입니다.
        /// [EN] Last wave attempted.
        /// </summary>
        public int ReachedWave { get; }

        /// <summary>
        /// 終了時点の強化。Run 解放後も値を保持します。
        /// [KO] 종료 시점의 강화이며 Run 해제 후에도 값을 유지합니다.
        /// [EN] Final upgrades retained after run resources are released.
        /// </summary>
        public RunRewards Rewards { get; }

        /// <summary>
        /// 確定モデルからコピーした結果だけを受け付けます。
        /// [KO] 확정 모델에서 복사한 결과만 받습니다.
        /// [EN] Accepts only a completed outcome copied from committed combat state.
        /// </summary>
        public RunResult(Guid runId, RunConfiguration configuration, Define.BattleResult outcome, int ticks,
            int clearedWaves = -1, int reachedWave = 1, RunRewards rewards = null)
        {
            if (runId == Guid.Empty)
                throw new ArgumentException("Run ID must not be empty.", nameof(runId));

            if (outcome == Define.BattleResult.Ongoing || !Enum.IsDefined(typeof(Define.BattleResult), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));

            if (ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks));

            if (clearedWaves == -1)
                clearedWaves = outcome == Define.BattleResult.Victory ? 1 : 0;

            if (reachedWave < 1 || reachedWave > Define.WavesPerRun || clearedWaves < 0 || clearedWaves > reachedWave ||
                (outcome == Define.BattleResult.Victory ? clearedWaves != reachedWave : clearedWaves != reachedWave - 1))
            {
                throw new ArgumentOutOfRangeException(nameof(clearedWaves));
            }

            if (ticks > reachedWave * Define.BattleTickLimit)
                throw new ArgumentOutOfRangeException(nameof(ticks));

            RunId = runId;
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Outcome = outcome;
            Ticks = ticks;
            ClearedWaves = clearedWaves;
            ReachedWave = reachedWave;
            Rewards = rewards ?? RunRewards.Empty;
        }
    }
}
