using UnityCombatLab.Application.Run;
using System;
using System.Linq;
using UnityCombatLab.Infrastructure.Balance;
using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// Frontend が所有する UI と英雄データを、共通の Run 調整役へ接続します。
    /// [KO] Frontend가 소유하는 UI와 영웅 데이터를 공통 Run 조정자에 연결합니다.
    /// [EN] Connects Frontend-owned UI and hero data to the shared run coordinator.
    /// </summary>
    public sealed class FrontendLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// このシーン内の専用 Canvas にある画面。
        /// [KO] 이 씬의 전용 Canvas 안에 있는 화면입니다.
        /// [EN] View under this scene's dedicated canvas.
        /// </summary>
        [SerializeField] private FrontendView _view;

        /// <summary>
        /// 騎士・弓使い・魔法使いの実際の開始能力。
        /// [KO] 기사·궁수·마법사의 실제 시작 능력입니다.
        /// [EN] Actual starting definitions for knight, ranger and mage.
        /// </summary>
        [SerializeField] private UnitDefinition[] _heroes;

        /// <summary>
        /// 共通サービスの DI 親を Boot の AppRoot から取得します。
        /// [KO] 공통 서비스의 DI 부모를 Boot의 AppRoot에서 가져옵니다.
        /// [EN] Finds the common-services DI parent created by Boot.
        /// </summary>
        protected override LifetimeScope FindParent()
            => FindFirstObjectByType<AppLifetimeScope>();

        /// <summary>
        /// 画面のシーン所有を確認し、開始・入力接続・解除を登録します。
        /// [KO] 화면의 씬 소유권을 확인하고 시작·입력 연결·해제를 등록합니다.
        /// [EN] Validates scene ownership and registers startup, input wiring and disposal.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent == null || _view == null || _view.gameObject.scene != gameObject.scene ||
                _heroes == null || _heroes.Length != Define.SlotsPerTeam || _heroes.Any(hero => hero == null))
            {
                throw new InvalidOperationException("Frontend requires AppRoot, a scene-owned view and three hero definitions.");
            }

            builder.RegisterEntryPoint(resolver => new FrontendEntryPoint
            (resolver.Resolve<RunFlowCoordinator>(),
                _view,
                _heroes,
                resolver.Resolve<GameColorPalette>(),
                resolver.Resolve<AddressableAssetManager>(),
                resolver.Resolve<UiAssetCatalog>()), Lifetime.Scoped).AsSelf();
        }
    }
}
