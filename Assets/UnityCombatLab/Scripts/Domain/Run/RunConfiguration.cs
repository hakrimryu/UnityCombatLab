using System;
using System.Collections.Generic;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 出発時の三人の配置・難易度・シードを変更できない値として保存します。
    /// [KO] 출발 시 세 영웅의 배치·난이도·시드를 변경할 수 없는 값으로 보관합니다.
    /// [EN] Stores departure formation, difficulty and seed as immutable values.
    /// </summary>
    public sealed class RunConfiguration
    {
        /// <summary>
        /// 前列・後列上・後列下の順で保存する三人。呼出元の配列とは共有しません。
        /// [KO] 전열·위쪽 후열·아래쪽 후열 순서의 세 영웅이며 호출자 배열과 공유하지 않습니다.
        /// [EN] Three heroes in front, upper-back and lower-back order, copied from the caller.
        /// </summary>
        private readonly Define.UnitRole[] _roles;

        /// <summary>
        /// 敵の能力値を選ぶ難易度。
        /// [KO] 적 능력치를 선택할 난이도입니다.
        /// [EN] Difficulty selecting enemy stats.
        /// </summary>
        public Define.Difficulty Difficulty { get; }

        /// <summary>
        /// この挑戦の再現に使うシード。
        /// [KO] 이번 도전을 재현할 때 사용할 시드입니다.
        /// [EN] Seed retained for reproducing this run.
        /// </summary>
        public uint Seed { get; }

        /// <summary>
        /// 三種類の英雄が一度ずつ存在することを確認し、入力をコピーします。
        /// [KO] 세 종류 영웅이 한 번씩 존재하는지 확인하고 입력을 복사합니다.
        /// [EN] Validates that each of the three heroes occurs once and copies the input.
        /// </summary>
        public RunConfiguration(IReadOnlyList<Define.UnitRole> roles, Define.Difficulty difficulty, uint seed = Define.DefaultRunSeed)
        {
            if (roles == null || roles.Count != Define.SlotsPerTeam)
                throw new ArgumentException("Formation requires three heroes.", nameof(roles));

            if (difficulty != Define.Difficulty.Standard && difficulty != Define.Difficulty.Easy)
                throw new ArgumentOutOfRangeException(nameof(difficulty));

            _roles = new Define.UnitRole[Define.SlotsPerTeam];
            var seen = 0;

            for (var slot = 0; slot < _roles.Length; slot++)
            {
                var role = roles[slot];
                if ((uint)role > (uint)Define.UnitRole.Mage || (seen & (1 << (int)role)) != 0)
                    throw new ArgumentException("Formation must contain knight, ranger and mage exactly once.", nameof(roles));

                // 騎士/弓使い/魔法使いをビット1/2/4で記録し、同じ英雄を二枠へ配置させない。
                seen |= 1 << (int)role;
                _roles[slot] = role;
            }

            Difficulty = difficulty;
            Seed = seed == 0 ? Define.DefaultRunSeed : seed;
        }

        /// <summary>
        /// 指定した配置枠にいる英雄を返します。
        /// [KO] 지정한 배치 슬롯의 영웅을 반환합니다.
        /// [EN] Returns the hero occupying the requested slot.
        /// </summary>
        public Define.UnitRole GetRole(Define.BattleSlot slot)
        {
            if ((uint)slot >= _roles.Length)
                throw new ArgumentOutOfRangeException(nameof(slot));

            return _roles[(int)slot];
        }

        /// <summary>
        /// 選択した英雄と移動先の英雄を交換した、新しい編成を返します。
        /// [KO] 선택한 영웅과 이동할 슬롯의 영웅을 교환한 새 편성을 반환합니다.
        /// [EN] Returns a new formation swapping the selected hero with the destination hero.
        /// </summary>
        public RunConfiguration Swap(Define.UnitRole role, Define.BattleSlot destination)
        {
            if ((uint)role > (uint)Define.UnitRole.Mage)
                throw new ArgumentOutOfRangeException(nameof(role));

            if ((uint)destination >= _roles.Length)
                throw new ArgumentOutOfRangeException(nameof(destination));

            var roles = (Define.UnitRole[])_roles.Clone();
            var source = Array.IndexOf(roles, role);
            roles[source] = roles[(int)destination];
            roles[(int)destination] = role;

            return new RunConfiguration(roles, Difficulty, Seed);
        }

        /// <summary>
        /// 配置を保ったまま難易度を変更した、新しい開始設定を返します。
        /// [KO] 배치를 유지하면서 난이도를 변경한 새 시작 설정을 반환합니다.
        /// [EN] Returns a new configuration changing difficulty while preserving formation.
        /// </summary>
        public RunConfiguration WithDifficulty(Define.Difficulty difficulty)
            => new(_roles, difficulty, Seed);

        /// <summary>
        /// 騎士を前列、弓使いを後列上、魔法使いを後列下に置く初期編成を作ります。
        /// [KO] 기사 전열·궁수 위쪽 후열·마법사 아래쪽 후열의 초기 편성을 만듭니다.
        /// [EN] Creates the default knight-front, ranger-upper and mage-lower formation.
        /// </summary>
        public static RunConfiguration CreateDefault()
            => new(new[]
            {
                Define.UnitRole.Knight, Define.UnitRole.Ranger, Define.UnitRole.Mage
            }, Define.Difficulty.Standard);
    }
}
