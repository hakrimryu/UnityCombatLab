using System;
using System.Collections.Generic;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 選択時に確定した増加量を種類ごとに合算する変更不能な強化状態。
    /// [KO] 선택 시 확정한 증가량을 종류별로 합산한 불변 강화 상태입니다.
    /// [EN] Immutable upgrades summing increments committed at selection time by kind.
    /// </summary>
    public sealed class RunRewards
    {
        /// <summary>
        /// 強化なしの開始状態。
        /// [KO] 강화가 없는 시작 상태입니다.
        /// [EN] Starting state without upgrades.
        /// </summary>
        public static RunRewards Empty { get; } = new(Array.Empty<RewardChoice>());

        /// <summary>
        /// 種類番号ごとに数えた選択回数。
        /// [KO] 종류 번호별로 센 선택 횟수입니다.
        /// [EN] Selection counts indexed by kind.
        /// </summary>
        private readonly int[] _stacks = new int[6];

        /// <summary>
        /// 種類番号ごとに合算した確定 basis point。
        /// [KO] 종류 번호별로 합산한 확정 basis point입니다.
        /// [EN] Committed cumulative basis points indexed by kind.
        /// </summary>
        private readonly int[] _effects = new int[6];

        /// <summary>
        /// 選択順の変更不能な履歴。
        /// [KO] 선택 순서의 불변 이력입니다.
        /// [EN] Immutable history in selection order.
        /// </summary>
        public IReadOnlyList<RewardChoice> History { get; }

        /// <summary>
        /// 履歴をコピーし、重複ウェーブと種類ごとの上限を検査します。
        /// [KO] 이력을 복사하고 중복 웨이브와 종류별 상한을 검사합니다.
        /// [EN] Copies history and checks duplicate waves and per-kind stack limits.
        /// </summary>
        public RunRewards(IReadOnlyList<RewardChoice> history)
        {
            if (history == null || history.Count >= Define.WavesPerRun)
                throw new ArgumentException("A run has at most four reward choices.", nameof(history));

            var copy = new RewardChoice[history.Count];
            var previousWave = 0;
            for (var index = 0; index < copy.Length; index++)
            {
                var choice = history[index];
                if (choice.Wave <= previousWave || choice.Wave >= Define.WavesPerRun ||
                    (uint)choice.Kind >= _stacks.Length || choice.IncreaseBp <= 0 || choice.IncreaseBp > 30000)
                {
                    throw new ArgumentException("Invalid reward history.", nameof(history));
                }

                if (++_stacks[(int)choice.Kind] > 3)
                    throw new ArgumentException("An upgrade cannot exceed three stacks.", nameof(history));

                _effects[(int)choice.Kind] = checked(_effects[(int)choice.Kind] + choice.IncreaseBp);
                copy[index] = choice;
                previousWave = choice.Wave;
            }

            History = Array.AsReadOnly(copy);
        }

        /// <summary>
        /// 種類に対応する選択回数を返します。
        /// [KO] 종류에 대응하는 선택 횟수를 반환합니다.
        /// [EN] Returns the number of selections for a kind.
        /// </summary>
        public int GetStacks(Define.RewardKind kind)
            => (uint)kind >= _stacks.Length ? throw new ArgumentOutOfRangeException(nameof(kind)) : _stacks[(int)kind];

        /// <summary>
        /// 種類に対応する累積 basis point を返します。
        /// [KO] 종류에 대응하는 누적 basis point를 반환합니다.
        /// [EN] Returns cumulative basis points for a kind.
        /// </summary>
        public int GetEffect(Define.RewardKind kind)
            => (uint)kind >= _effects.Length ? throw new ArgumentOutOfRangeException(nameof(kind)) : _effects[(int)kind];

        /// <summary>
        /// 現在の状態を変更せず、選択を一つ追加した状態を返します。
        /// [KO] 현재 상태를 바꾸지 않고 선택 하나를 추가한 상태를 반환합니다.
        /// [EN] Returns a state with one additional choice without changing this state.
        /// </summary>
        public RunRewards Add(RewardChoice choice)
        {
            var choices = new RewardChoice[History.Count + 1];
            for (var index = 0; index < History.Count; index++)
                choices[index] = History[index];

            choices[choices.Length - 1] = choice;
            return new RunRewards(choices);
        }
    }
}
