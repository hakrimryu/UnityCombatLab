using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UnityCombatLab.Application.Ports
{
    /// <summary>
    /// 一回の挑戦で必要な資産を読み込み、挑戦の終了時に解放する境界。
    /// [KO] 한 도전에 필요한 자산을 로드하고 도전 종료 시 해제하는 경계입니다.
    /// [EN] Boundary for loading assets needed by one run and releasing them on exit.
    /// </summary>
    public interface IRunResources : IDisposable
    {
        /// <summary>
        /// 読み込みを完了してから取消を通知し、途中の資産も所有者に残します。
        /// [KO] 로딩 완료 후 취소를 알리고 중간 자산도 소유자에게 남깁니다.
        /// [EN] Finishes underlying loads before reporting cancellation, retaining ownership of partial resources.
        /// </summary>
        UniTask PrepareAsync(CancellationToken cancellationToken);
    }
}
