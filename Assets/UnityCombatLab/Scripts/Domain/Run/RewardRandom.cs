using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// Run 開始時のシードを保持し、報酬抽選だけに使う xorshift32。
    /// [KO] Run 시작 시드를 보관하고 보상 추첨에만 사용하는 xorshift32입니다.
    /// [EN] Xorshift32 initialized once per run and used only for reward draws.
    /// </summary>
    public sealed class RewardRandom
    {

        /// <summary>
        /// 次の抽選へ引き継ぐ非ゼロの32-bit状態。
        /// [KO] 다음 추첨에 이어 쓸 0이 아닌 32-bit 상태입니다.
        /// [EN] Nonzero 32-bit state retained for the next draw.
        /// </summary>
        private uint _state;

        /// <summary>
        /// ゼロのシードは共通の既定値に置き換えます。
        /// [KO] 0 시드는 공통 기본값으로 바꿉니다.
        /// [EN] Substitutes the common default for a zero seed.
        /// </summary>
        public RewardRandom(uint seed)
            => _state = seed == 0 ? Define.DefaultRunSeed : seed;

        /// <summary>
        /// 上限未満の整数を偏りのない rejection sampling で選びます。
        /// [KO] 상한 미만 정수를 편향 없는 rejection sampling으로 선택합니다.
        /// [EN] Selects an integer below the bound with unbiased rejection sampling.
        /// </summary>
        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));

            // xorshift32 は0を生成しないため、1..uint.MaxValue を0..uint.MaxValue-1へ移す。
            var bound = (uint)exclusiveMaximum;
            var limit = uint.MaxValue - uint.MaxValue % bound;
            uint value;
            do
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                value = _state - 1;
            } while (value >= limit);

            return (int)(value % bound);
        }
    }
}
