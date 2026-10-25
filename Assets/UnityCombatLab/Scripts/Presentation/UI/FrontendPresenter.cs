using System;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.ReadModels;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 英雄選択と配置交換を処理し、出発や画面切替の要求を Bootstrap へ渡します。
    /// [KO] 영웅 선택과 배치 교환을 처리하고 출발·화면 전환 요청을 Bootstrap에 전달합니다.
    /// [EN] Handles hero selection and swaps, forwarding departure and navigation requests to Bootstrap.
    /// </summary>
    public sealed class FrontendPresenter : IDisposable
    {
        /// <summary>
        /// 最後の編成と実行中の挑戦を検査する Application の規則。
        /// [KO] 마지막 편성과 실행 중 도전을 검사할 Application 규칙입니다.
        /// [EN] Application rules inspecting remembered formation and active runs.
        /// </summary>
        private readonly RunController _controller;

        /// <summary>
        /// Frontend シーンから借りる表示先。
        /// [KO] Frontend 씬에서 빌리는 표시 대상입니다.
        /// [EN] View borrowed from the Frontend scene.
        /// </summary>
        private readonly IFrontendView _view;

        /// <summary>
        /// 実際の UnitDefinition から作った三人の説明。
        /// [KO] 실제 UnitDefinition으로 만든 세 영웅 설명입니다.
        /// [EN] Three hero descriptions built from real unit definitions.
        /// </summary>
        private readonly string[] _descriptions;

        /// <summary>
        /// 次に配置先を選ぶ英雄。未選択なら配置ボタンは変更しません。
        /// [KO] 다음 배치할 영웅이며 미선택이면 슬롯 버튼으로 변경하지 않습니다.
        /// [EN] Hero awaiting a destination; slot input makes no changes without a selection.
        /// </summary>
        private Define.UnitRole? _selected;

        /// <summary>
        /// 読込・終了処理中および解除後の入力を拒否します。
        /// [KO] 로딩·종료 처리 중과 해제 후 입력을 거부합니다.
        /// [EN] Rejects input during loading, cleanup and after disposal.
        /// </summary>
        private bool _isLocked;

        /// <summary>
        /// 解放後のShowで入力が再び有効にならないよう、Presenterの終了を記録します。
        /// [KO] 해제 후 Show로 입력이 다시 활성화되지 않도록 Presenter 종료를 기록합니다.
        /// [EN] Records presenter disposal so a late Show call cannot unlock input again.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 現在のエラーが新しい挑戦の再試行を許可するか。
        /// [KO] 현재 오류가 새 도전 재시도를 허용하는지 나타냅니다.
        /// [EN] Whether the current error permits retrying a fresh run.
        /// </summary>
        private bool _canRetry;

        /// <summary>
        /// 現在表示しているページ。
        /// [KO] 현재 표시 중인 페이지입니다.
        /// [EN] Currently displayed page.
        /// </summary>
        private Define.FrontendPage _page;

        /// <summary>
        /// 検証した編成での出発要求。
        /// [KO] 검증한 편성의 출발 요청입니다.
        /// [EN] Departure request using a validated configuration.
        /// </summary>
        public event Action<RunConfiguration> DepartureRequested;

        /// <summary>
        /// タイトル・編成への移動要求。
        /// [KO] 타이틀·편성 이동 요청입니다.
        /// [EN] Request to navigate to title or formation.
        /// </summary>
        public event Action<Define.FrontendPage> NavigationRequested;

        /// <summary>
        /// 共通音量の変更要求。
        /// [KO] 공통 음량 변경 요청입니다.
        /// [EN] Request to update shared volume.
        /// </summary>
        public event Action<float> VolumeRequested;

        /// <summary>
        /// View の入力を接続します。接続の解除はこの Presenter が担当します。
        /// [KO] View 입력을 연결하며 이 Presenter가 연결 해제를 담당합니다.
        /// [EN] Connects view input, with this presenter owning the corresponding unsubscription.
        /// </summary>
        public FrontendPresenter(RunController controller, IFrontendView view, string[] descriptions)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            if (descriptions == null || descriptions.Length != Define.SlotsPerTeam)
                throw new ArgumentException("Three descriptions required.");

            _descriptions = (string[])descriptions.Clone();
            _view.HeroSelected += HandleHeroSelected;
            _view.SlotSelected += HandleSlotSelected;
            _view.DifficultySelected += HandleDifficultySelected;
            _view.PageRequested += HandlePageRequested;
            _view.DepartureRequested += HandleDepartureRequested;
            _view.RetryRequested += HandleRetryRequested;
            _view.VolumeRequested += HandleVolumeRequested;
        }

        /// <summary>
        /// 調整側が確定したページと入力可否を表示します。
        /// [KO] 조정자가 확정한 페이지와 입력 가능 상태를 표시합니다.
        /// [EN] Renders the page and input availability committed by the coordinator.
        /// </summary>
        public void Show(Define.FrontendPage page, bool isBusy, string error, float volume, bool canRetry)
        {
            if (_isDisposed)
                return;

            if (page != _page)
                _selected = null;
            _page = page;
            _isLocked = isBusy;
            _canRetry = canRetry;
            _view.SetInteractable(!isBusy);
            var result = _controller.Result == null ? null : new RunResultDisplay(_controller.Result);
            _view.ShowPage(page, result, error);
            _view.SetVolume(volume);
            _view.SetErrorRetry(canRetry);
            RefreshFormation();
        }

        /// <summary>
        /// 次に配置する英雄を選びます。
        /// [KO] 다음 배치할 영웅을 선택합니다.
        /// [EN] Selects the next hero to place.
        /// </summary>
        private void HandleHeroSelected(Define.UnitRole role)
        {
            if (_isLocked || _page != Define.FrontendPage.Formation)
                return;

            _selected = role;
            RefreshFormation();
        }

        /// <summary>
        /// 選択済み英雄と配置先の英雄を交換し、選択を解除します。
        /// [KO] 선택한 영웅과 해당 슬롯 영웅을 교환하고 선택을 해제합니다.
        /// [EN] Swaps the selected hero with the destination hero and clears selection.
        /// </summary>
        private void HandleSlotSelected(Define.BattleSlot slot)
        {
            if (_isLocked || _page != Define.FrontendPage.Formation || !_selected.HasValue)
                return;

            _controller.RememberConfiguration(_controller.LastConfiguration.Swap(_selected.Value, slot));
            _selected = null;
            RefreshFormation();
        }

        /// <summary>
        /// 出発前の難易度を変更します。
        /// [KO] 출발 전 난이도를 변경합니다.
        /// [EN] Changes difficulty before departure.
        /// </summary>
        private void HandleDifficultySelected(Define.Difficulty difficulty)
        {
            if (_isLocked || _page != Define.FrontendPage.Formation)
                return;

            _controller.RememberConfiguration(_controller.LastConfiguration.WithDifficulty(difficulty));
            RefreshFormation();
        }

        /// <summary>
        /// 表示中の選択を解除して画面切替を要求します。
        /// [KO] 표시 중 선택을 해제하고 화면 전환을 요청합니다.
        /// [EN] Clears selection and requests navigation.
        /// </summary>
        private void HandlePageRequested(Define.FrontendPage page)
        {
            if (_isLocked)
                return;

            _selected = null;
            NavigationRequested?.Invoke(page);
        }

        /// <summary>
        /// 編成の最初の出発入力を直ちにロックします。
        /// [KO] 편성의 첫 출발 입력을 즉시 잠급니다.
        /// [EN] Immediately locks the first departure input from formation.
        /// </summary>
        private void HandleDepartureRequested()
        {
            if (_isLocked || _page != Define.FrontendPage.Formation)
                return;

            RequestDeparture();
        }

        /// <summary>
        /// 失敗した開始を、同じ編成で新しい挑戦として再要求します。
        /// [KO] 실패한 시작을 같은 편성의 새 도전으로 다시 요청합니다.
        /// [EN] Retries a failed start as a fresh run using the same formation.
        /// </summary>
        private void HandleRetryRequested()
        {
            if (_isLocked || _page != Define.FrontendPage.Error || !_canRetry)
                return;

            RequestDeparture();
        }

        /// <summary>
        /// 表示をロックして、固定済みの設定を一度渡します。
        /// [KO] 표시를 잠그고 확정된 설정을 한 번 전달합니다.
        /// [EN] Locks the view and forwards the committed configuration once.
        /// </summary>
        private void RequestDeparture()
        {
            _isLocked = true;
            _view.SetInteractable(false);
            DepartureRequested?.Invoke(_controller.LastConfiguration);
        }

        /// <summary>
        /// タイトルの音量入力を表示へ反映して共通所有者へ渡します。
        /// [KO] 타이틀 음량 입력을 표시에 반영하고 공통 소유자에 전달합니다.
        /// [EN] Updates title volume presentation and forwards it to the shared owner.
        /// </summary>
        private void HandleVolumeRequested(float value)
        {
            if (_isLocked)
                return;

            _view.SetVolume(value);
            VolumeRequested?.Invoke(value);
        }

        /// <summary>
        /// 最後に保存した編成と選択印を更新します。
        /// [KO] 마지막 저장 편성과 선택 표시를 갱신합니다.
        /// [EN] Refreshes remembered formation and selection marks.
        /// </summary>
        private void RefreshFormation()
            => _view.ShowFormation(new FormationDisplay(_controller.LastConfiguration, _selected, _descriptions));

        /// <summary>
        /// Frontend 終了時にすべての入力接続を解除します。
        /// [KO] Frontend 종료 시 모든 입력 연결을 해제합니다.
        /// [EN] Detaches all input subscriptions when Frontend ends.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isLocked = true;
            _view.HeroSelected -= HandleHeroSelected;
            _view.SlotSelected -= HandleSlotSelected;
            _view.DifficultySelected -= HandleDifficultySelected;
            _view.PageRequested -= HandlePageRequested;
            _view.DepartureRequested -= HandleDepartureRequested;
            _view.RetryRequested -= HandleRetryRequested;
            _view.VolumeRequested -= HandleVolumeRequested;
            DepartureRequested = null;
            NavigationRequested = null;
            VolumeRequested = null;
        }
    }
}
