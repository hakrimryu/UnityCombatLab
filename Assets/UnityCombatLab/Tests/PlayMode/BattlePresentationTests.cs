using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Bootstrap;
using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Scenes;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
using VContainer;
using Object = UnityEngine.Object;

namespace UnityCombatLab.Tests.PlayMode
{
    /// <summary>
    /// 実際のシーン・HUD・購読・古い callback を検証します。
    /// [KO] 실제 씬·HUD·구독·이전 콜백을 검증합니다.
    /// [EN] Verifies real scenes, HUD, subscriptions and stale callbacks.
    /// </summary>
    public sealed class BattlePresentationTests
    {
        /// <summary>
        /// 各検証を独立した AppRoot から起動します。
        /// [KO] 각 검증을 독립 AppRoot에서 시작합니다.
        /// [EN] Starts each check from an independent AppRoot.
        /// </summary>
        [UnitySetUp]
        public IEnumerator StartBattle()
        {
            foreach (var app in Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None))
            {
                Object.Destroy(app.gameObject);
            }

            yield return null;
            yield return SceneManager.LoadSceneAsync((int)Define.SceneType.Boot, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var scope = Object.FindFirstObjectByType<BattleLifetimeScope>();
                if (scope != null && scope.Container != null && scope.Container.Resolve<BattleEntryPoint>().Hud != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Battle did not start.");
        }

        /// <summary>
        /// テスト所有の寿命を後に残しません。
        /// [KO] 테스트 소유 수명을 남기지 않습니다.
        /// [EN] Leaves no test-owned lifetime behind.
        /// </summary>
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var battle in Object.FindObjectsByType<BattleLifetimeScope>(FindObjectsSortMode.None))
            {
                Object.Destroy(battle.gameObject);
            }

            yield return null;
            foreach (var app in Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None))
            {
                Object.Destroy(app.gameObject);
            }

            yield return null;
        }

        /// <summary>
        /// 六人の表示が最後の死亡を含めコアと一致します。
        /// [KO] 여섯 명의 표시가 마지막 사망까지 코어와 일치합니다.
        /// [EN] All six displays match the core through the final death.
        /// </summary>
        [UnityTest]
        public IEnumerator SixViewsMatchCoreThroughFinalEvent()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var runner = entry.Runner;
            var views = Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            Assert.That(views.Length, Is.EqualTo(6));
            Assert.That(entry.Hud.transform.IsChildOf(Object.FindFirstObjectByType<AppLifetimeScope>().transform),
                Is.True);
            var lastEventCount = 0;
            runner.EventOccurred += item =>
            {
                if (item.Kind == Define.BattleEventKind.BattleEnded)
                {
                    lastEventCount++;
                }
            };
            while (runner.Result == Define.BattleResult.Ongoing)
            {
                runner.Advance(0.4);
                for (var index = 0; index < runner.ReadModel.Units.Count; index++)
                {
                    var model = runner.ReadModel.Units[index];
                    var snapshot = runner.GetUnit(model.Input.Team, model.Input.Slot);
                    Assert.That(entry.Hud.Units[index].DisplayedHealth, Is.EqualTo(snapshot.Health));
                    Assert.That(entry.Hud.Units[index].DisplayedShield, Is.EqualTo(snapshot.Shield));
                }
            }

            Assert.That(runner.Result, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(entry.Hud.ResultText, Is.EqualTo("勝利"));
            Assert.That(lastEventCount, Is.EqualTo(1));
            Assert.That(views.Count(view => view.IsDead), Is.EqualTo(3));
            Assert.That(views.Where(view => view.IsDead).All(view => view.CurrentAnimation == "Death"), Is.True);
            var tick = runner.CurrentTick;
            runner.Advance(100);
            Assert.That(runner.CurrentTick, Is.EqualTo(tick));
            Assert.That(lastEventCount, Is.EqualTo(1));
            yield return null;
        }

        /// <summary>
        /// Presenter 解放後、別モデルの変更でも View を更新しません。
        /// [KO] Presenter 해제 후 별도 모델이 변해도 View를 갱신하지 않습니다.
        /// [EN] Detached presenters cannot update a view when their model changes.
        /// </summary>
        [UnityTest]
        public IEnumerator HudSubscriptionIsReleasedAndCanRebind()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var hud = Object.Instantiate(entry.Hud);
            var inputs = entry.Runner.ReadModel.Units.Select(unit => unit.Input).ToArray();
            using var runner = new BattleRunner(Guid.NewGuid(), 1, inputs);
            var presenter = new BattleHudPresenter(runner.ReadModel, hud);
            var initial = hud.Units.Select(unit => unit.HealthRenderCount).ToArray();
            runner.Advance(0.4);
            Assert.That(hud.Units.Select(unit => unit.HealthRenderCount), Is.EqualTo(initial));
            presenter.Dispose();
            presenter.Dispose();
            runner.Advance(0.1);
            Assert.That(hud.Units.Select(unit => unit.HealthRenderCount), Is.EqualTo(initial));
            using (new BattleHudPresenter(runner.ReadModel, hud))
            {
                for (var index = 0; index < inputs.Length; index++)
                {
                    Assert.That(hud.Units[index].DisplayedHealth,
                        Is.EqualTo(runner.ReadModel.Units[index].Health.CurrentValue));
                    Assert.That(hud.Units[index].HealthRenderCount, Is.EqualTo(initial[index] + 1));
                }
            }

            Object.Destroy(hud.gameObject);
            yield return null;
        }

        /// <summary>
        /// 古い Tween 完了と古い RunId は再バインドしたモデルを変更しません。
        /// [KO] 이전 Tween 완료와 RunId는 재바인딩한 모델을 바꾸지 않습니다.
        /// [EN] Old tween completion and RunId cannot mutate a rebound model.
        /// </summary>
        [UnityTest]
        public IEnumerator OldMotionCallbackCannotAffectReboundView()
        {
            var view = GameObject.Find("Unit_Knight").GetComponent<UnitView>();
            var oldRun = Guid.NewGuid();
            view.Bind(oldRun);
            view.ShowAttack(oldRun, view.transform.position + Vector3.right, true, false);
            var motionField = typeof(UnitView).GetField("_motion", BindingFlags.Instance | BindingFlags.NonPublic);
            var callback = ((Tween)motionField.GetValue(view)).onComplete;
            var model = view.GetComponentInChildren<Animator>().transform;
            view.Release();
            var newRun = Guid.NewGuid();
            view.Bind(newRun);
            view.ShowAttack(newRun, view.transform.position + Vector3.right, true, false);
            var currentMotion = motionField.GetValue(view);
            var expectedPosition = model.localPosition + Vector3.forward;
            model.localPosition = expectedPosition;
            callback();
            Assert.That(model.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(motionField.GetValue(view), Is.SameAs(currentMotion));
            view.ShowDeath(oldRun);
            Assert.That(view.IsDead, Is.False);
            view.ShowDeath(newRun);
            callback();
            Assert.That(view.CurrentAnimation, Is.EqualTo("Death"));
            view.Release();
            Assert.That(view.IsBound, Is.False);
            yield return null;
        }

        /// <summary>
        /// 旧被害表示の callback が新表示を消しません。
        /// [KO] 이전 피해 표시 콜백이 새 표시를 지우지 않습니다.
        /// [EN] An old damage callback cannot erase a newer label.
        /// </summary>
        [UnityTest]
        public IEnumerator OldDamageCallbackCannotClearNewLabel()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var view = entry.Hud.Units[0];
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var tweenField = typeof(UnitStatusView).GetField("_damageTween", Flags);
            var textField = typeof(UnitStatusView).GetField("_damage", Flags);
            view.ShowDamage(12, 3);
            var callback = ((Tween)tweenField.GetValue(view)).onStepComplete;
            view.ClearTransient();
            view.ShowDamage(9, 0);
            var label = (TMPro.TMP_Text)textField.GetValue(view);
            var expectedText = label.text;
            callback();
            Assert.That(label.text, Is.EqualTo(expectedText));
            view.ClearTransient();
            yield return null;
        }

        /// <summary>
        /// Single 再読込で旧 HUD と購読を破棄し AppRoot は一つです。
        /// [KO] Single 재로드 시 이전 HUD와 구독을 해제하며 AppRoot는 하나입니다.
        /// [EN] Single reload disposes old HUD and subscriptions while preserving one AppRoot.
        /// </summary>
        [UnityTest]
        public IEnumerator DungeonReloadDisposesOldBattleLifetime()
        {
            var app = Object.FindFirstObjectByType<AppLifetimeScope>();
            var previous = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var oldHud = previous.Hud;
            yield return app.Container.Resolve<SceneTransitionManager>().LoadSceneAsync(Define.SceneType.Dungeon)
                .ToCoroutine();
            yield return null;
            Assert.That(previous.IsDisposed, Is.True);
            Assert.That(oldHud == null, Is.True);
            var tick = previous.Runner.CurrentTick;
            previous.Runner.Advance(1);
            Assert.That(previous.Runner.CurrentTick, Is.EqualTo(tick));
            Assert.That(Object.FindFirstObjectByType<AppLifetimeScope>(), Is.SameAs(app));
            Assert.That(Object.FindObjectsByType<BattleHudView>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length, Is.EqualTo(6));
        }
    }
}
