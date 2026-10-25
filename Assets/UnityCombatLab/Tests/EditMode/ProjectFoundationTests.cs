using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// Domain の参照先・画面設定・シーンのビルド順を確認します。
    /// [KO] Domain 참조 대상·화면 설정·씬 빌드 순서를 확인합니다.
    /// [EN] Checks Domain references, display settings and scene build order.
    /// </summary>
    public sealed class ProjectFoundationTests
    {
        /// <summary>
        /// Domain がエンジンや外部ライブラリを参照しないことを検証します。
        /// [KO] Domain이 엔진 및 외부 라이브러리를 참조하지 않는지 검증합니다.
        /// [EN] Verifies that Domain references no engine or third-party libraries.
        /// </summary>
        [Test]
        public void DomainHasNoEngineOrPackageDependencies()
        {
            var assembly = Assembly.Load("UnityCombatLab.Domain");
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name != "UnityCombatLab.Common")
                {
                    Assert.That(reference.Name, Does.Not.StartWith("Unity"));
                }

                Assert.That(reference.Name, Does.Not.StartWith("VContainer"));
                Assert.That(reference.Name, Does.Not.StartWith("R3"));
                Assert.That(reference.Name, Does.Not.StartWith("UniTask"));
                Assert.That(reference.Name, Does.Not.StartWith("DOTween"));
            }
        }

        /// <summary>
        /// 横向きだけを許可し、表示名を契約と一致させます。
        /// [KO] 가로모드만 허용하고 표시명이 계약과 일치하는지 확인합니다.
        /// [EN] Verifies landscape-only rotation and the agreed product name.
        /// </summary>
        [Test]
        public void DisplaySettingsMatchProjectContract()
        {
            Assert.That(PlayerSettings.productName, Is.EqualTo("Unity Combat Lab"));
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }

        /// <summary>
        /// 列挙値とビルド順序が正しいシーンアセットに対応することを検証します。
        /// [KO] enum 값과 빌드 순서가 올바른 씬 에셋에 대응하는지 검증합니다.
        /// [EN] Verifies enum values and build order correspond to the correct scene assets.
        /// </summary>
        [Test]
        public void SceneIndicesResolveToExpectedBuildAssets()
        {
            Assert.That((int)Define.SceneType.Boot, Is.EqualTo(0));
            Assert.That((int)Define.SceneType.Dungeon, Is.EqualTo(1));
            var expectedPaths = new[]
            {
                "Assets/UnityCombatLab/Scenes/Boot.unity",
                "Assets/UnityCombatLab/Scenes/Dungeon.unity"
            };
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(expectedPaths.Length));
            for (var index = 0; index < expectedPaths.Length; index++)
            {
                Assert.That(scenes[index].path, Is.EqualTo(expectedPaths[index]));
                Assert.That(SceneUtility.GetScenePathByBuildIndex(index), Is.EqualTo(expectedPaths[index]));
                Assert.That(scenes.Count(scene => scene.path == expectedPaths[index]), Is.EqualTo(1));
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(expectedPaths[index]), Is.Not.Null);
            }
        }
    }
}
