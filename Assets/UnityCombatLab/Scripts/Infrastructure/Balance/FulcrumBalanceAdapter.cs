using System;
using System.Collections.Generic;
using Fulcrum;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// Fulcrum の出力を検査し、敵配置と合わせて Run の固定値に変換します。
    /// [KO] Fulcrum 출력을 검사하고 적 배치와 합쳐 Run의 고정값으로 변환합니다.
    /// [EN] Validates Fulcrum output and combines it with enemy slots into fixed run values.
    /// </summary>
    public static class FulcrumBalanceAdapter
    {
        /// <summary>
        /// ゲームが読む三トラック。配列順は HP・攻撃力・報酬倍率です。
        /// [KO] 게임이 읽는 세 트랙이며 배열 순서는 HP·공격력·보상 배율입니다.
        /// [EN] The game's three tracks, ordered health, attack and reward strength.
        /// </summary>
        private static readonly string[] RequiredTracks = { "enemy_hp", "enemy_attack", "reward_strength" };

        /// <summary>
        /// 五ウェーブ分の値をコピーし、以後の再エクスポートから切り離します。
        /// [KO] 다섯 웨이브 값을 복사해 이후 재내보내기와 분리합니다.
        /// [EN] Copies five waves of values, isolating them from subsequent exports.
        /// </summary>
        public static WaveData[] CreateWaves(BalanceTable table, IReadOnlyList<WaveDefinition> definitions)
        {
            Validate(table);
            if (definitions == null || definitions.Count != Define.WavesPerRun)
                throw new InvalidOperationException("Run balance requires five wave definitions.");

            var waves = new WaveData[Define.WavesPerRun];
            for (var index = 0; index < waves.Length; index++)
            {
                if (definitions[index] == null)
                    throw new InvalidOperationException($"Missing wave definition {index + 1}.");

                var wave = index + 1;
                waves[index] = definitions[index].CreateSnapshot
                (Round(Read(table, RequiredTracks[0], wave, 1, 1000000)),
                    Round(Read(table, RequiredTracks[1], wave, 1, 100000)),
                    Round(Read(table, RequiredTracks[2], wave, 0.1, 3) * Define.BasisPoints));
            }

            return waves;
        }

        /// <summary>
        /// 必須トラックの重複・範囲・値を調べ、不正な入力では出発とエクスポートを拒否します。
        /// [KO] 필수 트랙의 중복·범위·값을 검사하고 잘못된 입력은 출발·내보내기를 거부합니다.
        /// [EN] Checks track uniqueness, coverage and values, rejecting invalid inputs before departure or export.
        /// </summary>
        public static void Validate(BalanceTable table)
        {
            if (table == null)
                throw new InvalidOperationException("Run requires a Fulcrum balance table.");

            var ids = table.TrackIds;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null)
                throw new InvalidOperationException("Fulcrum balance table has no tracks.");

            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    throw new InvalidOperationException("Fulcrum balance table contains an empty or duplicate track id.");
            }

            try
            {
                foreach (var track in RequiredTracks)
                {
                    if (!table.TryGetRange(track, out var min, out var max) || min > 1 || max < Define.WavesPerRun)
                        throw new InvalidOperationException($"Fulcrum track '{track}' must cover waves 1..5.");
                }

                for (var wave = 1; wave <= Define.WavesPerRun; wave++)
                {
                    Read(table, RequiredTracks[0], wave, 1, 1000000);
                    Read(table, RequiredTracks[1], wave, 1, 100000);
                    Read(table, RequiredTracks[2], wave, 0.1, 3);
                }
            }
            catch (Exception exception) when (exception is IndexOutOfRangeException || exception is NullReferenceException)
            {
                throw new InvalidOperationException("Fulcrum balance table storage is incomplete.", exception);
            }
        }

        /// <summary>
        /// 丸める前の値が安全範囲内か確認し、範囲外の小数を丸めて受け入れることを防ぎます。
        /// [KO] 반올림 전 값이 안전 범위인지 확인해 범위 밖 소수를 반올림하여 허용하는 일을 막습니다.
        /// [EN] Checks raw values before rounding so out-of-range fractions cannot round into acceptance.
        /// </summary>
        private static double Read(BalanceTable table, string track, int wave, double min, double max)
        {
            if (!table.TryGet(track, wave, out var value) || double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                throw new InvalidOperationException($"Invalid Fulcrum '{track}' at wave {wave}; expected finite {min}..{max}.");

            return value;
        }

        /// <summary>
        /// HP・攻撃力と basis point を四捨五入します。9.6 は10、1.1倍は11000になります。
        /// [KO] HP·공격력·basis point를 반올림하며 9.6은 10, 1.1배는 11000이 됩니다.
        /// [EN] Rounds stats and basis points away from zero at midpoints: 9.6 becomes 10 and 1.1 times becomes 11000.
        /// </summary>
        private static int Round(double value)
            => checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
    }
}
