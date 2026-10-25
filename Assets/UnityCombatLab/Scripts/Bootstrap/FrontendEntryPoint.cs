using UnityCombatLab.Application.Run;
using System;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Infrastructure.Balance;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.UI;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// Frontend の Presenter と共通 Run 調整役の間を接続し、シーン終了時に解除します。
    /// [KO] Frontend Presenter와 공통 Run 조정자를 연결하고 씬 종료 시 해제합니다.
    /// [EN] Connects the Frontend presenter to shared run coordination and detaches on scene exit.
    /// </summary>
    public sealed class FrontendEntryPoint : IStartable, IDisposable
    {
        /// <summary>
        /// シーンをまたいで保持する共通調整役。
        /// [KO] 씬을 넘어 유지하는 공통 조정자입니다.
        /// [EN] Shared coordinator retained across scenes.
        /// </summary>
        private readonly RunFlowCoordinator _flow;

        /// <summary>
        /// このシーンの View から入力を受ける Presenter。
        /// [KO] 이 씬의 View에서 입력을 받을 Presenter입니다.
        /// [EN] Presenter receiving input from this scene's view.
        /// </summary>
        private readonly FrontendPresenter _presenter;

        /// <summary>
        /// 二重解除を防ぎます。
        /// [KO] 중복 해제를 막습니다.
        /// [EN] Prevents duplicate disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 同じ EntryPoint に通知を重複接続しないための開始状態。
        /// [KO] 같은 EntryPoint에 알림을 중복 연결하지 않도록 하는 시작 상태입니다.
        /// [EN] Start state preventing duplicate notification connections on this entry point.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// 実データの英雄説明と共通色を画面へ渡し、Presenter を作ります。
        /// [KO] 실제 데이터 영웅 설명과 공통 색을 화면에 전달하고 Presenter를 만듭니다.
        /// [EN] Supplies real hero descriptions and shared colors and creates the presenter.
        /// </summary>
        public FrontendEntryPoint(RunFlowCoordinator flow, FrontendView view, UnitDefinition[] heroes, GameColorPalette colors)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            if (colors == null)
                throw new ArgumentNullException(nameof(colors));

            if (heroes == null || heroes.Length != Define.SlotsPerTeam)
                throw new ArgumentException("Frontend requires three hero definitions.", nameof(heroes));

            var descriptions = new string[Define.SlotsPerTeam];
            for (var index = 0; index < heroes.Length; index++)
            {
                if (heroes[index] == null)
                    throw new ArgumentException($"Frontend hero definition {index} is missing.", nameof(heroes));

                var input = heroes[index].CreateInput(index + 1, Define.BattleTeam.Ally, (Define.BattleSlot)index);
                if ((int)input.Role != index)
                    throw new InvalidOperationException("Hero definitions must be knight, ranger and mage.");

                descriptions[index] = GameText.HeroDescription(input);
            }
            view.Initialize();
            view.ApplyPalette(colors);
            _presenter = new FrontendPresenter(flow.Controller, view, descriptions);
        }

        /// <summary>
        /// 入力と画面変更の接続を開始し、現在のページを表示します。
        /// [KO] 입력과 화면 변경 연결을 시작하고 현재 페이지를 표시합니다.
        /// [EN] Connects input and screen updates and renders the current page.
        /// </summary>
        public void Start()
        {
            if (_isDisposed || _isStarted)
                return;

            try
            {
                _presenter.DepartureRequested += HandleDepartureRequested;
                _presenter.NavigationRequested += HandleNavigationRequested;
                _presenter.VolumeRequested += HandleVolumeRequested;
                _flow.Changed += HandleFlowChanged;
                HandleFlowChanged();
                _isStarted = true;
                _flow.MarkFrontendReady();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Presenter の出発要求を取消・エラー処理を持つ非同期境界へ渡します。
        /// [KO] Presenter 출발 요청을 취소·오류 처리가 있는 비동기 경계에 전달합니다.
        /// [EN] Forwards departure to the asynchronous boundary owning cancellation and errors.
        /// </summary>
        private void HandleDepartureRequested(RunConfiguration configuration)
            => _flow.StartRunAsync(configuration).Forget();
        /// <summary>
        /// 共通調整役へページ変更を要求します。
        /// [KO] 공통 조정자에게 페이지 변경을 요청합니다.
        /// [EN] Requests navigation from the shared coordinator.
        /// </summary>
        private void HandleNavigationRequested(Define.FrontendPage page)
            => _flow.Navigate(page);
        /// <summary>
        /// Run に依存しない音量を更新します。
        /// [KO] Run에 의존하지 않는 음량을 갱신합니다.
        /// [EN] Updates volume independently of a run.
        /// </summary>
        private void HandleVolumeRequested(float value)
            => _flow.SetVolume(value);
        /// <summary>
        /// 現在のページと結果スナップショットを Presenter に表示させます。
        /// [KO] 현재 페이지와 결과 스냅샷을 Presenter가 표시하도록 합니다.
        /// [EN] Asks the presenter to render the current page and result snapshot.
        /// </summary>
        private void HandleFlowChanged()
            => _presenter.Show(_flow.Page, _flow.IsBusy, GameText.Failure(_flow.Failure), _flow.Volume, _flow.CanRetry);

        /// <summary>
        /// 共通サービスに古い画面の接続を残さず、Presenter も解除します。
        /// [KO] 공통 서비스에 이전 화면 연결을 남기지 않고 Presenter도 해제합니다.
        /// [EN] Removes stale-screen connections from common services and disposes the presenter.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _flow.Changed -= HandleFlowChanged;
            _presenter.DepartureRequested -= HandleDepartureRequested;
            _presenter.NavigationRequested -= HandleNavigationRequested;
            _presenter.VolumeRequested -= HandleVolumeRequested;
            _presenter.Dispose();
        }
    }
}
