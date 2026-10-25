using System;
using System.Collections.Generic;
using System.Linq;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 出発時の配置と難易度から六人の開始能力を作り、各配置に使う元のユニット番号も保存します。
    /// [KO] 출발 배치와 난이도로 여섯 명의 시작 능력을 만들고 각 배치에 사용할 원본 유닛 번호도 보관합니다.
    /// [EN] Builds six starting units from departure formation and difficulty, retaining the source unit index for each slot.
    /// </summary>
    public sealed class RunBattleSetup
    {
        /// <summary>
        /// 配置先の番号から元のモデルを選ぶ対応表。能力とモデルを必ず同じ英雄から取得します。
        /// [KO] 배치 위치 번호로 원본 모델을 선택하는 대응표이며 능력과 모델을 항상 같은 영웅에서 가져옵니다.
        /// [EN] Maps destination indices to source models so stats and visuals always come from the same hero.
        /// </summary>
        private readonly int[] _sourceIndices = new int[Define.MaxBattleUnitCount];

        /// <summary>
        /// 味方/敵の前列、後列上、後列下の順で Runner に渡す開始データ。
        /// [KO] 아군/적 전열, 위쪽 후열, 아래쪽 후열 순서로 Runner에 전달할 시작 데이터입니다.
        /// [EN] Starting inputs for the runner, ordered ally/enemy front, upper-back, then lower-back.
        /// </summary>
        public IReadOnlyList<BattleUnitInput> Units { get; }

        /// <summary>
        /// 六枠の原型を確認し、英雄の交換と敵の難易度倍率を新しい値へ適用します。
        /// [KO] 여섯 슬롯의 원형을 확인하고 영웅 교환과 적 난이도 배율을 새 값에 적용합니다.
        /// [EN] Validates six source slots and applies hero swaps and enemy difficulty scaling to fresh inputs.
        /// </summary>
        public RunBattleSetup(RunConfiguration configuration, IReadOnlyList<BattleUnitInput> originals)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (originals == null || originals.Count != Define.MaxBattleUnitCount)
                throw new ArgumentException("A run requires all six source slots.", nameof(originals));

            if (originals.Where((input, index) =>
                    input == null || input.Team != (Define.BattleTeam)(index % Define.BattleTeamCount) ||
                    input.Slot != (Define.BattleSlot)(index / Define.BattleTeamCount)).Any())
            {
                throw new ArgumentException("Source units must follow interleaved team and slot order.", nameof(originals));
            }

            var units = new BattleUnitInput[Define.MaxBattleUnitCount];

            for (var index = 0; index < units.Length; index++)
            {
                // 0/1 は味方/敵の前列、2/3 は後列上、4/5 は後列下。ID は配置先の番号に1を足す。
                var team = (Define.BattleTeam)(index % Define.BattleTeamCount);
                var slot = (Define.BattleSlot)(index / Define.BattleTeamCount);
                var source = team == Define.BattleTeam.Ally ? FindHero(originals, configuration.GetRole(slot)) : index;
                _sourceIndices[index] = source;

                var original = originals[source];
                var isEasyEnemy = team == Define.BattleTeam.Enemy && configuration.Difficulty == Define.Difficulty.Easy;
                units[index] = new BattleUnitInput
                (index + 1,
                    team,
                    slot,
                    original.Role,
                    ScaleEnemyStat(original.MaxHealth, isEasyEnemy),
                    ScaleEnemyStat(original.Attack, isEasyEnemy),
                    original.IntervalTicks,
                    original.Shield);
            }

            Units = Array.AsReadOnly(units);
        }

        /// <summary>
        /// 配置先の番号に対応する原型番号を返し、呼出元が同じ英雄の View を移動できるようにします。
        /// [KO] 배치 위치에 대응하는 원본 번호를 반환하여 호출자가 같은 영웅의 View를 옮길 수 있게 합니다.
        /// [EN] Returns the source index for a destination so the caller can move the matching hero view.
        /// </summary>
        public int GetSourceIndex(int destination)
            => (uint)destination >= _sourceIndices.Length ? throw new ArgumentOutOfRangeException(nameof(destination)) : _sourceIndices[destination];

        /// <summary>
        /// 指定した英雄が味方の原型に一人だけ存在することを確認して、その番号を返します。
        /// [KO] 지정 영웅이 아군 원형에 한 명만 있는지 확인하고 그 번호를 반환합니다.
        /// [EN] Finds the sole ally template for the requested hero, rejecting missing or duplicate heroes.
        /// </summary>
        private static int FindHero(IReadOnlyList<BattleUnitInput> originals, Define.UnitRole role)
        {
            var found = -1;

            for (var index = 0; index < originals.Count; index += Define.BattleTeamCount)
            {
                if (originals[index].Role != role)
                    continue;

                if (found >= 0)
                    throw new ArgumentException("Each hero requires one source unit.", nameof(originals));

                found = index;
            }

            return found >= 0 ? found : throw new ArgumentException("A formation hero has no source unit.", nameof(originals));
        }

        /// <summary>
        /// やさしい敵の HP と攻撃力を整数の百分率で切り捨て、開始値の下限1を維持します。
        /// [KO] 쉬움 적의 HP와 공격력을 정수 백분율로 내림하고 시작값 하한 1을 유지합니다.
        /// [EN] Floors easy enemy health and attack by the percentage rule, retaining the starting minimum of one.
        /// </summary>
        private static int ScaleEnemyStat(int value, bool isEasyEnemy)
            => isEasyEnemy
                ? Math.Max(1, (int)((long)value * Define.EasyEnemyStatPercent / Define.PercentScale))
                : value;
    }
}
