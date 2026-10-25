using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Common;

namespace UnityCombatLab.Application.Ports
{
    /// <summary>
    /// Applicationからシーンの置換を要求する境界。
    /// [KO] Application에서 씬 교체를 요청하는 경계입니다.
    /// [EN] Boundary for application requests to replace a scene.
    /// </summary>
    public interface ISceneTransition
    {
        /// <summary>
        /// 開始後は実際の読込完了を待ち、取消でも並行した切替を発生させません。
        /// [KO] 시작 후 실제 로드 완료를 기다려 취소되어도 전환이 겹치지 않게 합니다.
        /// [EN] Waits for an active load to finish even on cancellation, preventing overlapping transitions.
        /// </summary>
        UniTask LoadSceneAsync(Define.SceneType sceneType, CancellationToken cancellationToken = default);
    }
}
