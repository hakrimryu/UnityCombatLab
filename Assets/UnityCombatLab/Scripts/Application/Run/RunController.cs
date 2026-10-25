using System;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Application.Run
{
    /// <summary>
    /// 出発・戦闘開始・終了処理の順序を検査し、同時に一つの挑戦だけを許可します。
    /// [KO] 출발·전투 시작·종료 처리 순서를 검사하고 동시에 하나의 도전만 허용합니다.
    /// [EN] Validates departure, combat and cleanup order, permitting one run at a time.
    /// </summary>
    public sealed class RunController
    {
        /// <summary>
        /// 資源の片付けが終わるまで保持する現在の挑戦。
        /// [KO] 자원 정리가 끝날 때까지 보관하는 현재 도전입니다.
        /// [EN] Current run retained until resource cleanup completes.
        /// </summary>
        public RunState Current { get; private set; }

        /// <summary>
        /// 次の編成画面へ持ち越す配置・難易度。
        /// [KO] 다음 편성 화면으로 유지할 배치·난이도입니다.
        /// [EN] Formation and difficulty retained for the next formation screen.
        /// </summary>
        public RunConfiguration LastConfiguration { get; private set; } = RunConfiguration.CreateDefault();

        /// <summary>
        /// 戦闘の参照を含まない直前の結果。中断した挑戦では null です。
        /// [KO] 전투 참조가 없는 직전 결과이며 중단된 도전은 null입니다.
        /// [EN] Last result without combat references; null for an abandoned run.
        /// </summary>
        public RunResult Result { get; private set; }

        /// <summary>
        /// 実行中の挑戦がないときだけ編成画面の選択を保存します。
        /// [KO] 실행 중인 도전이 없을 때만 편성 화면 선택을 저장합니다.
        /// [EN] Remembers formation choices only while no run is active.
        /// </summary>
        public void RememberConfiguration(RunConfiguration configuration)
        {
            if (Current != null)
                throw new InvalidOperationException("Cannot edit an active run's formation.");

            LastConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// 重複出発を拒否し、新しい ID の読み込み状態を一度だけ作ります。
        /// [KO] 중복 출발을 거부하고 새 ID의 로딩 상태를 한 번만 만듭니다.
        /// [EN] Rejects duplicate departures and creates a loading state with a fresh ID once.
        /// </summary>
        public bool TryStart(RunConfiguration configuration)
        {
            if (Current != null)
                return false;

            RememberConfiguration(configuration);
            Result = null;
            Current = new RunState(Guid.NewGuid(), configuration, Define.RunPhase.Loading);
            return true;
        }

        /// <summary>
        /// 現在の読み込み ID が一致した場合だけ戦闘開始を許可します。
        /// [KO] 현재 로딩 ID가 일치할 때만 전투 시작을 허용합니다.
        /// [EN] Permits combat only for the matching run currently loading.
        /// </summary>
        public bool TryEnterBattle(Guid runId)
        {
            if (!Matches(runId, Define.RunPhase.Loading))
                return false;

            Current = new RunState(runId, Current.Configuration, Define.RunPhase.Battle);
            return true;
        }

        /// <summary>
        /// 終了を一度だけ受け付け、正常終了なら結果をコピーして新しい出発をロックします。
        /// [KO] 종료를 한 번만 받고 정상 종료이면 결과를 복사한 뒤 새 출발을 잠급니다.
        /// [EN] Accepts cleanup once, copying a completed result and locking new departures.
        /// </summary>
        public bool TryBeginCleanup(Guid runId, Define.BattleResult outcome = Define.BattleResult.Ongoing, int ticks = 0)
        {
            if (Current == null || Current.Id != runId || Current.Phase == Define.RunPhase.CleaningUp)
                return false;

            if (outcome != Define.BattleResult.Ongoing)
            {
                if (Current.Phase != Define.RunPhase.Battle) return false;
                Result = new RunResult(runId, Current.Configuration, outcome, ticks);
            }

            Current = new RunState(runId, Current.Configuration, Define.RunPhase.CleaningUp);
            return true;
        }

        /// <summary>
        /// 資源所有者の片付け完了通知を受けてから、次の出発を許可します。
        /// [KO] 자원 소유자의 정리 완료 알림을 받은 뒤 다음 출발을 허용합니다.
        /// [EN] Permits another departure only after the resource owner reports cleanup complete.
        /// </summary>
        public bool TryCompleteCleanup(Guid runId)
        {
            if (!Matches(runId, Define.RunPhase.CleaningUp))
                return false;

            Current = null;
            return true;
        }

        /// <summary>
        /// 識別子と段階が現在の挑戦に一致するかを確認します。
        /// [KO] 식별자와 단계가 현재 도전과 일치하는지 확인합니다.
        /// [EN] Checks that the identifier and phase match the active run.
        /// </summary>
        private bool Matches(Guid runId, Define.RunPhase phase)
            => Current != null && Current.Id == runId && Current.Phase == phase;
    }
}
