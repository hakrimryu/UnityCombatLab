using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 非同期にも渡せる不変イベント値。
    /// [KO] 비동기에도 전달 가능한 불변 이벤트 값.
    /// [EN] Immutable event value safe to copy for asynchronous use.
    /// </summary>
    public readonly struct BattleEvent
    {
        /// <summary>
        /// 挑戦識別子。
        /// [KO] 도전 식별자.
        /// [EN] Run identifier.
        /// </summary>
        public Guid RunId { get; }

        /// <summary>
        /// ウェーブ番号。
        /// [KO] 웨이브 번호.
        /// [EN] Wave number.
        /// </summary>
        public int Wave { get; }

        /// <summary>
        /// 発生ティック。
        /// [KO] 발생 틱.
        /// [EN] Emission tick.
        /// </summary>
        public int Tick { get; }

        /// <summary>
        /// 戦闘内の単調連番。
        /// [KO] 전투 내 단조 증가 순번.
        /// [EN] Monotonic sequence within battle.
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// 行動者。終了時は0。
        /// [KO] 행동자, 종료 시 0.
        /// [EN] Actor; zero for battle end.
        /// </summary>
        public int ActorId { get; }

        /// <summary>
        /// 対象。全体イベントは0。
        /// [KO] 대상, 전체 이벤트는 0.
        /// [EN] Target; zero for aggregate events.
        /// </summary>
        public int TargetId { get; }

        /// <summary>
        /// イベント種別。
        /// [KO] 이벤트 종류.
        /// [EN] Event kind.
        /// </summary>
        public Define.BattleEventKind Kind { get; }

        /// <summary>
        /// 実際の HP 減少。
        /// [KO] 실제 HP 감소량.
        /// [EN] Actual health loss.
        /// </summary>
        public int HealthDamage { get; }

        /// <summary>
        /// 実際の防御膜吸収。
        /// [KO] 실제 방어막 흡수량.
        /// [EN] Actual shield absorption.
        /// </summary>
        public int ShieldDamage { get; }

        /// <summary>
        /// 発生時の戦闘結果。
        /// [KO] 발생 시 전투 결과.
        /// [EN] Battle result at emission.
        /// </summary>
        public Define.BattleResult Result { get; }

        /// <summary>
        /// 順序と被害を値として保存します。
        /// [KO] 순서와 피해를 값으로 보존합니다.
        /// [EN] Stores ordering and damage as values.
        /// </summary>
        internal BattleEvent(Guid runId, int wave, int tick, int sequence, int actorId, int targetId,
            Define.BattleEventKind kind, int healthDamage, int shieldDamage, Define.BattleResult result)
        {
            RunId = runId;
            Wave = wave;
            Tick = tick;
            Sequence = sequence;
            ActorId = actorId;
            TargetId = targetId;
            Kind = kind;
            HealthDamage = healthDamage;
            ShieldDamage = shieldDamage;
            Result = result;
        }
    }
}
