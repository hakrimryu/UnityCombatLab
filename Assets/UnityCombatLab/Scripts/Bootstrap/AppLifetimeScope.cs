using UnityCombatLab.Infrastructure.Scenes;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
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
        /// 重複した共通ルートを起動しないための生存インスタンス。
        /// [KO] 중복 공통 루트의 시작을 막는 생존 인스턴스입니다.
        /// [EN] The live instance used to prevent duplicate app roots.
        /// </summary>
        private static AppLifetimeScope _instance;

        /// <summary>
        /// AppRoot が所有する起動状態の表示先。
        /// [KO] AppRoot가 소유하는 시작 상태 표시 대상입니다.
        /// [EN] The startup label owned by AppRoot.
        /// </summary>
        [SerializeField] private Text _statusText;

        /// <summary>
        /// 全画面が共有する唯一の色アセット。子の Dungeon Scope も同じインスタンスを受け取ります。
        /// [KO] 모든 화면이 공유하는 색상 에셋 하나입니다. 자식 Dungeon Scope도 같은 인스턴스를 받습니다.
        /// [EN] The shared color asset also resolved by the child Dungeon scope.
        /// </summary>
        [SerializeField] private GameColorPalette _colors;

        /// <summary>
        /// 共通ルートだけをシーンの寿命から分離してから DI を起動します。
        /// [KO] 공통 루트만 씬 수명에서 분리한 뒤 DI를 시작합니다.
        /// [EN] Detaches only the common root from scene lifetime before starting DI.
        /// </summary>
        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            try
            {
                if (transform.parent != null)
                {
                    throw new System.InvalidOperationException("AppRoot must be a scene root.");
                }

                _instance = this;
                DontDestroyOnLoad(gameObject);
                base.Awake();
            }
            catch
            {
                // 破棄がフレーム末尾でも、正常な代替ルートの起動を妨げない。
                if (_instance == this)
                {
                    _instance = null;
                }

                gameObject.SetActive(false);
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// DI を破棄し、所有していたインスタンスだけを解除します。
        /// [KO] DI를 해제하고 자신이 소유한 인스턴스만 해제합니다.
        /// [EN] Disposes DI and clears the instance only when owned by this root.
        /// </summary>
        protected override void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            base.OnDestroy();
        }

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
            if (_colors == null)
            {
                throw new System.InvalidOperationException("The shared color palette must be assigned in Boot.");
            }

            // アセットは借用するだけで、AppRoot や戦闘の終了時に破棄しない。
            builder.RegisterInstance(_colors);
            _statusText.color = _colors.GetColor(Define.PaletteColor.OnBackground);
            builder.Register<SceneTransitionManager>(Lifetime.Singleton);
            builder.RegisterEntryPoint<AppEntryPoint>().AsSelf();
        }
    }
}
