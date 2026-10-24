using System.Collections;
using NUnit.Framework;
using UnityCombatLab.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace UnityCombatLab.Tests.PlayMode
{
    /// <summary>
    /// 実際のシーン起動と Additive 構成を検証します。
    /// [KO] 실제 씬 시작과 Additive 구성을 검증합니다.
    /// [EN] Verifies actual scene startup and additive composition.
    /// </summary>
    public sealed class BootSceneTests
    {
        /// <summary>
        /// DI による起動と単一のアプリサービスを検証します。
        /// [KO] DI를 통한 시작과 앱 서비스의 단일 구성을 검증합니다.
        /// [EN] Verifies DI startup and single app services.
        /// </summary>
        [UnityTest]
        public IEnumerator BootStartsThroughVContainer()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return null;

            var scopes = Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None);
            Assert.That(scopes.Length, Is.EqualTo(1));
            Assert.That(scopes[0].Container.Resolve<AppEntryPoint>().IsStarted, Is.True);
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        /// <summary>
        /// Dungeon を追加しても App Scope や Listener が増えないことを検証します。
        /// [KO] Dungeon을 추가해도 App Scope 및 Listener가 중복되지 않는지 검증합니다.
        /// [EN] Verifies that additive Dungeon loading does not duplicate app services.
        /// </summary>
        [UnityTest]
        public IEnumerator DungeonLoadsWithoutDuplicatingAppServices()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return null;
            yield return SceneManager.LoadSceneAsync("Dungeon", LoadSceneMode.Additive);

            Assert.That(SceneManager.GetSceneByName("Dungeon").isLoaded, Is.True);
            Assert.That(Object.FindObjectsByType<AppLifetimeScope>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

            yield return SceneManager.UnloadSceneAsync("Dungeon");
        }
    }
}
