namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 状態を所有しない値コピー。
    /// [KO] 상태 소유권 없는 값 복사본.
    /// [EN] Value copy without mutable state ownership.
    /// </summary>
    public readonly struct BattleUnitSnapshot
    {
        /// <summary>
        /// 元の不変入力。
        /// [KO] 원본 불변 입력.
        /// [EN] Original immutable input.
        /// </summary>
        public BattleUnitInput Input { get; }

        /// <summary>
        /// 現在 HP。
        /// [KO] 현재 HP.
        /// [EN] Current health.
        /// </summary>
        public int Health { get; }

        /// <summary>
        /// 現在の防御膜。
        /// [KO] 현재 방어막.
        /// [EN] Current shield.
        /// </summary>
        public int Shield { get; }

        /// <summary>
        /// 実行済み攻撃数。
        /// [KO] 실제 실행한 공격 수.
        /// [EN] Executed attack count.
        /// </summary>
        public int AttackCount { get; }

        /// <summary>
        /// 次の攻撃ティック。
        /// [KO] 다음 공격 틱.
        /// [EN] Next attack tick.
        /// </summary>
        public int NextAttackTick { get; }

        /// <summary>
        /// 生存状態。
        /// [KO] 생존 상태.
        /// [EN] Whether alive.
        /// </summary>
        public bool IsAlive => Health > 0;

        /// <summary>
        /// シミュレーションの状態をコピーします。
        /// [KO] 시뮬레이션 상태를 복사합니다.
        /// [EN] Copies simulation state.
        /// </summary>
        internal BattleUnitSnapshot(BattleUnitInput input, int health, int shield, int attackCount, int nextAttackTick)
        {
            Input = input;
            Health = health;
            Shield = shield;
            AttackCount = attackCount;
            NextAttackTick = nextAttackTick;
        }
    }
}
