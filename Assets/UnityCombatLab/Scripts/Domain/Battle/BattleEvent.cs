using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 攻撃・被害・死亡・終了の内容と発生順を記録するイベント。
    /// [KO] 공격·피해·사망·종료의 내용과 발생 순서를 기록하는 이벤트입니다.
    /// [EN] Event recording attacks, damage, deaths or completion and their occurrence order.
    /// </summary>
    public readonly struct BattleEvent
    {
        /// <summary>
        /// このイベントが属する戦闘の ID。
        /// [KO] 이 이벤트가 속한 전투의 ID입니다.
        /// [EN] ID of the battle this event belongs to.
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
        /// 戦闘内でのイベントの発生順。
        /// [KO] 전투 안에서 이벤트가 발생한 순서입니다.
        /// [EN] Event occurrence order within the battle.
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// 行動したユニットの ID。戦闘終了では 0。
        /// [KO] 행동한 유닛의 ID이며 전투 종료에서는 0입니다.
        /// [EN] Acting unit's ID, or 0 for battle completion.
        /// </summary>
        public int ActorId { get; }

        /// <summary>
        /// 対象ユニットの ID。単一対象を持たないイベントでは 0。
        /// [KO] 대상 유닛의 ID이며 단일 대상이 없는 이벤트에서는 0입니다.
        /// [EN] Target unit's ID, or 0 for events without a single target.
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
        /// 最大 HP と生存条件を適用した実際の回復量。
        /// [KO] 최대 HP·생존 조건을 적용한 실제 회복량입니다.
        /// [EN] Actual healing after maximum-health and survival constraints.
        /// </summary>
        public int HealthRestored { get; }

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
            Define.BattleEventKind kind, int healthDamage, int shieldDamage, Define.BattleResult result, int healthRestored = 0)
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
            HealthRestored = healthRestored;
            Result = result;
        }
    }
}
