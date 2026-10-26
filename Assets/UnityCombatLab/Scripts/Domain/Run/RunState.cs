using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 一回の挑戦の識別子・開始設定・現在段階を保存する不変スナップショット。
    /// [KO] 한 번의 도전 식별자·시작 설정·현재 단계를 보관하는 불변 스냅샷입니다.
    /// [EN] Immutable snapshot of a run's identity, starting configuration and current phase.
    /// </summary>
    public sealed class RunState
    {
        /// <summary>
        /// 古い読み込みや通知を区別する挑戦 ID。
        /// [KO] 이전 로딩이나 알림을 구분하는 도전 ID입니다.
        /// [EN] Run ID distinguishing stale loads and notifications.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// 出発時に確定した編成。
        /// [KO] 출발 시 확정한 편성입니다.
        /// [EN] Configuration committed at departure.
        /// </summary>
        public RunConfiguration Configuration { get; }

        /// <summary>
        /// RunController が最後に許可した処理段階。
        /// [KO] RunController가 마지막으로 허용한 처리 단계입니다.
        /// [EN] Processing phase last permitted by RunController.
        /// </summary>
        public Define.RunPhase Phase { get; }

        /// <summary>
        /// 有効な識別子と設定から現在段階の値を作ります。
        /// [KO] 유효한 식별자와 설정으로 현재 단계 값을 만듭니다.
        /// [EN] Creates a phase snapshot from a valid identifier and configuration.
        /// </summary>
        public RunState(Guid id, RunConfiguration configuration, Define.RunPhase phase)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Run ID must not be empty.", nameof(id));

            if ((uint)phase > (uint)Define.RunPhase.RewardSelection)
                throw new ArgumentOutOfRangeException(nameof(phase));

            Id = id;
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Phase = phase;
        }
    }
}
