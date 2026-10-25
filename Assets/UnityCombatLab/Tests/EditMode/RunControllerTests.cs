using System;
using NUnit.Framework;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 出発と終了の競合、および古い挑戦からの通知拒否を確認します。
    /// [KO] 출발·종료 경쟁과 이전 도전 알림 거부를 확인합니다.
    /// [EN] Checks departure/cleanup races and rejection of stale-run notifications.
    /// </summary>
    public sealed class RunControllerTests
    {
        /// <summary>
        /// 取消から資源整理完了まで次の挑戦を開始できないことを確認します。
        /// [KO] 취소부터 자원 정리 완료까지 다음 도전을 시작할 수 없는지 확인합니다.
        /// [EN] Verifies another run cannot start between cancellation and cleanup completion.
        /// </summary>
        [Test]
        public void CancellationLocksDepartureUntilMatchingCleanupCompletes()
        {
            var controller = new RunController();
            var configuration = RunConfiguration.CreateDefault();
            Assert.That(controller.TryStart(configuration), Is.True);
            var first = controller.Current.Id;
            Assert.That(controller.TryStart(configuration), Is.False);
            Assert.That(controller.TryCompleteCleanup(first), Is.False);
            Assert.That(controller.TryEnterBattle(Guid.NewGuid()), Is.False);
            Assert.That(controller.TryBeginCleanup(first), Is.True);
            Assert.That(controller.TryEnterBattle(first), Is.False);
            Assert.That(controller.TryBeginCleanup(first), Is.False);
            Assert.That(controller.TryStart(configuration), Is.False);
            Assert.That(controller.TryCompleteCleanup(Guid.NewGuid()), Is.False);
            Assert.That(controller.TryCompleteCleanup(first), Is.True);
            Assert.That(controller.Result, Is.Null);
            Assert.That(controller.TryStart(configuration), Is.True);
            Assert.That(controller.Current.Id, Is.Not.EqualTo(first));
            Assert.That(controller.TryBeginCleanup(first), Is.False);
        }

        /// <summary>
        /// 確定結果だけを保存し、再出発では結果を初期化して編成だけを保つことを確認します。
        /// [KO] 확정 결과만 저장하고 재출발 시 결과를 초기화하며 편성만 유지하는지 확인합니다.
        /// [EN] Verifies committed results survive cleanup while restart resets results and retains formation.
        /// </summary>
        [Test]
        public void ResultsAreCopiedOnceAndRestartResetsRunState()
        {
            var controller = new RunController();
            var configuration = RunConfiguration.CreateDefault().Swap(Define.UnitRole.Mage, Define.BattleSlot.Front);
            controller.TryStart(configuration);
            var id = controller.Current.Id;
            Assert.That(controller.TryBeginCleanup(id, Define.BattleResult.Victory, 298), Is.False);
            Assert.That(controller.TryEnterBattle(id), Is.True);
            Assert.Throws<InvalidOperationException>(() => controller.RememberConfiguration(RunConfiguration.CreateDefault()));
            Assert.That(controller.TryBeginCleanup(id, Define.BattleResult.Victory, 298), Is.True);
            Assert.That(controller.TryBeginCleanup(id, Define.BattleResult.Defeat, 299), Is.False);
            Assert.That(controller.Result.Outcome, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(controller.Result.Ticks, Is.EqualTo(298));
            controller.TryCompleteCleanup(id);
            Assert.That(controller.Result.Configuration.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Mage));
            Assert.That(controller.TryStart(controller.LastConfiguration), Is.True);
            Assert.That(controller.Result, Is.Null);
            Assert.That(controller.Current.Phase, Is.EqualTo(Define.RunPhase.Loading));
        }
    }
}
