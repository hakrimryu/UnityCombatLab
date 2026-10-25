using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Scenes;
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityCombatLab.Bootstrap;
using UnityCombatLab.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace UnityCombatLab.Tests.PlayMode
{
    /// <summary>
    /// 共通ルートの永続化と Single シーン切替を検証します。
    /// [KO] 공통 루트의 영속화와 Single 씬 전환을 검증합니다.
    /// [EN] Verifies persistent app ownership and Single scene transitions.
    /// </summary>
    public sealed class BootSceneTests
    {
        /// <summary>
        /// 各テストを独立した Boot 起動から始めます。
        /// [KO] 각 테스트를 독립된 Boot 시작 상태에서 시작합니다.
        /// [EN] Starts each test from an independent Boot launch.
        /// </summary>
        [UnitySetUp]
        public IEnumerator StartBoot()
        {
            foreach (var scope in Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None))
            {
                Object.Destroy(scope.gameObject);
            }

            yield return null;
            yield return SceneManager.LoadSceneAsync((int)Define.SceneType.Boot, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
                if (scope != null && scope.Container != null && scope.Container.Resolve<AppEntryPoint>().IsStarted)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Boot did not finish the initial scene transition.");
        }

        /// <summary>
        /// テスト所有の共通ルートを破棄します。
        /// [KO] 테스트 소유 공통 루트를 해제합니다.
        /// [EN] Destroys the app root owned by the test.
        /// </summary>
        [UnityTearDown]
        public IEnumerator DisposeAppRoot()
        {
            foreach (var scope in Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None))
            {
                Object.Destroy(scope.gameObject);
            }

            yield return null;
        }

        /// <summary>
        /// Boot が消え、共通サービスだけが AppRoot に残ることを検証します。
        /// [KO] Boot가 사라지고 공통 서비스만 AppRoot에 남는지 검증합니다.
        /// [EN] Verifies that Boot unloads while app services remain under AppRoot.
        /// </summary>
        [UnityTest]
        public IEnumerator BootReplacesSceneAndPreservesCommonRoot()
        {
            var scopes = Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None);
            Assert.That(scopes.Length, Is.EqualTo(1));
            var root = scopes[0].gameObject;
            Assert.That(root.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(root.scene.GetRootGameObjects(), Does.Contain(root));
            Assert.That(root.transform.parent, Is.Null);
            Assert.That(SceneManager.GetSceneByBuildIndex((int)Define.SceneType.Boot).isLoaded, Is.False);
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo((int)Define.SceneType.Dungeon));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(Object.FindFirstObjectByType<BattleHudView>().gameObject.scene,
                Is.EqualTo(SceneManager.GetActiveScene()));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield break;
        }

        /// <summary>
        /// 再読込で舞台だけが置き換わり、同じ App Scope が残ることを検証します。
        /// [KO] 재로드 시 무대만 교체되고 같은 App Scope가 유지되는지 검증합니다.
        /// [EN] Verifies stage replacement while retaining the same app scope.
        /// </summary>
        [UnityTest]
        public IEnumerator DungeonReloadPreservesAppAndReplacesStage()
        {
            var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
            var knight = GameObject.Find("Unit_Knight");
            Assert.That(knight, Is.Not.Null);
            var transition = scope.Container.Resolve<SceneTransitionManager>();
            yield return transition.LoadSceneAsync(Define.SceneType.Dungeon).ToCoroutine();
            Assert.That(knight == null, Is.True);
            Assert.That(GameObject.Find("Unit_Knight"), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(scope));
            Assert.That(transition.IsLoading, Is.False);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Boot を再読込しても共通ルートが重複せず、Boot 専用 Canvas が作られないことを検証します。
        /// [KO] Boot를 다시 로드해도 공통 루트가 중복되지 않고 Boot 전용 Canvas가 생성되지 않는지 검증합니다.
        /// [EN] Verifies reloading Boot neither duplicates the shared root nor creates a Boot-only canvas.
        /// </summary>
        [UnityTest]
        public IEnumerator BootIndexLoadsWithoutDuplicatingAppRoot()
        {
            var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
            var transition = scope.Container.Resolve<SceneTransitionManager>();
            yield return transition.LoadSceneAsync(Define.SceneType.Boot).ToCoroutine();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo((int)Define.SceneType.Boot));
            Assert.That(SceneManager.GetSceneByBuildIndex((int)Define.SceneType.Dungeon).isLoaded, Is.False);
            Assert.That(Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(scope));
            Assert.That(transition.IsLoading, Is.False);
            foreach (var bootRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Assert.That(bootRoot.GetComponentsInChildren<Canvas>(true), Is.Empty);
            }
        }

        /// <summary>
        /// 重複ルートが追加の DI とサービスを残さないことを検証します。
        /// [KO] 중복 루트가 추가 DI와 서비스를 남기지 않는지 검증합니다.
        /// [EN] Verifies that a duplicate root leaves no extra DI or services.
        /// </summary>
        [UnityTest]
        public IEnumerator DuplicateAppRootIsDiscarded()
        {
            var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
            var duplicate = Object.Instantiate(scope.gameObject);
            yield return null;
            Assert.That(duplicate == null, Is.True);
            Assert.That(Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(scope));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        /// <summary>
        /// 初期化に失敗したルートが同一フレームの正常な再作成を妨げないことを検証します。
        /// [KO] 초기화 실패 루트가 같은 프레임의 정상 재생성을 막지 않는지 검증합니다.
        /// [EN] Verifies a failed root does not block a valid replacement in the same frame.
        /// </summary>
        [UnityTest]
        public IEnumerator FailedInitializationAllowsReplacementRoot()
        {
            var original = Object.FindFirstObjectByType<AppLifetimeScope>();
            original.gameObject.SetActive(false);
            var replacement = Object.Instantiate(original.gameObject);
            GameObject failedRoot = null;
            try
            {
                Object.Destroy(original.gameObject);
                yield return null;

                failedRoot = new GameObject("FailedAppRoot");
                failedRoot.SetActive(false);
                var failedScope = failedRoot.AddComponent<AppLifetimeScope>();
                LogAssert.Expect(LogType.Exception,
                    new System.Text.RegularExpressions.Regex("The shared color palette must be assigned in Boot"));
                failedRoot.SetActive(true);
                Assert.That(failedRoot.activeSelf, Is.False);
                Assert.That(failedScope.Container, Is.Null);

                // 失敗したルートの遅延破棄より先に、置換ルートを起動する。
                replacement.SetActive(true);
                var replacementScope = replacement.GetComponent<AppLifetimeScope>();
                var deadline = Time.realtimeSinceStartup + 10f;
                while (replacement != null && Time.realtimeSinceStartup < deadline)
                {
                    if (replacementScope.Container != null &&
                        replacementScope.Container.Resolve<AppEntryPoint>().IsStarted)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(failedRoot == null, Is.True);
                Assert.That(replacement != null, Is.True);
                Assert.That(replacementScope.Container, Is.Not.Null);
                Assert.That(replacementScope.Container.Resolve<AppEntryPoint>().IsStarted, Is.True);
                Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(replacementScope));
                Assert.That(Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
            finally
            {
                if (failedRoot != null) Object.Destroy(failedRoot);
                if (replacement != null) Object.Destroy(replacement);
            }
        }

        /// <summary>
        /// 取消済みの起動要求が現在の戦闘 HUD とシーンを変更しないことを検証します。
        /// [KO] 이미 취소된 시작 요청이 현재 전투 HUD와 씬을 변경하지 않는지 검증합니다.
        /// [EN] Verifies pre-cancelled startup preserves the current battle HUD and scene.
        /// </summary>
        [UnityTest]
        public IEnumerator CancelledStartupPreservesUiAndScene()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
                var manager = scope.Container.Resolve<SceneTransitionManager>();
                var hud = Object.FindFirstObjectByType<BattleHudView>();
                var resultText = hud.ResultText;
                var wasActive = hud.gameObject.activeSelf;
                var knight = GameObject.Find("Unit_Knight");
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                var entry = new AppEntryPoint(manager);
                var isCancelled = false;
                try
                {
                    await entry.StartAsync(cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    isCancelled = true;
                }

                Assert.That(isCancelled, Is.True);
                Assert.That(Object.FindFirstObjectByType<BattleHudView>(), Is.SameAs(hud));
                Assert.That(hud.ResultText, Is.EqualTo(resultText));
                Assert.That(hud.gameObject.activeSelf, Is.EqualTo(wasActive));
                Assert.That(entry.IsStarted, Is.False);
                Assert.That(manager.IsLoading, Is.False);
                Assert.That(GameObject.Find("Unit_Knight"), Is.SameAs(knight));
            });
        }

        /// <summary>
        /// 不正な遷移先と開始前の取消が現在の舞台を変更しないことを検証します。
        /// [KO] 잘못된 경로와 시작 전 취소가 현재 무대를 변경하지 않는지 검증합니다.
        /// [EN] Verifies invalid destinations and pre-cancellation preserve the current stage.
        /// </summary>
        [UnityTest]
        public IEnumerator InvalidAndCancelledRequestsPreserveStage()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var transition = Object.FindFirstObjectByType<AppLifetimeScope>().Container
                    .Resolve<SceneTransitionManager>();
                var knight = GameObject.Find("Unit_Knight");
                var isRejected = false;
                try
                {
                    await transition.LoadSceneAsync((Define.SceneType)int.MaxValue);
                }
                catch (ArgumentOutOfRangeException)
                {
                    isRejected = true;
                }

                Assert.That(isRejected, Is.True);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                var isCancelled = false;
                try
                {
                    await transition.LoadSceneAsync(Define.SceneType.Dungeon, cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    isCancelled = true;
                }

                Assert.That(isCancelled, Is.True);
                Assert.That(transition.IsLoading, Is.False);
                Assert.That(GameObject.Find("Unit_Knight"), Is.SameAs(knight));
            });
        }

        /// <summary>
        /// 開始後の取消でも Unity の完了まで重複切替を拒否します。
        /// [KO] 시작 후 취소되어도 Unity 완료까지 중복 전환을 거부합니다.
        /// [EN] Rejects overlapping transitions until Unity finishes a cancelled load.
        /// </summary>
        [UnityTest]
        public IEnumerator CancellationWaitsForUnityAndReleasesLock()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
                var transition = scope.Container.Resolve<SceneTransitionManager>();
                using var cancellation = new CancellationTokenSource();
                var loading = transition.LoadSceneAsync(Define.SceneType.Dungeon, cancellation.Token);
                Assert.That(transition.IsLoading, Is.True);
                cancellation.Cancel();
                var isRejected = false;
                try
                {
                    await transition.LoadSceneAsync(Define.SceneType.Dungeon, CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    isRejected = true;
                }

                Assert.That(isRejected, Is.True);
                var isCancelled = false;
                try
                {
                    await loading;
                }
                catch (OperationCanceledException)
                {
                    isCancelled = true;
                }

                Assert.That(isCancelled, Is.True);
                Assert.That(transition.IsLoading, Is.False);
                Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo((int)Define.SceneType.Dungeon));
                Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(scope));
            });
        }
    }
}
