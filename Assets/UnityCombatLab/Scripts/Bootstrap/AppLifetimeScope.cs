using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// アプリ起動時の依存関係を組み立てます。
    /// [KO] 앱 시작 시 의존성을 구성합니다.
    /// [EN] Composes dependencies for application startup.
    /// </summary>
    public sealed class AppLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// シーンが所有する起動状態の表示先。
        /// [KO] 씬이 소유하는 시작 상태 표시 대상입니다.
        /// [EN] The startup label owned by the scene.
        /// </summary>
        [SerializeField] private Text _statusText;

        /// <summary>
        /// 表示先とアプリ起動処理を App Scope に登録します。
        /// [KO] 표시 대상과 앱 시작 처리를 App Scope에 등록합니다.
        /// [EN] Registers the startup label and entry point in the app scope.
        /// </summary>
        /// <param name="builder">
        /// VContainer が所有する App Scope の登録先。
        /// [KO] VContainer가 소유하는 App Scope의 등록 대상입니다.
        /// [EN] The app-scope registration builder owned by VContainer.
        /// </param>
        protected override void Configure(IContainerBuilder builder)
        {
            if (_statusText == null)
            {
                throw new System.InvalidOperationException("The startup label must be assigned in Boot.");
            }

            builder.RegisterComponent(_statusText);
            builder.RegisterEntryPoint<AppEntryPoint>().AsSelf();
        }
    }
}
