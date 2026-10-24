using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// プロジェクト基盤の境界と画面設定を検証します。
    /// [KO] 프로젝트 기반의 계층 경계와 화면 설정을 검증합니다.
    /// [EN] Verifies foundation boundaries and display settings.
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
                Assert.That(reference.Name, Does.Not.StartWith("Unity"));
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
    }
}
