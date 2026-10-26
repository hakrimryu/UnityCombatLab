using UnityCombatLab.Application.Ports;
using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Scenes;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityCombatLab.Bootstrap;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.Units;
using UnityCombatLab.Domain.Battle;
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
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo((int)Define.SceneType.Frontend));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<LoadingView>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<FrontendView>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<BattleHudView>(true), Is.Empty);
            Assert.That(Object.FindFirstObjectByType<FrontendView>().gameObject.scene,
                Is.EqualTo(SceneManager.GetActiveScene()));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield break;
        }

        /// <summary>
        /// 再読込で画面だけが置き換わり、同じ App Scope が残ることを検証します。
        /// [KO] 재로드 시 화면만 교체되고 같은 App Scope가 유지되는지 검증합니다.
        /// [EN] Verifies page replacement while retaining the same app scope.
        /// </summary>
        [UnityTest]
        public IEnumerator FrontendReloadPreservesAppAndReplacesView()
        {
            var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
            var previousView = Object.FindFirstObjectByType<FrontendView>();
            Assert.That(previousView, Is.Not.Null);
            var transition = scope.Container.Resolve<SceneTransitionManager>();
            var flow = scope.Container.Resolve<RunFlowCoordinator>();
            var isReady = false;
            yield return flow.StartFrontendAsync().ToCoroutine(resultHandler: value => isReady = value);
            Assert.That(isReady, Is.True);
            Assert.That(previousView == null, Is.True);
            Assert.That(Object.FindFirstObjectByType<FrontendView>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(scope));
            Assert.That(transition.IsLoading, Is.False);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 実際のボタンで英雄を交換し、選択印・能力・モデル・配置・HUD画像が同じ職業になることを確認します。
        /// [KO] 실제 버튼으로 영웅을 교환하고 선택 표시·능력·모델·위치·HUD 이미지가 같은 직업인지 확인합니다.
        /// [EN] Swaps heroes through actual buttons and verifies selection, stats, models, placement and HUD images agree.
        /// </summary>
        [UnityTest]
        public IEnumerator SwappedFormationKeepsVisualsStatsAndIconsTogether()
        {
            var app = Object.FindFirstObjectByType<AppLifetimeScope>();
            var flow = app.Container.Resolve<RunFlowCoordinator>();
            var frontend = Object.FindFirstObjectByType<FrontendView>();
            ReadField<ActionButtonView>(frontend, "_titleStart").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(flow.Page, Is.EqualTo(Define.FrontendPage.Formation));
            var cards = ReadField<HeroCardView[]>(frontend, "_heroCards");
            var slots = ReadField<FormationSlotView[]>(frontend, "_slots");
            AssertSelection(cards, null);
            ReadField<UnityEngine.UI.Button>(cards[0], "_button").onClick.Invoke();
            AssertSelection(cards, Define.UnitRole.Knight);
            ReadField<UnityEngine.UI.Button>(cards[2], "_button").onClick.Invoke();
            AssertSelection(cards, Define.UnitRole.Mage);
            ReadField<UnityEngine.UI.Button>(slots[0], "_button").onClick.Invoke();
            AssertSelection(cards, null);
            Assert.That(flow.Controller.LastConfiguration.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Mage));
            Assert.That(ReadField<TMPro.TMP_Text>(slots[0], "_text").text, Does.Contain(GameText.RoleName(Define.UnitRole.Mage)));
            ReadField<UnityEngine.UI.Button>(cards[1], "_button").onClick.Invoke();
            ReadField<ActionButtonView>(frontend, "_formationBack").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            ReadField<ActionButtonView>(frontend, "_titleStart").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            AssertSelection(cards, null);
            ReadField<ActionButtonView>(frontend, "_depart").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var deadline = Time.realtimeSinceStartup + 15f;
            while (flow.Controller.Current?.Phase != Define.RunPhase.Battle && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(flow.Controller.Current?.Phase, Is.EqualTo(Define.RunPhase.Battle));
            var battleScope = Object.FindFirstObjectByType<BattleLifetimeScope>();
            var battle = battleScope.Container.Resolve<BattleEntryPoint>();
            var assets = (RunAssetScope)flow.ActiveSession.Resources;
            var views = ReadField<UnitView[]>(battle, "_unitViews");
            var points = ReadField<Transform[]>(battleScope, "_spawnPoints");
            for (var index = 0; index < views.Length; index++)
            {
                // 0/1は味方/敵の前列、2/3は後列上、4/5は後列下。同じ配置の入力とViewを比較する。
                var team = (Define.BattleTeam)(index % Define.BattleTeamCount);
                var slot = (Define.BattleSlot)(index / Define.BattleTeamCount);
                var input = battle.Runner.GetUnit(team, slot).Input;
                var loaded = assets.GetUnit(input.Role);
                if (team == Define.BattleTeam.Ally)
                    Assert.That(input.Role, Is.EqualTo(flow.Controller.LastConfiguration.GetRole(slot)));

                var expected = loaded.Definition.CreateInput(input.Id, team, slot);
                Assert.That(input.MaxHealth, Is.EqualTo(expected.MaxHealth));
                Assert.That(input.Attack, Is.EqualTo(expected.Attack));
                Assert.That(input.IntervalTicks, Is.EqualTo(expected.IntervalTicks));
                Assert.That(views[index].name, Is.EqualTo(loaded.Prefab.name));
                Assert.That(Vector3.Distance(views[index].transform.position, points[index].position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(views[index].transform.rotation, points[index].rotation), Is.LessThan(0.01f));
                var model = ReadField<Transform>(views[index], "_model");
                var prefabModel = ReadField<Transform>(loaded.Prefab.GetComponent<UnitView>(), "_model");
                Assert.That(model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,
                    Is.SameAs(prefabModel.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh));
                Assert.That(ReadField<UnityEngine.UI.Image>(battle.Hud.Units[index], "_roleIcon").sprite,
                    Is.SameAs(assets.Icons[input.Role]));
                Assert.That(ReadField<TMPro.TMP_Text>(battle.Hud.Units[index], "_name").text,
                    Does.Contain(GameText.RoleName(input.Role)));
            }

            var initialTick = battle.Runner.CurrentTick;
            var initialFrame = Time.frameCount;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
            Assert.That(battle.Runner.CurrentTick, Is.GreaterThan(initialTick));
        }

        /// <summary>
        /// 四人目の生成失敗で先の三人を除去し、配置を戻せば六人を再生成できることを確認します。
        /// [KO] 네 번째 생성 실패로 먼저 만든 세 명을 제거하고 배치를 복구하면 여섯 명을 다시 생성하는지 확인합니다.
        /// [EN] Verifies failure at the fourth spawn removes the first three and allows a complete retry.
        /// </summary>
        [UnityTest]
        public IEnumerator PartialUnitCreationDestroysInstancesAndCanRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var app = Object.FindFirstObjectByType<AppLifetimeScope>();
                using var assets = new RunAssetScope(app.Container.Resolve<AddressableAssetManager>(),
                    app.Container.Resolve<UiAssetCatalog>(), app.Container.Resolve<UnitAssetCatalog>());
                await assets.PrepareAsync(CancellationToken.None);
                var root = new GameObject("PartialCreationTest");
                var pointsRoot = new GameObject("SpawnPoints");
                try
                {
                    var roles = new[] { Define.UnitRole.Knight, Define.UnitRole.SkeletonMelee, Define.UnitRole.Ranger,
                        Define.UnitRole.SkeletonRanged, Define.UnitRole.Mage, Define.UnitRole.SkeletonCaptain };
                    var inputs = new BattleUnitInput[roles.Length];
                    var points = new Transform[roles.Length];
                    for (var index = 0; index < roles.Length; index++)
                    {
                        // 偶数は味方、奇数は敵。同じ二人ごとに前列・後列上・後列下を割り当てる。
                        inputs[index] = assets.GetUnit(roles[index]).Definition.CreateInput(index + 1,
                            (Define.BattleTeam)(index % Define.BattleTeamCount), (Define.BattleSlot)(index / Define.BattleTeamCount));
                        points[index] = new GameObject("Spawn" + index).transform;
                        points[index].SetParent(pointsRoot.transform);
                    }

                    var fourth = points[3];
                    points[3] = null;
                    Assert.Throws<InvalidOperationException>(() => BattleUnitFactory.Create(inputs, assets, points, root.transform));
                    var partial = root.GetComponentsInChildren<UnitView>(true);
                    Assert.That(partial.Length, Is.EqualTo(3));
                    Assert.That(partial.All(view => !view.gameObject.activeSelf), Is.True);
                    await UniTask.NextFrame();
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(partial.All(view => view == null), Is.True);
                    points[3] = fourth;
                    var retry = BattleUnitFactory.Create(inputs, assets, points, root.transform);
                    Assert.That(retry.Length, Is.EqualTo(Define.MaxBattleUnitCount));
                    Assert.That(root.transform.childCount, Is.EqualTo(Define.MaxBattleUnitCount));
                }
                finally
                {
                    Object.Destroy(root);
                    Object.Destroy(pointsRoot);
                    // Destroyが完了した後で、usingがモデルと効果のロード所有権を返す。
                    await UniTask.NextFrame();
                }
            });
        }

        /// <summary>
        /// 全カードの色と印を比較し、職業名に選択文字を混ぜないことも確認します。
        /// [KO] 모든 카드의 색과 표시를 비교하고 직업 이름에 선택 문구가 섞이지 않는지도 확인합니다.
        /// [EN] Checks each card's color and badge, including keeping selection words out of role names.
        /// </summary>
        private static void AssertSelection(HeroCardView[] cards, Define.UnitRole? selected)
        {
            for (var index = 0; index < cards.Length; index++)
            {
                var isSelected = selected == (Define.UnitRole)index;
                Assert.That(ReadField<GameObject>(cards[index], "_selectionBadge").activeSelf, Is.EqualTo(isSelected));
                Assert.That(ReadField<UnityEngine.UI.Image>(cards[index], "_background").color,
                    Is.EqualTo(isSelected ? ReadField<Color>(cards[index], "_selectedColor") : Color.white));
                var description = ReadField<TMPro.TMP_Text>(cards[index], "_description").text;
                Assert.That(description, Does.Contain(GameText.RoleName((Define.UnitRole)index)));
                Assert.That(description, Does.Not.Contain("선택중"));
            }
        }

        /// <summary>
        /// 公開APIを増やさず、テストから生成済みViewと参照を確認します。
        /// [KO] 공개 API를 늘리지 않고 테스트에서 생성된 View와 참조를 확인합니다.
        /// [EN] Inspects created views and references without adding public product APIs.
        /// </summary>
        private static T ReadField<T>(object target, string name)
        {
            // Button の共通参照は Base にあるため、宣言元まで親クラスを順に確認する。
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return (T)field.GetValue(target);
            }

            throw new MissingFieldException(target.GetType().FullName, name);
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
            foreach (var loading in replacement.GetComponentsInChildren<LoadingView>(true))
                Object.DestroyImmediate(loading.gameObject);
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
                if (failedRoot != null)
                    Object.Destroy(failedRoot);
                if (replacement != null)
                    Object.Destroy(replacement);
            }
        }

        /// <summary>
        /// 取消済みの起動要求が現在の Frontend とシーンを変更しないことを検証します。
        /// [KO] 이미 취소된 시작 요청이 현재 Frontend와 씬을 변경하지 않는지 검증합니다.
        /// [EN] Verifies pre-cancelled startup preserves the current Frontend and scene.
        /// </summary>
        [UnityTest]
        public IEnumerator CancelledStartupPreservesUiAndScene()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
                var manager = scope.Container.Resolve<SceneTransitionManager>();
                var view = Object.FindFirstObjectByType<FrontendView>();
                var wasActive = view.gameObject.activeSelf;
                var activeScene = SceneManager.GetActiveScene();
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                var entry = new AppEntryPoint(scope.Container.Resolve<RunFlowCoordinator>());
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
                Assert.That(Object.FindFirstObjectByType<FrontendView>(), Is.SameAs(view));
                Assert.That(view.gameObject.activeSelf, Is.EqualTo(wasActive));
                Assert.That(entry.IsStarted, Is.False);
                Assert.That(manager.IsLoading, Is.False);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
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
                var view = Object.FindFirstObjectByType<FrontendView>();
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
                Assert.That(Object.FindFirstObjectByType<FrontendView>(), Is.SameAs(view));
            });
        }

        /// <summary>
        /// 読込中の内部取消後に Run と HUD を解放し、二重出発を受け付けません。
        /// [KO] 로딩 중 내부 취소 후 Run과 HUD를 해제하고 중복 출발을 받지 않습니다.
        /// [EN] Releases the run and HUD after internal loading cancellation and rejects duplicate departure.
        /// </summary>
        [UnityTest]
        public IEnumerator InternallyCancelledRunReleasesResourcesAndRejectsDuplicateDeparture()
        {
            var app = Object.FindFirstObjectByType<AppLifetimeScope>();
            var flow = app.Container.Resolve<RunFlowCoordinator>();
            var loading = flow.StartRunAsync(flow.Controller.LastConfiguration);
            var session = flow.ActiveSession;
            Assert.That(session, Is.Not.Null);
            yield return flow.StartRunAsync(flow.Controller.LastConfiguration).ToCoroutine();
            Assert.That(flow.ActiveSession, Is.SameAs(session));
            var view = app.GetComponentInChildren<LoadingView>(true);
            Assert.That(view.GetComponentsInChildren<UnityEngine.UI.Button>(true), Is.Empty);
            session.Cancel();
            yield return loading.ToCoroutine();
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(flow.Controller.Current, Is.Null);
            Assert.That(flow.Controller.Result, Is.Null);
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.Page, Is.EqualTo(Define.FrontendPage.Formation));
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(Object.FindObjectsByType<BattleHudView>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<BattleLifetimeScope>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo((int)Define.SceneType.Frontend));
        }

        /// <summary>
        /// 共通表示は操作ボタンを持たず、復帰失敗では回転を止めて再起動を案内します。
        /// [KO] 공통 표시에는 버튼이 없으며 복귀 실패 시 회전을 멈추고 재시작을 안내하는지 확인합니다.
        /// [EN] Checks that the shared display has no buttons and stops its spinner with restart guidance on failure.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDisplayHasNoButtonsAndStopsSpinnerOnFailure()
        {
            var view = Object.FindFirstObjectByType<AppLifetimeScope>().GetComponentInChildren<LoadingView>(true);
            var spinner = view.transform.Find("UI_Loading/Loading_01");
            var message = view.transform.Find("UI_LoadingMessage").GetComponent<TMPro.TMP_Text>();
            Assert.That(view.GetComponentsInChildren<UnityEngine.UI.Button>(true), Is.Empty);
            view.Show(Define.RunTransitionPhase.LoadingDungeon);
            var initialRotation = spinner.localRotation;
            yield return new WaitForSeconds(0.1f);
            Assert.That(Quaternion.Angle(initialRotation, spinner.localRotation), Is.GreaterThan(0f));
            view.Show(Define.RunTransitionPhase.RecoveryFailed);
            Assert.That(spinner.gameObject.activeSelf, Is.False);
            Assert.That(message.text, Is.EqualTo(GameText.FrontendFailed));
            var stoppedRotation = spinner.localRotation;
            yield return new WaitForSeconds(0.1f);
            Assert.That(spinner.localRotation, Is.EqualTo(stoppedRotation));
            view.Hide();
            Assert.That(view.gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// 途中の能力設定不一致で取得済み資産を返却し、実際の再試行ボタンから新しいRunを開始します。
        /// [KO] 중간 능력 설정 불일치 시 취득한 자산을 반환하고 실제 재시도 버튼으로 새 Run을 시작합니다.
        /// [EN] Releases acquired assets on a mid-load stat mismatch and starts a fresh run through the actual retry button.
        /// </summary>
        [UnityTest]
        public IEnumerator StartupFailureShowsErrorAndRetryStartsFreshRun()
        {
            var app = Object.FindFirstObjectByType<AppLifetimeScope>();
            var flow = app.Container.Resolve<RunFlowCoordinator>();
            var address = typeof(RunFlowCoordinator).GetField("_resourcesFactory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var original = address.GetValue(flow);
            var catalog = Object.Instantiate(app.Container.Resolve<UnitAssetCatalog>());
            typeof(UnitAssetReference).GetField("_definition", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(catalog.Get(Define.UnitRole.Mage), catalog.Get(Define.UnitRole.Knight).Definition);
            RunAssetScope failedAssets = null;
            try
            {
                // 複製したカタログだけを壊し、HUDと先の二職業をロードした後の復帰経路を通す。
                address.SetValue(flow, (Func<IRunResources>)(() => failedAssets = new RunAssetScope(
                    app.Container.Resolve<AddressableAssetManager>(), app.Container.Resolve<UiAssetCatalog>(), catalog)));
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Unit definition does not match catalog role Mage"));
                yield return flow.StartRunAsync(flow.Controller.LastConfiguration).ToCoroutine();
                Assert.That(flow.Page, Is.EqualTo(Define.FrontendPage.Error));
                Assert.That(flow.CanRetry, Is.True);
                Assert.That(flow.IsBusy, Is.False);
                Assert.That(flow.ActiveSession, Is.Null);
                Assert.That(flow.Controller.Result, Is.Null);
                Assert.That(failedAssets.HudPrefab, Is.Null);
                Assert.That(failedAssets.Icons, Is.Empty);
                Assert.Throws<ObjectDisposedException>(() => failedAssets.GetUnit(Define.UnitRole.Knight));
                Assert.That(ReadField<System.Collections.Generic.List<IDisposable>>(
                    ReadField<AddressableAssetScope>(failedAssets, "_scope"), "_leases"), Is.Empty);
                Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(Object.FindObjectsByType<BattleHudView>(FindObjectsSortMode.None), Is.Empty);
                address.SetValue(flow, original);
                var frontend = Object.FindFirstObjectByType<FrontendView>();
                var retry = frontend.transform.Find("SafeArea/UI_RunError/UI_ErrorPanel/UI_Retry").GetComponent<UnityEngine.UI.Button>();
                Assert.That(retry.gameObject.activeInHierarchy, Is.True);
                Assert.That(retry.IsInteractable(), Is.True);
                retry.onClick.Invoke();
                var deadline = Time.realtimeSinceStartup + 10f;
                while (flow.Controller.Current?.Phase != Define.RunPhase.Battle && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(flow.Controller.Current?.Phase, Is.EqualTo(Define.RunPhase.Battle));
                Assert.That(flow.ActiveSession, Is.Not.Null);
                Assert.That(Object.FindObjectsByType<BattleHudView>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
            finally
            {
                address.SetValue(flow, original);
                Object.Destroy(catalog);
            }
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
                // 画面準備を持たないBootで、低水準のシーン切替だけの取消とロックを検証する。
                var loading = transition.LoadSceneAsync(Define.SceneType.Boot, cancellation.Token);
                Assert.That(transition.IsLoading, Is.True);
                cancellation.Cancel();
                var isRejected = false;
                try
                {
                    await transition.LoadSceneAsync(Define.SceneType.Boot, CancellationToken.None);
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
                Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo((int)Define.SceneType.Boot));
                Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(scope));
                Assert.That(await scope.Container.Resolve<RunFlowCoordinator>().StartFrontendAsync(CancellationToken.None), Is.True);
            });
        }
    }
}
