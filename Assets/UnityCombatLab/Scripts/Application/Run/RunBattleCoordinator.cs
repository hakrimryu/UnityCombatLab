using System;
using System.Collections.Generic;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Application.Run
{
    /// <summary>
    /// 渡された実時間とフォーカスでウェーブを進め、表示完了後に結果と再現記録をセッションへ渡します。
    /// [KO] 전달받은 실제 시간·포커스로 웨이브를 진행하고 표시 완료 후 결과·재현 기록을 세션에 전달합니다.
    /// [EN] Advances waves from supplied real time and focus, then queues results and replay data after presentation completes.
    /// </summary>
    public sealed class RunBattleCoordinator : IRunBattle
    {
        /// <summary>
        /// 開始バナーの実時間秒。戦闘 tick には含めません。
        /// [KO] 시작 배너의 실제 초이며 전투 tick에는 포함하지 않습니다.
        /// [EN] Real introduction seconds, excluded from battle ticks.
        /// </summary>
        private const float IntroSeconds = 0.6f;

        /// <summary>
        /// 中間クリア案内の実時間秒。
        /// [KO] 중간 클리어 안내의 실제 초입니다.
        /// [EN] Real seconds of intermediate-clear guidance.
        /// </summary>
        private const float ClearSeconds = 0.5f;

        /// <summary>
        /// ウェーブ・報酬・停止状態を変更する所有者。
        /// [KO] 웨이브·보상·정지 상태를 변경할 소유자입니다.
        /// [EN] Owner of wave, reward and pause state changes.
        /// </summary>
        private readonly RunController _controller;

        /// <summary>
        /// この進行処理が結果と失敗を返す Run。
        /// [KO] 이 진행 처리가 결과·실패를 반환할 Run입니다.
        /// [EN] Run receiving results and failures from this coordinator.
        /// </summary>
        private readonly RunSession _session;

        /// <summary>
        /// ウェーブ番号・開始ユニット・強化から、未開始の戦闘とその Runner の所有者を作ります。
        /// [KO] 웨이브 번호·시작 유닛·강화로 미시작 전투와 그 Runner의 소유자를 만듭니다.
        /// [EN] Creates an unstarted battle owning its runner from a wave number, starting units and upgrades.
        /// </summary>
        private readonly Func<int, IReadOnlyList<BattleUnitInput>, RunRewards, IWaveBattle> _createBattle;

        /// <summary>
        /// Runner の再利用バッファから値として複写した全ウェーブのイベント。
        /// [KO] Runner 재사용 버퍼에서 값으로 복사한 전체 웨이브 이벤트입니다.
        /// [EN] Events copied by value from runner buffers across all waves.
        /// </summary>
        private readonly List<BattleEvent> _events = new();

        /// <summary>
        /// 次のウェーブ生成前、または終了時に解放する現在の戦闘。
        /// [KO] 다음 웨이브 생성 전 또는 종료 시 해제할 현재 전투입니다.
        /// [EN] Current battle released before creating another wave or on shutdown.
        /// </summary>
        private IWaveBattle _battle;

        /// <summary>
        /// 現在生成済みのウェーブ番号。
        /// [KO] 현재 생성한 웨이브 번호입니다.
        /// [EN] Number of the currently created wave.
        /// </summary>
        private int _wave;

        /// <summary>
        /// 秒表示を更新した最後の秒。
        /// [KO] 초 표시를 갱신한 마지막 초입니다.
        /// [EN] Last second sent to the clock display.
        /// </summary>
        private int _lastClockSecond;

        /// <summary>
        /// 開始・クリア案内に残った実時間秒。
        /// [KO] 시작·클리어 안내에 남은 실제 초입니다.
        /// [EN] Real seconds remaining for introduction or clear guidance.
        /// </summary>
        private float _remainingBannerSeconds;

        /// <summary>
        /// 重複開始と開始前の更新を防ぎます。
        /// [KO] 중복 시작·시작 전 갱신을 막습니다.
        /// [EN] Prevents duplicate starts and updates before startup.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// 勝敗確定直後の操作ボタン更新に使う前回状態。
        /// [KO] 승패 확정 직후 조작 버튼 갱신에 사용하는 이전 상태입니다.
        /// [EN] Previous eligibility used to refresh controls when an outcome is committed.
        /// </summary>
        private bool _wereControlsEnabled;

        /// <summary>
        /// フォーカス復帰後の最初の経過時間を破棄する指定。
        /// [KO] 포커스 복귀 후 첫 경과 시간을 버리는 지정입니다.
        /// [EN] Flag dropping the first elapsed interval after focus returns.
        /// </summary>
        private bool _shouldSkipDelta;

        /// <summary>
        /// 破棄後の開始・更新・入力を拒否する状態。
        /// [KO] 파괴 후 시작·갱신·입력을 거부하는 상태입니다.
        /// [EN] State rejecting startup, updates and input after disposal.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 別 Run への遅れた更新と、取消後の更新を拒否します。
        /// [KO] 다른 Run에 대한 늦은 갱신과 취소 후 갱신을 거부합니다.
        /// [EN] Rejects late updates against a different run and updates after cancellation.
        /// </summary>
        private bool CanUpdate => !IsDisposed && _session.IsBattleAllowed &&
                                  _controller.Current?.Id == _session.InitialState.Id && _session.Failure == null;

        /// <summary>
        /// 現在 Run の未終了戦闘だけが速度・停止操作を受け付けるか。
        /// [KO] 현재 Run의 종료 전 전투만 배속·정지 조작을 받을 수 있는지 나타냅니다.
        /// [EN] Whether this run's unfinished battle accepts speed and pause controls.
        /// </summary>
        public bool CanControlBattle => CanUpdate && _controller.Current.Phase == Define.RunPhase.Battle &&
                                        _battle != null && _battle.Runner.Result == Define.BattleResult.Ongoing;

        /// <summary>
        /// ウェーブ交換または操作可否変更を、接続された画面へ知らせます。
        /// [KO] 웨이브 교체 또는 조작 가능 여부 변경을 연결한 화면에 알립니다.
        /// [EN] Notifies the connected screen of wave replacement or control eligibility changes.
        /// </summary>
        public event Action BattleChanged;

        /// <summary>
        /// 表示秒が変わったとき、計算済み tick を時計へ渡します。
        /// [KO] 표시 초가 바뀌면 계산한 tick을 시계에 전달합니다.
        /// [EN] Sends committed ticks to the clock when its displayed second changes.
        /// </summary>
        public event Action<int> ClockChanged;

        /// <summary>
        /// Run の状態と戦闘生成処理を受け取り、生成された各戦闘の終了を管理します。
        /// [KO] Run 상태·전투 생성 처리를 받아 생성한 각 전투의 종료를 관리합니다.
        /// [EN] Receives run state and a battle factory and manages each created battle's lifetime.
        /// </summary>
        public RunBattleCoordinator(RunController controller, RunSession session,
            Func<int, IReadOnlyList<BattleUnitInput>, RunRewards, IWaveBattle> createBattle)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _createBattle = createBattle ?? throw new ArgumentNullException(nameof(createBattle));
        }

        /// <summary>
        /// ロード側が許可した Run の最初の戦闘を一度だけ作ります。
        /// [KO] 로딩 측에서 허용한 Run의 첫 전투를 한 번만 만듭니다.
        /// [EN] Creates the first battle once after loading permits this run to start.
        /// </summary>
        public void Start()
        {
            if (_isStarted || !CanUpdate)
                return;

            try
            {
                CreateWave();
                _isStarted = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 前の戦闘を解放してから、確定強化で全回復した開始ユニットを生成側へ渡します。
        /// [KO] 이전 전투를 해제한 뒤 확정 강화로 완전히 회복한 시작 유닛을 생성 측에 전달합니다.
        /// [EN] Releases the previous battle before passing fully healed, upgraded starting units to the factory.
        /// </summary>
        private void CreateWave()
        {
            ReleaseBattle();
            var progress = _controller.Progress ?? throw new InvalidOperationException("Run progress must be prepared before combat.");
            var inputs = progress.Data.CreateUnits(_session.InitialState.Configuration, progress.Wave, progress.Rewards);
            _wave = progress.Wave;
            _battle = _createBattle(_wave, inputs, progress.Rewards) ?? throw new InvalidOperationException("The wave factory returned no battle.");
            _battle.Runner.EventOccurred += HandleEventOccurred;
            _battle.Start();
            if (!CanUpdate)
                return;

            _battle.SetPlaybackSpeed(0);
            _lastClockSecond = 0;
            _remainingBannerSeconds = IntroSeconds;
            _wereControlsEnabled = false;
            ClockChanged?.Invoke(0);
            if (CanUpdate)
                BattleChanged?.Invoke();
        }

        /// <summary>
        /// 同期通知のイベントを値として再現記録へ保存します。
        /// [KO] 동기 알림 이벤트를 값으로 재현 기록에 저장합니다.
        /// [EN] Copies synchronously delivered events by value into the replay log.
        /// </summary>
        private void HandleEventOccurred(BattleEvent item)
            => _events.Add(item);

        /// <summary>
        /// 復帰フレームの経過時間を除外し、フォーカス状態を表示速度へ直ちに反映します。
        /// [KO] 복귀 프레임 경과 시간을 제외하고 포커스 상태를 표시 속도에 즉시 반영합니다.
        /// [EN] Excludes the return-frame delta and immediately applies focus to presentation speed.
        /// </summary>
        public void NotifyFocusChanged(bool hasFocus)
        {
            if (IsDisposed)
                return;

            _shouldSkipDelta = true;
            ApplyPlayback(hasFocus);
        }

        /// <summary>
        /// 停止・フォーカス・段階から現在の戦闘表示だけの速度を選びます。
        /// [KO] 정지·포커스·단계로 현재 전투 표시의 속도만 선택합니다.
        /// [EN] Selects playback speed for the current battle from pause, focus and phase.
        /// </summary>
        public void ApplyPlayback(bool hasFocus)
        {
            var canAdvance = CanUpdate && hasFocus && !_controller.IsPaused &&
                             _controller.Current.Phase == Define.RunPhase.Battle;
            _battle?.SetPlaybackSpeed(canAdvance ? _controller.Speed : 0);
        }

        /// <summary>
        /// 実経過秒で案内を進め、選択中や停止中は蓄積せず、戦闘だけに選択速度を掛けます。
        /// [KO] 실제 경과 초로 안내를 진행하고 선택·정지 중에는 누적하지 않으며 전투에만 선택 배속을 곱합니다.
        /// [EN] Advances banners in real seconds, accumulates nothing while choosing or paused, and scales only combat time.
        /// </summary>
        public void Advance(float unscaledDeltaSeconds, bool hasFocus)
        {
            if (!_isStarted || !CanUpdate || _session.PendingResult != null || _session.Failure != null)
                return;

            try
            {
                if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0)
                    throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));

                var phase = _controller.Current.Phase;
                if (phase == Define.RunPhase.WaveIntro && _wave != _controller.Progress.Wave)
                    CreateWave();

                if (!CanUpdate)
                    return;

                if (!hasFocus && (phase == Define.RunPhase.WaveIntro || CanControlBattle))
                    _controller.TryPause(_session.InitialState.Id);

                ApplyPlayback(hasFocus);
                if (!hasFocus || _controller.IsPaused)
                    return;

                if (_shouldSkipDelta)
                {
                    _shouldSkipDelta = false;
                    return;
                }

                if (phase == Define.RunPhase.WaveIntro || phase == Define.RunPhase.WaveClear)
                {
                    _remainingBannerSeconds -= unscaledDeltaSeconds;
                    if (_remainingBannerSeconds <= 0)
                    {
                        if (phase == Define.RunPhase.WaveIntro)
                            _controller.TryBeginWave(_session.InitialState.Id, _wave);
                        else
                            _controller.TryOpenRewards(_session.InitialState.Id);
                    }

                    return;
                }

                if (phase != Define.RunPhase.Battle)
                    return;

                _battle.Advance(unscaledDeltaSeconds * _controller.Speed);
                if (!CanUpdate)
                    return;

                // 20 tick が1秒。警告開始の75秒も同じ秒境界なので、秒が変わる時だけ通知する。
                var clockSecond = _battle.Runner.CurrentTick / Define.TicksPerSecond;
                if (_lastClockSecond != clockSecond)
                {
                    _lastClockSecond = clockSecond;
                    ClockChanged?.Invoke(_battle.Runner.CurrentTick);
                }

                if (!CanUpdate)
                    return;

                var controlsEnabled = CanControlBattle;
                if (_wereControlsEnabled != controlsEnabled)
                {
                    _wereControlsEnabled = controlsEnabled;
                    BattleChanged?.Invoke();
                }

                if (!CanUpdate || !_battle.IsPresentationComplete)
                    return;

                if (!_controller.TryFinishWave(_session.InitialState.Id, _wave, _battle.Runner.Result, _battle.Runner.CurrentTick))
                    throw new InvalidOperationException("The current wave completion was rejected.");

                if (!CanUpdate)
                    return;

                if (_controller.Progress.IsFinished)
                {
                    var result = _controller.Result;
                    _session.QueueResult(result, new RunReplayRecord(_controller.Progress.Data, result, _events));
                }
                else
                {
                    _remainingBannerSeconds = ClearSeconds;
                }
            }
            catch (Exception exception)
            {
                try
                {
                    Dispose();
                }
                finally
                {
                    _session.QueueFailure(exception);
                }
            }
        }

        /// <summary>
        /// 再現記録への接続を外し、Runner を含む現在の戦闘を所有者に解放させます。
        /// [KO] 재현 기록 연결을 해제하고 현재 전투 소유자가 Runner까지 해제하도록 합니다.
        /// [EN] Detaches replay recording and asks the current battle owner to release itself and its runner.
        /// </summary>
        private void ReleaseBattle()
        {
            var battle = _battle;
            _battle = null;
            if (battle == null)
                return;

            battle.Runner.EventOccurred -= HandleEventOccurred;
            battle.Dispose();
        }

        /// <summary>
        /// 戦闘と自分の通知を解放します。Run 資産の解放はセッションの終了処理に任せます。
        /// [KO] 전투·자신의 알림을 해제하고 Run 자산 해제는 세션 종료 처리에 맡깁니다.
        /// [EN] Releases combat and owned notifications, leaving run assets to session cleanup.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            try
            {
                ReleaseBattle();
            }
            finally
            {
                _events.Clear();
                BattleChanged = null;
                ClockChanged = null;
            }
        }
    }
}
