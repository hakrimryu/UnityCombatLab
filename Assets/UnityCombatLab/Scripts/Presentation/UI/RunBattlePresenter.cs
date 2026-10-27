using System;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.ReadModels;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// Run の操作要求を Controller へ渡し、状態変更時だけ報酬とモーダルを更新します。
    /// [KO] Run 조작 요청을 Controller에 전달하고 상태 변경 시에만 보상·모달을 갱신합니다.
    /// [EN] Forwards run controls to the controller and updates offers and modals only on state changes.
    /// </summary>
    public sealed class RunBattlePresenter : IDisposable
    {

        /// <summary>
        /// 結果への復帰と共通音量を扱う Run の調整役。
        /// [KO] 결과 복귀·공통 음량을 처리하는 Run 조정자입니다.
        /// [EN] Run coordinator handling frontend return and shared volume.
        /// </summary>
        private readonly RunFlowCoordinator _flow;

        /// <summary>
        /// この Presenter が入力と表示を接続する画面。
        /// [KO] 이 Presenter가 입력·표시를 연결할 화면입니다.
        /// [EN] View whose input and display this presenter connects.
        /// </summary>
        private readonly RunBattleView _view;

        /// <summary>
        /// 実際の Runner の終了状態を読む操作可否の問い合わせ。
        /// [KO] 실제 Runner 종료 상태를 읽는 조작 가능 여부 조회입니다.
        /// [EN] Control eligibility query reading the actual runner's completion state.
        /// </summary>
        private readonly Func<bool> _canControlBattle;

        /// <summary>
        /// 操作変更を自分のユニットと効果の速度へ反映する呼び出し。
        /// [KO] 조작 변경을 자신의 유닛·효과 속도에 반영하는 호출입니다.
        /// [EN] Callback applying control changes to owned units and effects.
        /// </summary>
        private readonly Action _applyPlayback;

        /// <summary>
        /// 古い画面から別 Run を変更しないための対象 ID。
        /// [KO] 이전 화면에서 다른 Run을 바꾸지 않기 위한 대상 ID입니다.
        /// [EN] Target ID preventing stale views from modifying another run.
        /// </summary>
        private readonly Guid _runId;

        /// <summary>
        /// 同じ候補を再接続・再抽選しないための表示済みウェーブ。
        /// [KO] 같은 후보를 재연결·재추첨하지 않기 위한 표시한 웨이브입니다.
        /// [EN] Displayed wave preventing offer rebinding or redrawing.
        /// </summary>
        private int _offeredWave;

        /// <summary>
        /// 解除後の更新を拒否する状態。
        /// [KO] 해제 후 갱신을 거부하는 상태입니다.
        /// [EN] State rejecting updates after disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// Run の入力検査と状態変更を担当する Controller。
        /// [KO] Run 입력 검사·상태 변경을 담당하는 Controller입니다.
        /// [EN] Controller responsible for run input validation and state transitions.
        /// </summary>
        private RunController Controller => _flow.Controller;

        /// <summary>
        /// 画面入力と状態通知を接続します。Dispose が両側の購読を解除します。
        /// [KO] 화면 입력·상태 알림을 연결하며 Dispose가 양쪽 구독을 해제합니다.
        /// [EN] Connects view input and state notifications; Dispose detaches both sides.
        /// </summary>
        public RunBattlePresenter(RunFlowCoordinator flow, RunBattleView view, Guid runId,
            Func<bool> canControlBattle, Action applyPlayback)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _runId = runId;
            _canControlBattle = canControlBattle ?? throw new ArgumentNullException(nameof(canControlBattle));
            _applyPlayback = applyPlayback ?? throw new ArgumentNullException(nameof(applyPlayback));
            try
            {
                _view.Initialize();
                _view.SpeedRequested += HandleSpeedRequested;
                _view.PauseRequested += HandlePauseRequested;
                _view.ResumeRequested += HandleResumeRequested;
                _view.ExitRequested += HandleExitRequested;
                _view.CancelExitRequested += HandleCancelExitRequested;
                _view.ConfirmExitRequested += HandleConfirmExitRequested;
                _view.ContinueRequested += HandleContinueRequested;
                _view.RewardSelected += HandleRewardSelected;
                _view.EscapeRequested += HandleEscapeRequested;
                _view.FocusChanged += HandleFocusChanged;
                _view.VolumeRequested += HandleVolumeRequested;
                Controller.Changed += HandleChanged;
                Refresh();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 同じ Run の現在状態を表示し、報酬候補はウェーブにつき一度だけ接続します。
        /// [KO] 같은 Run의 현재 상태를 표시하고 보상 후보는 웨이브마다 한 번만 연결합니다.
        /// [EN] Displays this run's state and binds each wave's reward offers only once.
        /// </summary>
        public void Refresh()
        {
            var state = Controller.Current;
            var progress = Controller.Progress;
            if (_isDisposed || state == null || state.Id != _runId || progress == null)
                return;

            _view.ShowState(state.Phase, progress.Wave, Controller.Speed, Controller.IsPaused,
                Controller.IsExitConfirmationOpen, _canControlBattle(), progress.Outcome == Define.BattleResult.Victory && !progress.IsFinished);
            _view.ShowVolume(_flow.Volume);
            _view.ShowUpgrades(GameText.RewardSummary(progress.Rewards, false));
            if (state.Phase == Define.RunPhase.RewardSelection && _offeredWave != progress.Wave)
            {
                var offers = progress.GetOffers();
                var displays = new RewardCardDisplay[offers.Count];
                for (var index = 0; index < offers.Count; index++)
                    displays[index] = CreateRewardDisplay(progress.Wave, progress.Data.GetReward(offers[index]), progress.Rewards,
                        progress.Data.GetWave(progress.Wave).RewardStrengthBp);

                _view.ShowRewards(displays);
                _offeredWave = progress.Wave;
            }

            _applyPlayback();
        }

        /// <summary>
        /// 候補と確定済み強化から一枚分の文章と宝石数を作り、Run の選択履歴は変更しません。
        /// [KO] 후보와 확정한 강화로 카드 한 장의 문구·보석 수를 만들고 Run 선택 이력은 변경하지 않습니다.
        /// [EN] Prepares one card's text and gem count from an offer and committed upgrades without changing the run's choices.
        /// </summary>
        /// <param name="strengthBp">
        /// この候補を提示したウェーブの強化倍率。11000 は基本増加量の1.1倍です。
        /// [KO] 이 후보를 제시한 웨이브의 강화 배율이며 11000은 기본 증가량의 1.1배입니다.
        /// [EN] Reward strength of the offering wave; 11000 scales the base increment by 1.1.
        /// </param>
        public static RewardCardDisplay CreateRewardDisplay(int wave, RewardData reward, RunRewards current, int strengthBp)
        {
            if (reward == null)
                throw new ArgumentNullException(nameof(reward));

            if (current == null)
                throw new ArgumentNullException(nameof(current));

            var increase = reward.GetIncrease(strengthBp);
            var effectBefore = current.GetEffect(reward.Kind);
            var stacksBefore = current.GetStacks(reward.Kind);
            // 履歴の効果は選択時の倍率で確定済み。今回の倍率は新しい一回分にだけ適用する。
            return new RewardCardDisplay
            (wave,
                reward.Kind,
                GameText.RewardName(reward.Kind),
                $"+{GameText.Percent(increase)}",
                GameText.RewardEffect(reward.Kind, increase),
                $"{GameText.Percent(effectBefore)} → {GameText.Percent(effectBefore + increase)}\n" +
                $"重複：{stacksBefore} / {reward.MaxStacks} → {stacksBefore + 1} / {reward.MaxStacks}",
                stacksBefore + 1);
        }

        /// <summary>
        /// Controller の状態変更を受け、現在の画面を更新します。
        /// [KO] Controller 상태 변경을 받아 현재 화면을 갱신합니다.
        /// [EN] Refreshes the current screen when the controller reports a state change.
        /// </summary>
        private void HandleChanged()
            => Refresh();

        /// <summary>
        /// 未終了戦闘の速度切替だけを Controller へ渡します。
        /// [KO] 종료 전 전투의 속도 전환만 Controller에 전달합니다.
        /// [EN] Forwards speed changes only for unfinished combat.
        /// </summary>
        private void HandleSpeedRequested()
        {
            if (_view.HasFocus && _canControlBattle())
                Controller.TryToggleSpeed(_runId);
        }

        /// <summary>
        /// 未終了戦闘への停止要求を Controller へ渡します。
        /// [KO] 종료 전 전투의 정지 요청을 Controller에 전달합니다.
        /// [EN] Forwards pause requests for unfinished combat.
        /// </summary>
        private void HandlePauseRequested()
        {
            if (_canControlBattle())
                Controller.TryPause(_runId);
        }

        /// <summary>
        /// フォーカスがある場合だけ明示再開を要求します。
        /// [KO] 포커스가 있을 때만 명시적 재개를 요청합니다.
        /// [EN] Requests explicit resume only when focus is present.
        /// </summary>
        private void HandleResumeRequested()
        {
            if (_view.HasFocus)
                Controller.TryResume(_runId);
        }

        /// <summary>
        /// 一時停止上の終了確認を開きます。
        /// [KO] 일시정지 위에 종료 확인을 엽니다.
        /// [EN] Opens exit confirmation above pause.
        /// </summary>
        private void HandleExitRequested()
            => Controller.TrySetExitConfirmation(_runId, true);

        /// <summary>
        /// 確認を閉じ、一時停止へ戻します。
        /// [KO] 확인을 닫고 일시정지로 돌아갑니다.
        /// [EN] Closes confirmation and returns to pause.
        /// </summary>
        private void HandleCancelExitRequested()
            => Controller.TrySetExitConfirmation(_runId, false);

        /// <summary>
        /// 現在 Run の確認済み終了だけを調整役へ渡します。
        /// [KO] 현재 Run의 확인한 종료만 조정자에 전달합니다.
        /// [EN] Forwards only confirmed exit for the current run.
        /// </summary>
        private void HandleConfirmExitRequested()
        {
            if (Controller.Current?.Id == _runId && Controller.IsPaused && Controller.IsExitConfirmationOpen)
                _flow.RequestExitToTitle(_runId);
        }

        /// <summary>
        /// 提示候補がない現在ウェーブの続行を要求します。
        /// [KO] 제시 후보가 없는 현재 웨이브의 진행을 요청합니다.
        /// [EN] Requests continuation of a wave with no offered upgrades.
        /// </summary>
        private void HandleContinueRequested()
            => Controller.TrySelectReward(_runId, _offeredWave, null);

        /// <summary>
        /// カードに記録したウェーブと種類を Controller で検査します。
        /// [KO] 카드에 기록한 웨이브·종류를 Controller에서 검사합니다.
        /// [EN] Asks the controller to validate the wave and kind bound to a card.
        /// </summary>
        private void HandleRewardSelected(int wave, Define.RewardKind kind)
            => Controller.TrySelectReward(_runId, wave, kind);

        /// <summary>
        /// 最上位の確認・一時停止・戦闘にだけ Esc を適用します。
        /// [KO] 최상위 확인·일시정지·전투에만 Esc를 적용합니다.
        /// [EN] Routes Escape only to the topmost confirmation, pause or combat state.
        /// </summary>
        private void HandleEscapeRequested()
        {
            if (Controller.IsExitConfirmationOpen)
                HandleCancelExitRequested();
            else if (Controller.IsPaused)
                HandleResumeRequested();
            else
                HandlePauseRequested();
        }

        /// <summary>
        /// 戦闘と開始待ちだけを自動停止し、フォーカス復帰では再開しません。
        /// [KO] 전투·시작 대기만 자동 정지하고 포커스 복귀 시 재개하지 않습니다.
        /// [EN] Auto-pauses only combat and introduction, without auto-resuming on focus return.
        /// </summary>
        private void HandleFocusChanged()
        {
            if (!_view.HasFocus)
            {
                var phase = Controller.Current?.Phase;
                if (phase == Define.RunPhase.WaveIntro || _canControlBattle())
                    Controller.TryPause(_runId);
            }

            _applyPlayback();
        }

        /// <summary>
        /// 共通音量を変更し、通知を再発生させずに値を表示します。
        /// [KO] 공통 음량을 바꾸고 알림을 다시 발생시키지 않고 값을 표시합니다.
        /// [EN] Updates shared volume and displays it without firing another input event.
        /// </summary>
        private void HandleVolumeRequested(float value)
        {
            _flow.SetVolume(value);
            _view.ShowVolume(_flow.Volume);
        }

        /// <summary>
        /// 画面と Controller の自分の購読だけを解除します。
        /// [KO] 화면·Controller에서 자신의 구독만 해제합니다.
        /// [EN] Detaches only this presenter's subscriptions from the view and controller.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Controller.Changed -= HandleChanged;
            _view.SpeedRequested -= HandleSpeedRequested;
            _view.PauseRequested -= HandlePauseRequested;
            _view.ResumeRequested -= HandleResumeRequested;
            _view.ExitRequested -= HandleExitRequested;
            _view.CancelExitRequested -= HandleCancelExitRequested;
            _view.ConfirmExitRequested -= HandleConfirmExitRequested;
            _view.ContinueRequested -= HandleContinueRequested;
            _view.RewardSelected -= HandleRewardSelected;
            _view.EscapeRequested -= HandleEscapeRequested;
            _view.FocusChanged -= HandleFocusChanged;
            _view.VolumeRequested -= HandleVolumeRequested;
        }
    }
}
