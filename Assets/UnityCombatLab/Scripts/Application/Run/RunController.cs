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
        /// この Controller だけが変更する五ウェーブの進行。
        /// [KO] 이 Controller만 변경하는 다섯 웨이브 진행입니다.
        /// [EN] Five-wave progress changed only through this controller.
        /// </summary>
        public RunProgress Progress { get; private set; }

        /// <summary>
        /// 同じ Run 内で保持する一倍または二倍の速度。
        /// [KO] 같은 Run 안에서 유지하는 1배 또는 2배 속도입니다.
        /// [EN] One or two times speed retained within a run.
        /// </summary>
        public int Speed { get; private set; } = 1;

        /// <summary>
        /// 戦闘または開始待ちを止めるモーダル状態。
        /// [KO] 전투나 시작 대기를 멈추는 모달 상태입니다.
        /// [EN] Modal state stopping combat or its introduction.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// 一時停止より上に終了確認を表示しているか。
        /// [KO] 일시정지 위에 종료 확인을 표시하는지 나타냅니다.
        /// [EN] Whether an exit confirmation is displayed above pause.
        /// </summary>
        public bool IsExitConfirmationOpen { get; private set; }

        /// <summary>
        /// 操作または進行が変わったとき、Presenter に再表示を通知します。
        /// [KO] 조작·진행이 바뀌면 Presenter에 다시 표시하도록 알립니다.
        /// [EN] Notifies the presenter to refresh when controls or progress change.
        /// </summary>
        public event Action Changed;

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
            Progress = null;
            Speed = 1;
            IsPaused = false;
            IsExitConfirmationOpen = false;
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

            Current = new RunState(runId, Current.Configuration, Progress == null ? Define.RunPhase.Battle : Define.RunPhase.WaveIntro);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// ロード済み設定を受け、戦闘開始前に Run の進行と乱数を一度だけ作ります。
        /// [KO] 로드한 설정을 받아 전투 시작 전에 Run 진행과 난수를 한 번만 만듭니다.
        /// [EN] Receives loaded settings and initializes progress and random state once before combat.
        /// </summary>
        public void PrepareProgress(Guid runId, RunGameData data)
        {
            if (!Matches(runId, Define.RunPhase.Loading) || Progress != null)
                throw new InvalidOperationException("Run progress can be prepared once during loading.");

            Progress = new RunProgress(data, Current.Configuration.Seed);
        }

        /// <summary>
        /// 開始バナーが終わった現在のウェーブだけを戦闘へ移します。
        /// [KO] 시작 배너가 끝난 현재 웨이브만 전투로 전환합니다.
        /// [EN] Starts only the current wave after its introduction ends.
        /// </summary>
        public bool TryBeginWave(Guid runId, int wave)
        {
            if (!Matches(runId, Define.RunPhase.WaveIntro) || Progress?.Wave != wave || IsPaused)
                return false;

            SetPhase(Define.RunPhase.Battle);
            return true;
        }

        /// <summary>
        /// 終了した現在のウェーブを一度集計し、最終結果または中間クリアへ移します。
        /// [KO] 종료한 현재 웨이브를 한 번 집계하고 최종 결과 또는 중간 클리어로 전환합니다.
        /// [EN] Accounts for the finished current wave once and produces a final result or intermediate clear.
        /// </summary>
        public bool TryFinishWave(Guid runId, int wave, Define.BattleResult result, int ticks)
        {
            if (!Matches(runId, Define.RunPhase.Battle) || Progress == null || !Progress.TryCompleteWave(wave, result, ticks))
                return false;

            IsPaused = false;
            IsExitConfirmationOpen = false;
            if (Progress.IsFinished)
                Result = Progress.CreateResult(runId, Current.Configuration);

            SetPhase(Define.RunPhase.WaveClear);
            return true;
        }

        /// <summary>
        /// 中間クリアの表示後に、同じ候補を維持する報酬選択へ移します。
        /// [KO] 중간 클리어 표시 후 같은 후보를 유지하는 보상 선택으로 전환합니다.
        /// [EN] Opens reward selection with stable offers after intermediate-clear presentation.
        /// </summary>
        public bool TryOpenRewards(Guid runId)
        {
            if (!Matches(runId, Define.RunPhase.WaveClear) || Progress == null || Progress.IsFinished)
                return false;

            Progress.GetOffers();
            SetPhase(Define.RunPhase.RewardSelection);
            return true;
        }

        /// <summary>
        /// Run とウェーブ・候補を照合し、有効な一クリックだけを適用します。
        /// [KO] Run·웨이브·후보를 대조하고 유효한 한 번의 클릭만 적용합니다.
        /// [EN] Matches run, wave and offered kind and applies only one valid click.
        /// </summary>
        public bool TrySelectReward(Guid runId, int wave, Define.RewardKind? kind)
        {
            if (!Matches(runId, Define.RunPhase.RewardSelection) || !Progress.TryChoose(wave, kind))
                return false;

            SetPhase(Define.RunPhase.WaveIntro);
            return true;
        }

        /// <summary>
        /// 戦闘中だけ一倍と二倍を切り替えます。
        /// [KO] 전투 중에만 1배와 2배를 전환합니다.
        /// [EN] Toggles one and two times speed only during combat.
        /// </summary>
        public bool TryToggleSpeed(Guid runId)
        {
            if (!Matches(runId, Define.RunPhase.Battle) || IsPaused)
                return false;

            Speed = Speed == 1 ? 2 : 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 手動停止またはフォーカス喪失で戦闘を止め、復帰後も明示再開を待ちます。
        /// [KO] 수동 정지나 포커스 상실로 전투를 멈추고 복귀 후에도 명시적 재개를 기다립니다.
        /// [EN] Pauses on request or focus loss and waits for explicit resume after focus returns.
        /// </summary>
        public bool TryPause(Guid runId)
        {
            if (Current == null || Current.Id != runId ||
                (Current.Phase != Define.RunPhase.Battle && Current.Phase != Define.RunPhase.WaveIntro) || IsPaused)
            {
                return false;
            }

            IsPaused = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 終了確認を閉じた一時停止だけを再開します。
        /// [KO] 종료 확인을 닫은 일시정지만 재개합니다.
        /// [EN] Resumes pause only when no exit confirmation is open.
        /// </summary>
        public bool TryResume(Guid runId)
        {
            if (Current == null || Current.Id != runId || !IsPaused || IsExitConfirmationOpen)
                return false;

            IsPaused = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 一時停止中の終了確認だけを開閉します。
        /// [KO] 일시정지 중의 종료 확인만 열거나 닫습니다.
        /// [EN] Opens or closes exit confirmation only while paused.
        /// </summary>
        public bool TrySetExitConfirmation(Guid runId, bool isOpen)
        {
            if (Current == null || Current.Id != runId || !IsPaused || Current.Phase == Define.RunPhase.CleaningUp)
                return false;

            IsExitConfirmationOpen = isOpen;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// RunId と編成を保ったまま状態を変え、画面へ通知します。
        /// [KO] RunId·편성을 유지하며 상태를 바꾸고 화면에 알립니다.
        /// [EN] Changes phase while retaining RunId and formation, then notifies the screen.
        /// </summary>
        private void SetPhase(Define.RunPhase phase)
        {
            Current = new RunState(Current.Id, Current.Configuration, phase);
            Changed?.Invoke();
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
                if (Progress != null)
                {
                    if (Result == null || Result.Outcome != outcome || Result.Ticks != ticks)
                        return false;
                }
                else
                {
                    if (Current.Phase != Define.RunPhase.Battle)
                        return false;

                    Result = new RunResult(runId, Current.Configuration, outcome, ticks);
                }
            }

            else
            {
                Result = null;
            }

            Current = new RunState(runId, Current.Configuration, Define.RunPhase.CleaningUp);
            IsPaused = false;
            IsExitConfirmationOpen = false;
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
            Progress = null;
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
