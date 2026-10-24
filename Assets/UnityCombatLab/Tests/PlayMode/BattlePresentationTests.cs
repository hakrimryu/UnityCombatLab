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
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.Styling;
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
                    Assert.That(model.Health.CurrentValue, Is.EqualTo(snapshot.Health));
                    Assert.That(model.Shield.CurrentValue, Is.EqualTo(snapshot.Shield));
                    Assert.That(entry.Hud.Units[index].DisplayedHealth,
                        Is.EqualTo(entry.Presentation.Units[index].Health.CurrentValue));
                    var status = entry.Hud.Units[index];
                    var healthLabel = status.transform.Find("Health").GetComponent<TMPro.TMP_Text>();
                    var healthBar = status.transform.Find("HealthBar").GetComponent<UnityEngine.UI.Slider>();
                    Assert.That(healthLabel.text,
                        Is.EqualTo(entry.Presentation.Units[index].Health.CurrentValue + " / " +
                                   model.Input.MaxHealth));
                    Assert.That(healthBar.normalizedValue,
                        Is.InRange((float)snapshot.Health / model.Input.MaxHealth, 1f));
                    Assert.That(status.transform.Find("UI_Defeated").gameObject.activeSelf,
                        Is.EqualTo(entry.Presentation.Units[index].Health.CurrentValue == 0));
                }
            }

            Assert.That(runner.Result, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(entry.Hud.ResultText, Is.EqualTo("戦闘中"));
            entry.Presenter.Advance(3);
            entry.Presenter.Advance(1);
            Assert.That(entry.Hud.ResultText, Is.EqualTo("勝利"));
            Assert.That(lastEventCount, Is.EqualTo(1));
            Assert.That(views.Count(view => view.IsDead), Is.EqualTo(3));
            Assert.That(views.Where(view => view.IsDead).All(view => view.CurrentAnimation == "Death"), Is.True);
            var tick = runner.CurrentTick;
            runner.Advance(100);
            Assert.That(runner.CurrentTick, Is.EqualTo(tick));
            Assert.That(lastEventCount, Is.EqualTo(1));
            // 数字と死亡は上で即時確認し、最後の損失・残像が終わってからゲージの到着値を確認する。
            yield return new WaitForSeconds(0.8f);
            for (var index = 0; index < runner.ReadModel.Units.Count; index++)
            {
                var model = runner.ReadModel.Units[index];
                var status = entry.Hud.Units[index];
                var bar = status.transform.Find("HealthBar").GetComponent<UnityEngine.UI.Slider>();
                Assert.That(bar.normalizedValue,
                    Is.EqualTo((float)model.Health.CurrentValue / model.Input.MaxHealth).Within(0.001f));
                Assert.That(status.transform.Find("HealthBar/FillArea/UI_DamagePreview")
                    .GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                Assert.That(status.transform.Find("HealthBar/FillArea/UI_TrailingHealth")
                    .GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
            }

            yield return null;
        }

        /// <summary>
        /// 確定値・損失予告・等速補間・残像を別々に検証し、連続被害でも現在位置から続くことを確認します。
        /// [KO] 확정값·손실 예고·선형 보간·잔상을 각각 검증하고 연속 피해도 현재 위치에서 이어지는지 확인합니다.
        /// [EN] Verifies confirmed values, loss preview, linear movement and trailing health, including continuous repeated hits.
        /// </summary>
        [UnityTest]
        public IEnumerator HealthGaugePreviewsLerpsAndTrailsConsecutiveDamage()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var hud = Object.Instantiate(entry.Hud);
            try
            {
                var colors = Object.FindFirstObjectByType<AppLifetimeScope>().Container.Resolve<GameColorPalette>();
                hud.ApplyPalette(colors);
                var view = hud.Units[0];
                view.Initialize("騎士", Define.BattleTeam.Ally);
                var bar = view.transform.Find("HealthBar").GetComponent<UnityEngine.UI.Slider>();
                var preview = view.transform.Find("HealthBar/FillArea/UI_DamagePreview")
                    .GetComponent<UnityEngine.UI.Image>();
                var trail = view.transform.Find("HealthBar/FillArea/UI_TrailingHealth")
                    .GetComponent<UnityEngine.UI.Image>();

                view.SetHealth(100, 100);
                view.SetPreviewHealth(60, 100);
                Assert.That(view.DisplayedHealth, Is.EqualTo(100));
                Assert.That(bar.normalizedValue, Is.EqualTo(1f));
                Assert.That(preview.enabled, Is.True);
                Assert.That(preview.rectTransform.anchorMin.x, Is.EqualTo(0.6f).Within(0.001f));
                view.SetHealth(60, 100);
                view.ShowDamage(40, 0);
                Assert.That(view.DisplayedHealth, Is.EqualTo(60));
                Assert.That(bar.normalizedValue, Is.EqualTo(1f));
                Assert.That(preview.enabled, Is.True);
                Assert.That(preview.rectTransform.anchorMin.x, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(preview.rectTransform.anchorMax.x, Is.EqualTo(1f));
                Assert.That(preview.color, Is.EqualTo(colors.GetColor(Define.PaletteColor.DamagePreview)));
                Assert.That(trail.color, Is.EqualTo(colors.GetColor(Define.PaletteColor.TrailingHealth)));
                view.Advance(0.12f);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.8f).Within(0.001f));
                view.Advance(0.12f);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(preview.enabled, Is.False);
                Assert.That(trail.enabled, Is.True);
                Assert.That(trail.rectTransform.anchorMax.x, Is.EqualTo(1f));
                view.Advance(0.335f);
                Assert.That(trail.rectTransform.anchorMax.x, Is.EqualTo(0.8f).Within(0.001f));
                view.SetPreviewHealth(40, 100);
                view.SetHealth(40, 100);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(trail.rectTransform.anchorMax.x, Is.EqualTo(0.8f).Within(0.001f));
                view.Advance(1f);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(trail.enabled, Is.False);
                Assert.That(preview.enabled, Is.False);

                view.Initialize("騎士", Define.BattleTeam.Ally);
                view.SetHealth(100, 100);
                view.SetHealth(60, 100);
                // 0.12 秒を 4 フレームに分けても、上の 1 フレームと同じ 80% に到着する。
                for (var frame = 0; frame < 4; frame++)
                {
                    view.Advance(0.03f);
                }

                Assert.That(bar.normalizedValue, Is.EqualTo(0.8f).Within(0.001f));
            }
            finally
            {
                Object.Destroy(hud.gameObject);
            }

            yield return null;
        }

        /// <summary>
        /// 六人が同時に攻撃を始め、各自の命中時刻でだけ HP が画面へ届くことを確認します。
        /// [KO] 여섯 명이 동시에 공격을 시작하고 각 명중 시각에만 HP가 화면에 도착하는지 확인합니다.
        /// [EN] Verifies six concurrent attacks and HUD health changes only at each attack's own impact time.
        /// </summary>
        [UnityTest]
        public IEnumerator ConcurrentAttacksPresentDamageAtTheirOwnImpact()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            entry.Runner.Advance(0.5);
            Assert.That(entry.Presenter.PendingAttackCount, Is.EqualTo(6));
            var views = Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            Assert.That(views.All(v => v.IsAttacking && v.CurrentAnimation == "Attack"), Is.True);
            Assert.That(entry.Runner.ReadModel.Units[1].Health.CurrentValue, Is.EqualTo(288));
            Assert.That(entry.Hud.Units[1].DisplayedHealth, Is.EqualTo(420));
            var status = entry.Hud.Units[1];
            var preview = status.transform.Find("HealthBar/FillArea/UI_DamagePreview")
                .GetComponent<UnityEngine.UI.Image>();
            Assert.That(preview.enabled, Is.True);
            entry.Presenter.Advance(0.3);
            Assert.That(entry.Hud.Units[1].DisplayedHealth, Is.EqualTo(420));
            var ranger = views.Single(v => v.name == "Unit_Ranger");
            Assert.That(ranger.transform.Find("VFX_Projectile0").gameObject.activeSelf, Is.True);
            entry.Presenter.Advance(0.04);
            Assert.That(entry.Hud.Units[1].DisplayedHealth, Is.EqualTo(380));
            Assert.That(views.Single(v => v.name == "Unit_SkeletonWarrior").CurrentAnimation, Is.EqualTo("Attack"));
            status.Advance(0.12f);
            var bar = status.transform.Find("HealthBar").GetComponent<UnityEngine.UI.Slider>();
            Assert.That(bar.normalizedValue, Is.EqualTo((1f + 380f / 420f) * 0.5f).Within(0.001f));
            entry.Presenter.Advance(0.09);
            Assert.That(entry.Hud.Units[1].DisplayedHealth, Is.EqualTo(346));
            Assert.That(entry.Hud.Units[0].DisplayedHealth, Is.EqualTo(575));
            Assert.That(entry.Hud.Units[2].DisplayedHealth, Is.EqualTo(328));
            Assert.That(ranger.transform.Find("VFX_Projectile0").GetComponent<BattleEffectView>().IsEmitting, Is.False);
            entry.Presenter.Advance(0.2);
            Assert.That(entry.Hud.Units[1].DisplayedHealth, Is.EqualTo(288));
            Assert.That(entry.Presentation.Units[1].Health.CurrentValue, Is.EqualTo(288));
            yield return null;
        }

        /// <summary>
        /// 異なる到着順でも HP が増えず、解除した予約が HUD を更新しないことを確認します。
        /// [KO] 도착 순서가 달라도 HP가 늘지 않고 해제한 예약이 HUD를 갱신하지 않는지 확인합니다.
        /// [EN] Verifies differing arrival order cannot restore health and disposed cues cannot update the HUD.
        /// </summary>
        [UnityTest]
        public IEnumerator ArrivalOrderAndDisposedCuesCannotRestoreOrChangeHealth()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            using (var state = new UnitPresentationReadModel(entry.Runner.ReadModel.Units[0].Input))
            {
                state.ReserveDamage(40);
                state.ReserveDamage(30);
                Assert.That(state.Health.CurrentValue, Is.EqualTo(600));
                Assert.That(state.PreviewHealth.CurrentValue, Is.EqualTo(530));
                state.ApplyDamage(30, 0);
                Assert.That(state.Health.CurrentValue, Is.EqualTo(570));
                state.ApplyDamage(40, 0);
                Assert.That(state.Health.CurrentValue, Is.EqualTo(530));
            }

            entry.Runner.Advance(0.5);
            var health = entry.Hud.Units.Select(v => v.DisplayedHealth).ToArray();
            entry.Presenter.Dispose();
            entry.Presenter.Advance(10);
            Assert.That(entry.Presenter.PendingAttackCount, Is.Zero);
            Assert.That(entry.Hud.Units.Select(v => v.DisplayedHealth), Is.EqualTo(health));
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).All(v => !v.IsBound), Is.True);
            yield return null;
        }

        /// <summary>
        /// 閃光・小さな押し戻しが攻撃や論理座標を変えず、死亡・解除後に戻らないことを確認します。
        /// [KO] 섬광·작은 밀림이 공격이나 논리 좌표를 바꾸지 않고 사망·해제 후 되살아나지 않는지 확인합니다.
        /// [EN] Verifies flash and recoil preserve attacks and logical positions without resuming after death or release.
        /// </summary>
        [UnityTest]
        public IEnumerator HitFlashAndRecoilPreserveAttackAndReleaseCleanly()
        {
            var view = GameObject.Find("Unit_Knight").GetComponent<UnitView>();
            var runId = Guid.NewGuid();
            var position = view.transform.position;
            var reaction = view.GetComponentInChildren<UnitHitReactionView>();
            var renderer = view.GetComponentInChildren<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();
            var emission = Shader.PropertyToID("_EmissionColor");
            view.Bind(runId);
            view.ShowAttack(runId, position + Vector3.right * 3, true, false);
            view.ShowHit(runId, view.ImpactPosition + Vector3.right * 2);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(emission).maxColorComponent, Is.GreaterThan(1));
            Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(
                ((Texture2D)renderer.sharedMaterial.GetTexture(Shader.PropertyToID("_EmissionMap"))).GetPixel(0, 0)
                .maxColorComponent,
                Is.Zero);
            reaction.Advance(0.045f);
            Assert.That(view.CurrentAnimation, Is.EqualTo("Attack"));
            Assert.That(view.transform.position, Is.EqualTo(position));
            Assert.That(reaction.Displacement.x, Is.LessThan(0));
            Assert.That(reaction.Displacement.magnitude,
                Is.EqualTo(0.14f).Within(0.001f));
            view.ShowDeath(runId);
            var deathPosition = reaction.Displacement;
            reaction.Advance(1f);
            Assert.That(reaction.Displacement, Is.EqualTo(deathPosition));
            view.Release();
            reaction.Advance(1f);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(emission).maxColorComponent, Is.Zero);
            Assert.That(reaction.Displacement, Is.EqualTo(Vector3.zero));
            view.Bind(Guid.NewGuid());
            view.ShowHit(runId, position + Vector3.right);
            reaction.Advance(1f);
            Assert.That(reaction.Displacement, Is.EqualTo(Vector3.zero));
            reaction.Play(view.ImpactPosition + Vector3.right * 2);
            reaction.Advance(0.045f);
            reaction.Play(view.ImpactPosition + Vector3.right * 2);
            reaction.Advance(0.045f);
            Assert.That(reaction.Displacement.magnitude, Is.EqualTo(0.14f).Within(0.001f));
            reaction.Advance(1f);
            Assert.That(reaction.Displacement, Is.EqualTo(Vector3.zero));
            yield return null;
        }

        /// <summary>
        /// 攻撃中の Hit は攻撃を中断せず、同時の三被害数字がそれぞれ残ることを確認します。
        /// [KO] 공격 중 Hit가 공격을 중단하지 않고 동시 피해 숫자 세 개가 각각 남는지 확인합니다.
        /// [EN] Verifies hits preserve attacks and three concurrent damage numbers remain independently visible.
        /// </summary>
        [UnityTest]
        public IEnumerator HitProtectsAttackAndDamageNumbersRemainSeparate()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var view = GameObject.Find("Unit_Knight").GetComponent<UnitView>();
            var runId = Guid.NewGuid();
            view.Bind(runId);
            view.ShowAttack(runId, view.transform.position + Vector3.right * 3, true, false);
            view.ShowHit(runId, view.ImpactPosition + Vector3.right * 2);
            Assert.That(view.CurrentAnimation, Is.EqualTo("Attack"));
            Assert.That(view.IsAttacking, Is.True);
            var status = entry.Hud.Units[0];
            status.ClearTransient();
            status.ShowDamage(40, 0);
            status.ShowDamage(34, 0);
            status.ShowDamage(58, 0);
            var labels = new[] { "Damage", "UI_Damage1", "UI_Damage2" }
                .Select(name => status.transform.Find(name).GetComponent<TMPro.TMP_Text>().text).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "-40", "-34", "-58" }));
            view.ShowDeath(runId);
            Assert.That(view.CurrentAnimation, Is.EqualTo("Death"));
            status.ClearTransient();
            Assert.That(new[] { "Damage", "UI_Damage1", "UI_Damage2" }
                .All(name => status.transform.Find(name).GetComponent<TMPro.TMP_Text>().text == ""), Is.True);
            yield return null;
        }

        /// <summary>
        /// 回復・致死被害・非表示・再初期化で古い HP 演出が新しい値を上書きしないことを確認します。
        /// [KO] 회복·치명 피해·비활성화·재초기화 때 이전 HP 연출이 새 값을 덮어쓰지 않는지 확인합니다.
        /// [EN] Checks that healing, lethal damage, hiding and reinitialization cannot revive stale gauge transitions.
        /// </summary>
        [UnityTest]
        public IEnumerator HealthGaugeResetsForHealingDeathDisableAndRebind()
        {
            var entry = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>();
            var hud = Object.Instantiate(entry.Hud);
            try
            {
                var colors = Object.FindFirstObjectByType<AppLifetimeScope>().Container.Resolve<GameColorPalette>();
                hud.ApplyPalette(colors);
                var view = hud.Units[0];
                var bar = view.transform.Find("HealthBar").GetComponent<UnityEngine.UI.Slider>();
                var preview = view.transform.Find("HealthBar/FillArea/UI_DamagePreview")
                    .GetComponent<UnityEngine.UI.Image>();
                var trail = view.transform.Find("HealthBar/FillArea/UI_TrailingHealth")
                    .GetComponent<UnityEngine.UI.Image>();

                view.Initialize("騎士", Define.BattleTeam.Ally);
                view.SetHealth(40, 100);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.4f).Within(0.001f));
                view.SetShield(20);
                view.ShowDamage(0, 10);
                Assert.That(preview.enabled, Is.False);
                view.SetHealth(80, 100);
                Assert.That(preview.enabled, Is.False);
                Assert.That(trail.enabled, Is.False);
                view.Advance(0.12f);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.6f).Within(0.001f));
                view.SetHealth(0, 100);
                Assert.That(view.DisplayedHealth, Is.Zero);
                Assert.That(view.transform.Find("UI_Defeated").gameObject.activeSelf, Is.True);
                view.Advance(1f);
                Assert.That(bar.normalizedValue, Is.Zero);
                Assert.That(preview.enabled || trail.enabled, Is.False);
                view.Initialize("騎士", Define.BattleTeam.Ally);
                view.SetHealth(100, 100);
                view.SetHealth(30, 100);
                view.gameObject.SetActive(false);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(preview.enabled || trail.enabled, Is.False);
                view.gameObject.SetActive(true);
                view.Initialize("騎士", Define.BattleTeam.Ally);
                view.SetHealth(75, 100);
                view.Advance(2f);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.75f).Within(0.001f));
                view.SetHealth(50, 100);
                view.ClearTransient();
                view.Advance(2f);
                Assert.That(bar.normalizedValue, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(preview.enabled || trail.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(hud.gameObject);
            }

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
            using var presented = new BattlePresentationReadModel(inputs);
            var colors = Object.FindFirstObjectByType<AppLifetimeScope>().Container.Resolve<GameColorPalette>();
            var presenter = new BattleHudPresenter(presented, hud, colors);
            var initial = hud.Units.Select(unit => unit.HealthRenderCount).ToArray();
            runner.Advance(0.4);
            Assert.That(hud.Units.Select(unit => unit.HealthRenderCount), Is.EqualTo(initial));
            presenter.Dispose();
            presenter.Dispose();
            runner.Advance(0.1);
            presented.Units[0].ReserveDamage(10);
            presented.Units[0].ApplyDamage(10, 0);
            Assert.That(hud.Units.Select(unit => unit.HealthRenderCount), Is.EqualTo(initial));
            using (new BattleHudPresenter(presented, hud, colors))
            {
                for (var index = 0; index < inputs.Length; index++)
                {
                    Assert.That(hud.Units[index].DisplayedHealth,
                        Is.EqualTo(presented.Units[index].Health.CurrentValue));
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
            var motionRoot = view.transform.Find("Unit_MotionRoot");
            view.Release();
            var newRun = Guid.NewGuid();
            view.Bind(newRun);
            view.ShowAttack(newRun, view.transform.position + Vector3.right, true, false);
            var currentMotion = motionField.GetValue(view);
            var expectedPosition = motionRoot.localPosition + Vector3.forward;
            motionRoot.localPosition = expectedPosition;
            callback();
            Assert.That(motionRoot.localPosition, Is.EqualTo(expectedPosition));
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
            var numbers = view.GetComponent<DamageNumberView>();
            var slotsField = typeof(DamageNumberView).GetField("_slots", Flags);
            var firstSlot = ((Array)slotsField.GetValue(numbers)).GetValue(0);
            var timerField = firstSlot.GetType().GetField("_timer", Flags);
            view.ClearTransient();
            view.ShowDamage(12, 3);
            var callback = ((Tween)timerField.GetValue(firstSlot)).onStepComplete;
            view.ClearTransient();
            view.ShowDamage(9, 6);
            var label = view.transform.Find("Damage").GetComponent<TMPro.TMP_Text>();
            var expectedText = label.text;
            var shieldLabel = view.transform.Find("UI_ShieldDamage").GetComponent<TMPro.TMP_Text>();
            var shieldIcon = view.transform.Find("UI_ShieldDamageIcon").gameObject;
            callback();
            Assert.That(label.text, Is.EqualTo(expectedText));
            Assert.That(shieldLabel.text, Is.EqualTo("-6"));
            Assert.That(shieldIcon.activeSelf, Is.True);
            view.ClearTransient();
            Assert.That(shieldLabel.text, Is.Empty);
            Assert.That(shieldIcon.activeSelf, Is.False);
            yield return null;
        }

        /// <summary>
        /// ノッチを想定した内側の安全領域でも 6 枚が重ならず、名前と HP が切れないことを確認します。
        /// [KO] 노치를 가정한 안쪽 안전 영역에서도 패널 6개가 겹치지 않고 이름·HP가 잘리지 않는지 확인합니다.
        /// [EN] Checks that all six panels remain separate and their names and HP fit inside an inset safe area.
        /// </summary>
        [UnityTest]
        public IEnumerator PanelsFitInsetSafeAreaWithoutOverlap()
        {
            var hud = Object.FindFirstObjectByType<BattleLifetimeScope>().Container.Resolve<BattleEntryPoint>().Hud;
            var safe = (RectTransform)hud.transform.Find("SafeArea");
            var size = new Vector2Int(Screen.width, Screen.height);
            var inset = new Rect(size.x * 0.08f, size.y * 0.04f, size.x * 0.84f, size.y * 0.92f);
            try
            {
                hud.UpdateLayout(inset, size);
                Canvas.ForceUpdateCanvases();
                hud.UpdateLayout(inset, size);
                var panels = hud.Units.Select(unit => (RectTransform)unit.transform).ToArray();
                var bounds = panels.Select(panel => new Rect((Vector2)panel.localPosition -
                                                             new Vector2(panel.rect.width * 0.5f, 0), panel.rect.size))
                    .ToArray();
                for (var index = 0; index < bounds.Length; index++)
                {
                    Assert.That(bounds[index].xMin, Is.GreaterThanOrEqualTo(safe.rect.xMin));
                    Assert.That(bounds[index].xMax, Is.LessThanOrEqualTo(safe.rect.xMax));
                    Assert.That(bounds[index].yMin, Is.GreaterThanOrEqualTo(safe.rect.yMin));
                    Assert.That(bounds[index].yMax, Is.LessThanOrEqualTo(safe.rect.yMax - 140));
                    for (var other = index + 1; other < bounds.Length; other++)
                    {
                        Assert.That(bounds[index].Overlaps(bounds[other]), Is.False,
                            "Status panels " + index + " and " + other + " overlap.");
                    }

                    foreach (var name in new[] { "Role", "Health", "Shield", "UI_Defeated" })
                    {
                        var text = panels[index].Find(name).GetComponent<TMPro.TMP_Text>();
                        text.ForceMeshUpdate(true);
                        Assert.That(text.isTextOverflowing, Is.False, text.name + " text is clipped.");
                    }
                }
            }
            finally
            {
                hud.UpdateLayout(Screen.safeArea, size);
            }

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
