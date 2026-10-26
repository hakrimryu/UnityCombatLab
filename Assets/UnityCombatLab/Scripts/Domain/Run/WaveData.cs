using System;
using System.Collections.Generic;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 一ウェーブの敵配置と能力・報酬倍率を変更不能な値として保存します。
    /// [KO] 한 웨이브의 적 배치·능력·보상 배율을 불변 값으로 보관합니다.
    /// [EN] Stores immutable enemy slots, base stats and reward strength for one wave.
    /// </summary>
    public sealed class WaveData
    {
        /// <summary>
        /// Front、BackUpper、BackLower 順の敵役割。
        /// [KO] Front, BackUpper, BackLower 순서의 적 역할입니다.
        /// [EN] Enemy roles ordered Front, BackUpper, BackLower.
        /// </summary>
        private readonly Define.WaveEnemy[] _enemies;

        /// <summary>
        /// 役割係数を掛ける前の敵 HP。
        /// [KO] 역할 계수를 곱하기 전 적 HP입니다.
        /// [EN] Enemy health before role coefficients.
        /// </summary>
        public int EnemyHealth { get; }

        /// <summary>
        /// 役割係数を掛ける前の敵攻撃力。
        /// [KO] 역할 계수를 곱하기 전 적 공격력입니다.
        /// [EN] Enemy attack before role coefficients.
        /// </summary>
        public int EnemyAttack { get; }

        /// <summary>
        /// 強化の基本増加量へ掛ける倍率。10000 は一倍です。
        /// [KO] 강화 기본 증가량에 곱하는 배율이며 10000은 1배입니다.
        /// [EN] Multiplier for upgrade increments; 10000 denotes one times.
        /// </summary>
        public int RewardStrengthBp { get; }

        /// <summary>
        /// 設定から受けた敵枠と数値を検査し、Run 用にコピーします。
        /// [KO] 설정에서 받은 적 슬롯과 수치를 검사하고 Run용으로 복사합니다.
        /// [EN] Validates configured enemy slots and numbers and copies them for a run.
        /// </summary>
        public WaveData(IReadOnlyList<Define.WaveEnemy> enemies, int enemyHealth, int enemyAttack, int rewardStrengthBp)
        {
            if (enemies == null || enemies.Count != Define.SlotsPerTeam)
                throw new ArgumentException("A wave requires three enemy slots.", nameof(enemies));

            if (enemyHealth < 1 || enemyHealth > 1000000 || enemyAttack < 1 || enemyAttack > 100000 ||
                rewardStrengthBp < 1000 || rewardStrengthBp > 30000)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyHealth), "Wave values exceed the game data limits.");
            }

            _enemies = new Define.WaveEnemy[Define.SlotsPerTeam];
            var hasEnemy = false;
            for (var slot = 0; slot < _enemies.Length; slot++)
            {
                if ((uint)enemies[slot] > (uint)Define.WaveEnemy.Captain)
                    throw new ArgumentOutOfRangeException(nameof(enemies));

                _enemies[slot] = enemies[slot];
                hasEnemy |= enemies[slot] != Define.WaveEnemy.Empty;
            }

            if (!hasEnemy)
                throw new ArgumentException("A playable wave requires at least one enemy.", nameof(enemies));

            EnemyHealth = enemyHealth;
            EnemyAttack = enemyAttack;
            RewardStrengthBp = rewardStrengthBp;
        }

        /// <summary>
        /// 指定した配置枠の敵種別を返します。
        /// [KO] 지정한 배치 슬롯의 적 종류를 반환합니다.
        /// [EN] Returns the enemy kind in a formation slot.
        /// </summary>
        public Define.WaveEnemy GetEnemy(Define.BattleSlot slot)
            => (uint)slot >= _enemies.Length ? throw new ArgumentOutOfRangeException(nameof(slot)) : _enemies[(int)slot];
    }
}
