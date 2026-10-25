using UnityCombatLab.Application.Run;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.UI;
using VContainer.Unity;
using UnityEngine;

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
        private FrontendPresenter _presenter;

        /// <summary>
        /// シーン内の表示と、その表示が終了するまで保持する共通素材。
        /// [KO] 씬 내부 표시와 표시가 종료할 때까지 유지할 공통 소재입니다.
        /// [EN] Scene view and shared assets retained until that view ends.
        /// </summary>
        private readonly FrontendView _view;
        /// <summary>
        /// 新しいカードにも適用する共通色。
        /// [KO] 새 카드에도 적용할 공통 색상입니다.
        /// [EN] Shared colors also applied to newly created cards.
        /// </summary>
        private readonly GameColorPalette _colors;
        /// <summary>
        /// カード・配置枠・職業画像の型付き参照。
        /// [KO] 카드·배치 슬롯·직업 이미지의 타입 지정 참조입니다.
        /// [EN] Typed references for cards, slots and role images.
        /// </summary>
        private readonly UiAssetCatalog _catalog;

        /// <summary>
        /// 戦闘と同じ能力設定を非同期で取得する職業カタログ。
        /// [KO] 전투와 같은 능력 설정을 비동기로 가져올 직업 카탈로그입니다.
        /// [EN] Role catalog used to load the same stats as battle asynchronously.
        /// </summary>
        private readonly UnitAssetCatalog _unitCatalog;
        /// <summary>
        /// このFrontendが生成した表示の破棄後に解放する素材。
        /// [KO] 이 Frontend에서 생성한 표시를 파괴한 후 해제할 소재입니다.
        /// [EN] Assets released after this Frontend's generated views are destroyed.
        /// </summary>
        private readonly AddressableAssetScope _assets;
        /// <summary>
        /// UnitDefinitionから作った職業名と実能力の説明。
        /// [KO] UnitDefinition으로 만든 직업 이름과 실제 능력 설명입니다.
        /// [EN] Role names and actual stat descriptions built from UnitDefinition.
        /// </summary>
        private readonly string[] _descriptions;

        /// <summary>
        /// シーン終了で読込結果の使用を取り消します。
        /// [KO] 씬 종료 시 로딩 결과 사용을 취소합니다.
        /// [EN] Cancels use of loading results when the scene ends.
        /// </summary>
        private readonly CancellationTokenSource _lifetime = new();

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
        /// 非同期のUI・能力設定の準備に使う依存関係を保持します。
        /// [KO] 비동기 UI·능력 설정 준비에 사용할 의존성을 보관합니다.
        /// [EN] Retains dependencies for asynchronous UI and stat preparation.
        /// </summary>
        public FrontendEntryPoint(RunFlowCoordinator flow, FrontendView view,
            GameColorPalette colors, AddressableAssetManager assets, UiAssetCatalog catalog, UnitAssetCatalog unitCatalog)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            if (colors == null)
                throw new ArgumentNullException(nameof(colors));

            _view = view;
            _colors = colors;
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _unitCatalog = unitCatalog != null ? unitCatalog : throw new ArgumentNullException(nameof(unitCatalog));
            _descriptions = new string[Define.SlotsPerTeam];

            _assets = (assets ?? throw new ArgumentNullException(nameof(assets))).CreateScope();
        }

        /// <summary>
        /// 共通切替処理にUI準備を登録し、素材が揃うまで入力を止めます。
        /// [KO] 공통 전환 처리에 UI 준비를 등록하고 소재가 준비될 때까지 입력을 멈춥니다.
        /// [EN] Registers UI preparation with shared transitions and disables input until assets are ready.
        /// </summary>
        public void Start()
        {
            if (_isDisposed || _isStarted)
                return;

            try
            {
                _view.Initialize();
                _view.SetInteractable(false);
                _flow.RegisterFrontendPreparation(PrepareAsync);
                _isStarted = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 共通prefabと職業画像を読み込み、生成・入力接続・初回表示が揃ってから準備完了を通知します。
        /// [KO] 공통 prefab과 직업 이미지를 로드하고 생성·입력 연결·첫 표시 후 준비 완료를 알립니다.
        /// [EN] Loads shared prefabs and role images and signals readiness after creation, input setup and first rendering.
        /// </summary>
        private async UniTask PrepareAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FrontendEntryPoint));

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            var token = linked.Token;
            try
            {
                var heroAsset = await _assets.LoadAsync(_catalog.HeroCard, token);
                var slotAsset = await _assets.LoadAsync(_catalog.FormationSlot, token);
                var heroPrefab = heroAsset.GetComponent<HeroCardView>();
                var slotPrefab = slotAsset.GetComponent<FormationSlotView>();
                if (heroPrefab == null || slotPrefab == null)
                    throw new InvalidOperationException("UI catalog prefabs require HeroCardView and FormationSlotView.");

                var heroes = new HeroCardData[Define.SlotsPerTeam];
                for (var index = 0; index < heroes.Length; index++)
                {
                    var role = (Define.UnitRole)index;
                    var definition = await _assets.LoadAsync(_unitCatalog.Get(role).Definition, token);
                    var input = definition.CreateInput(index + 1, Define.BattleTeam.Ally, (Define.BattleSlot)index);
                    if (input.Role != role)
                        throw new InvalidOperationException($"Hero definition does not match catalog role {role}.");

                    _descriptions[index] = GameText.HeroDescription(input);
                    var visual = _catalog.GetHero(role);
                    var icon = await _assets.LoadAsync(visual.Icon, token);
                    heroes[index] = new HeroCardData(role, _descriptions[index], icon, visual.Tint);
                }

                token.ThrowIfCancellationRequested();
                _view.CreateFormation(heroPrefab, slotPrefab, heroes, _colors);
                _view.ApplyPalette(_colors);
                _presenter = new FrontendPresenter(_flow.Controller, _view, _descriptions);
                _presenter.DepartureRequested += HandleDepartureRequested;
                _presenter.NavigationRequested += HandleNavigationRequested;
                _presenter.VolumeRequested += HandleVolumeRequested;
                _flow.Changed += HandleFlowChanged;
                HandleFlowChanged();
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
            _lifetime.Cancel();
            _flow.Changed -= HandleFlowChanged;
            if (_presenter != null)
            {
                _presenter.DepartureRequested -= HandleDepartureRequested;
                _presenter.NavigationRequested -= HandleNavigationRequested;
                _presenter.VolumeRequested -= HandleVolumeRequested;
                _presenter.Dispose();
            }

            if (_view != null)
                _view.DestroyFormation();

            ReleaseAssetsAsync().Forget(Debug.LogException);
            _lifetime.Dispose();
        }

        /// <summary>
        /// 子ViewのDestroyとシーン破棄が完了する次フレームに素材の所有権を解放します。
        /// [KO] 자식 View Destroy와 씬 파괴가 끝나는 다음 프레임에 소재 소유권을 해제합니다.
        /// [EN] Releases asset ownership on the frame after child-view and scene destruction complete.
        /// </summary>
        private async UniTask ReleaseAssetsAsync()
        {
            await UniTask.NextFrame();
            _assets.Dispose();
        }
    }
}
