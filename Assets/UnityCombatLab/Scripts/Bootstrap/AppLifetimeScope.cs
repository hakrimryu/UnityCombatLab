using Cysharp.Threading.Tasks;
using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Infrastructure.Scenes;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Infrastructure.Balance;
using UnityEngine;
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
        /// 現在の AppRoot。重複して生成されたルートの検出に使います。
        /// [KO] 현재 AppRoot이며 중복 생성된 루트를 감지하는 데 사용합니다.
        /// [EN] Current AppRoot, used to detect duplicate roots.
        /// </summary>
        private static AppLifetimeScope _instance;

        /// <summary>
        /// 全画面が共有する唯一の色アセット。子の Dungeon Scope も同じインスタンスを受け取ります。
        /// [KO] 모든 화면이 공유하는 색상 에셋 하나입니다. 자식 Dungeon Scope도 같은 인스턴스를 받습니다.
        /// [EN] The shared color asset also resolved by the child Dungeon scope.
        /// </summary>
        [SerializeField] private GameColorPalette _colors;

        /// <summary>
        /// シーンをまたぐ読込・片付け・復帰失敗を案内する共通表示。
        /// [KO] 씬을 넘나드는 로딩·정리·복귀 실패를 안내하는 공통 표시입니다.
        /// [EN] Shared display for loading, cleanup and scene-return failures.
        /// </summary>
        [SerializeField] private LoadingView _loadingPrefab;

        /// <summary>
        /// ローカルUI資産の型付き参照カタログ。
        /// [KO] 로컬 UI 자산의 타입 지정 참조 카탈로그입니다.
        /// [EN] Typed reference catalog for local UI assets.
        /// </summary>
        [SerializeField] private UiAssetCatalog _uiAssets;

        /// <summary>
        /// 編成と戦闘が共有するローカルユニット参照カタログ。
        /// [KO] 편성과 전투에서 공유하는 로컬 유닛 참조 카탈로그입니다.
        /// [EN] Local unit reference catalog shared by formation and battle.
        /// </summary>
        [SerializeField] private UnitAssetCatalog _unitAssets;

        /// <summary>
        /// 各 Run の開始時に固定する五ウェーブ・六強化の設定。
        /// [KO] 각 Run 시작 시 고정하는 다섯 웨이브·여섯 강화 설정입니다.
        /// [EN] Five-wave and six-upgrade settings frozen at each run start.
        /// </summary>
        [SerializeField] private RunDefinition _runDefinition;

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
        /// 共通パレット・シーン切替・アプリ起動処理を App Scope に登録します。
        /// [KO] 공통 팔레트·씬 전환·앱 시작 처리를 App Scope에 등록합니다.
        /// [EN] Registers shared colors, scene transitions and startup in the app scope.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            if (_colors == null)
            {
                throw new System.InvalidOperationException("The shared color palette must be assigned in Boot.");
            }

            // Boot で指定した色アセットを App Scope と子の Dungeon Scope で共有する。
            builder.RegisterInstance(_colors);
            builder.Register<SceneTransitionManager>(Lifetime.Singleton);
            if (_loadingPrefab == null || _uiAssets == null || _unitAssets == null || _runDefinition == null)
                throw new System.InvalidOperationException("Boot requires the shared loading prefab and UI/unit asset catalogs.");

            builder.RegisterInstance(_uiAssets);
            builder.RegisterInstance(_unitAssets);
            builder.RegisterInstance(_runDefinition);
            builder.Register<AddressableAssetManager>(Lifetime.Singleton);

            // 共通調整役だけを App に置き、画面とユニットは各シーンに残す。
            var loading = Instantiate(_loadingPrefab, transform);
            loading.name = "UI_LoadingView";
            loading.Validate();
            builder.Register(resolver => new RunFlowCoordinator
            (resolver.Resolve<SceneTransitionManager>(),
                loading,
                configuration => new RunAssetScope(resolver.Resolve<AddressableAssetManager>(), _uiAssets, _unitAssets, _runDefinition, configuration),
                () => UniTask.NextFrame(),
                Debug.LogException,
                value => AudioListener.volume = value), Lifetime.Singleton);
            builder.RegisterEntryPoint<AppEntryPoint>().AsSelf();
        }
    }
}
