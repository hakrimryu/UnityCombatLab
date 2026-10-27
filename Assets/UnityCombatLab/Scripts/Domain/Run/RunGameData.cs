using System;
using System.Collections.Generic;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 五ウェーブ・六強化・六役割の開始設定を一 Run 用に固定します。
    /// [KO] 다섯 웨이브·여섯 강화·여섯 역할의 시작 설정을 한 Run용으로 고정합니다.
    /// [EN] Freezes five waves, six upgrades and six role templates for one run.
    /// </summary>
    public sealed class RunGameData
    {

        /// <summary>
        /// ウェーブ番号順にコピーした五つの定義。
        /// [KO] 웨이브 번호 순으로 복사한 다섯 정의입니다.
        /// [EN] Five definitions copied in wave order.
        /// </summary>
        private readonly WaveData[] _waves;

        /// <summary>
        /// 強化種類の番号で検索する六つの定義。
        /// [KO] 강화 종류 번호로 조회하는 여섯 정의입니다.
        /// [EN] Six definitions indexed by upgrade kind.
        /// </summary>
        private readonly RewardData[] _rewards = new RewardData[6];

        /// <summary>
        /// 役割番号で検索する六種の基礎能力。
        /// [KO] 역할 번호로 조회하는 여섯 기본 능력입니다.
        /// [EN] Six base-stat templates indexed by role.
        /// </summary>
        private readonly BattleUnitInput[] _roles = new BattleUnitInput[6];

        /// <summary>
        /// 検査済みデータをコピーし、欠けた定義では開始を拒否します。
        /// [KO] 검사한 데이터를 복사하고 누락된 정의가 있으면 시작을 거부합니다.
        /// [EN] Copies validated data and rejects starting when a definition is missing.
        /// </summary>
        public RunGameData(IReadOnlyList<WaveData> waves, IReadOnlyList<RewardData> rewards, IReadOnlyList<BattleUnitInput> roles)
        {
            if (waves == null || waves.Count != Define.WavesPerRun || rewards == null || rewards.Count != _rewards.Length ||
                roles == null || roles.Count != _roles.Length)
            {
                throw new ArgumentException("Run data requires five waves, six upgrades and six roles.");
            }

            _waves = new WaveData[waves.Count];
            for (var index = 0; index < _waves.Length; index++)
                _waves[index] = waves[index] ?? throw new ArgumentException("Missing wave definition.", nameof(waves));

            foreach (var reward in rewards)
            {
                if (reward == null || _rewards[(int)reward.Kind] != null)
                    throw new ArgumentException("Missing or duplicate upgrade definition.", nameof(rewards));

                _rewards[(int)reward.Kind] = reward;
            }

            foreach (var role in roles)
            {
                if (role == null || _roles[(int)role.Role] != null)
                    throw new ArgumentException("Missing or duplicate role template.", nameof(roles));

                if (role.MaxHealth > 1000000 || role.Attack > 100000)
                    throw new ArgumentOutOfRangeException(nameof(roles), "Role stats exceed the game data limits.");

                _roles[(int)role.Role] = role;
            }
        }

        /// <summary>
        /// 1始まりのウェーブ定義を返します。
        /// [KO] 1부터 시작하는 웨이브 정의를 반환합니다.
        /// [EN] Returns a wave definition by its one-based number.
        /// </summary>
        public WaveData GetWave(int wave)
            => wave < 1 || wave > _waves.Length ? throw new ArgumentOutOfRangeException(nameof(wave)) : _waves[wave - 1];

        /// <summary>
        /// 強化の固定定義を返します。
        /// [KO] 강화의 고정 정의를 반환합니다.
        /// [EN] Returns the fixed definition for an upgrade.
        /// </summary>
        public RewardData GetReward(Define.RewardKind kind)
            => (uint)kind >= _rewards.Length ? throw new ArgumentOutOfRangeException(nameof(kind)) : _rewards[(int)kind];

        /// <summary>
        /// 編成・ウェーブ・累積強化から、満 HP の味方と指定枠の敵を作ります。
        /// [KO] 편성·웨이브·누적 강화로 최대 HP 아군과 지정 슬롯의 적을 만듭니다.
        /// [EN] Builds fully healed allies and configured enemy slots from formation, wave and upgrades.
        /// </summary>
        public IReadOnlyList<BattleUnitInput> CreateUnits(RunConfiguration configuration, int wave, RunRewards rewards)
        {
            if (configuration == null || rewards == null)
                throw new ArgumentNullException(configuration == null ? nameof(configuration) : nameof(rewards));

            var definition = GetWave(wave);
            var units = new List<BattleUnitInput>(Define.MaxBattleUnitCount);
            for (var slot = 0; slot < Define.SlotsPerTeam; slot++)
            {
                var hero = _roles[(int)configuration.GetRole((Define.BattleSlot)slot)];
                var health = Scale(hero.MaxHealth, Define.BasisPoints + rewards.GetEffect(Define.RewardKind.Vitality));
                var attack = Scale(hero.Attack, Define.BasisPoints + rewards.GetEffect(Define.RewardKind.Power));
                var haste = Define.BasisPoints + rewards.GetEffect(Define.RewardKind.Haste);
                var interval = Math.Max(Define.MinimumAttackIntervalTicks,
                    (int)(((long)hero.IntervalTicks * Define.BasisPoints + haste - 1) / haste));
                // 枠0/1/2の味方IDは1/3/5、敵IDは2/4/6。空き枠があってもIDを詰めない。
                units.Add(new BattleUnitInput
                (slot * Define.BattleTeamCount + 1,
                    Define.BattleTeam.Ally,
                    (Define.BattleSlot)slot,
                    hero.Role,
                    health,
                    attack,
                    interval,
                    Scale(health, rewards.GetEffect(Define.RewardKind.Barrier))));

                var enemy = definition.GetEnemy((Define.BattleSlot)slot);
                if (enemy == Define.WaveEnemy.Empty)
                    continue;

                var role = enemy switch
                {
                    Define.WaveEnemy.Empty => throw new InvalidOperationException("Empty slots must be skipped before resolving a role."),
                    Define.WaveEnemy.Melee => Define.UnitRole.SkeletonMelee,
                    Define.WaveEnemy.Ranged => Define.UnitRole.SkeletonRanged,
                    Define.WaveEnemy.Captain => Define.UnitRole.SkeletonCaptain,
                    _ => throw new ArgumentOutOfRangeException(nameof(wave))
                };
                var enemyHealth = definition.EnemyHealth;
                var enemyAttack = definition.EnemyAttack;
                if (configuration.Difficulty == Define.Difficulty.Easy)
                {
                    // 表示と同じ百分率を計算用の basis point に換算する（80% → 8000）。
                    var easyMultiplierBp = Define.EasyEnemyStatPercent * Define.BasisPoints / Define.PercentScale;
                    enemyHealth = Math.Max(1, Scale(enemyHealth, easyMultiplierBp));
                    enemyAttack = Math.Max(1, Scale(enemyAttack, easyMultiplierBp));
                }

                // 難易度を整数化した後で、遠距離HP75%・隊長HP250%/攻撃180%を切り捨てる。
                var healthMultiplier = role switch
                {
                    Define.UnitRole.SkeletonRanged => 7500,
                    Define.UnitRole.SkeletonCaptain => 25000,
                    _ => Define.BasisPoints
                };
                enemyHealth = Math.Max(1, Scale(enemyHealth, healthMultiplier));
                enemyAttack = Math.Max(1, Scale(enemyAttack, role == Define.UnitRole.SkeletonCaptain ? 18000 : 10000));
                units.Add(new BattleUnitInput
                (slot * Define.BattleTeamCount + 2,
                    Define.BattleTeam.Enemy,
                    (Define.BattleSlot)slot,
                    role,
                    enemyHealth,
                    enemyAttack,
                    _roles[(int)role].IntervalTicks));
            }

            return units.AsReadOnly();
        }

        /// <summary>
        /// 整数倍率を long で計算し、端数切り捨てと結果範囲を保証します。
        /// [KO] 정수 배율을 long으로 계산하고 소수 내림·결과 범위를 보장합니다.
        /// [EN] Multiplies in long arithmetic, floors the fraction and checks the result range.
        /// </summary>
        private static int Scale(int value, int multiplierBp)
            => checked((int)((long)value * multiplierBp / Define.BasisPoints));
    }
}
