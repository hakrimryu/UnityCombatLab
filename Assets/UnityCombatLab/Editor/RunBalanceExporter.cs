using System;
using System.Linq;
using Fulcrum;
using Fulcrum.Editor.Export;
using UnityCombatLab.Common;
using UnityCombatLab.Infrastructure.Balance;
using UnityEditor;
using UnityEngine;

namespace UnityCombatLab.Editor
{
    /// <summary>
    /// ゲーム用 Profile の wave 軸を確認してから、参照を保ったまま Table を再出力します。
    /// [KO] 게임용 Profile의 wave 축을 확인한 뒤 참조를 유지하며 Table을 다시 출력합니다.
    /// [EN] Checks the wave axis of game profiles and re-exports tables while preserving references.
    /// </summary>
    public static class RunBalanceExporter
    {
        /// <summary>
        /// ゲーム用 Profile と Table の保存先。
        /// [KO] 게임용 Profile·Table의 저장 위치입니다.
        /// [EN] Asset directory for the game's profiles and tables.
        /// </summary>
        private const string BalanceDirectory = "Assets/UnityCombatLab/Data/Balance/";

        /// <summary>
        /// 両方の Profile を先に検査し、片方の不正で既存出力の一部だけが変わることを防ぎます。
        /// [KO] 두 Profile을 먼저 검사해 한쪽 오류로 기존 출력 일부만 바뀌는 일을 막습니다.
        /// [EN] Validates both profiles first so an invalid profile cannot leave only one output changed.
        /// </summary>
        [MenuItem("Tools/UnityCombatLab/Export Run Balance Tables")]
        public static void ExportTables()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Export run balance tables outside Play mode.");

            var standard = LoadProfile("Standard");
            var easy = LoadProfile("Easy");
            ValidateProfiles(standard, easy);
            ProfileExporter.ExportTable(standard, BalanceDirectory + "BalanceTable_Standard.asset");
            ProfileExporter.ExportTable(easy, BalanceDirectory + "BalanceTable_Easy.asset");
        }

        /// <summary>
        /// 保存先を変更せず、両 Profile の軸・値・難易度比率をすべて検査します。
        /// [KO] 저장 대상을 변경하지 않고 두 Profile의 축·값·난이도 비율을 모두 검사합니다.
        /// [EN] Validates both profiles' axes, values and difficulty ratio without changing export destinations.
        /// </summary>
        public static void ValidateProfiles(BalanceProfile standard, BalanceProfile easy)
        {
            ValidateProfile(standard);
            ValidateProfile(easy);
            ValidateDifficultyPair(standard, easy);
        }

        /// <summary>
        /// 軸名は Table に残らないため、Profile 側で wave の1〜5と三トラックの対応を検査します。
        /// [KO] 축 이름은 Table에 남지 않으므로 Profile에서 wave 1~5·세 트랙의 대응을 검사합니다.
        /// [EN] Checks wave 1..5 and all three track bindings on the profile because tables do not retain axis names.
        /// </summary>
        public static void ValidateProfile(BalanceProfile profile)
        {
            if (profile == null || profile.Axes.Count(axis => axis != null && axis.Id == "wave") != 1 ||
                !profile.TryGetAxis("wave", out var waveAxis) || waveAxis.Min != 1 || waveAxis.Max != Define.WavesPerRun)
            {
                throw new InvalidOperationException("Run profile requires one wave axis with integer positions 1..5.");
            }

            foreach (var id in new[] { "enemy_hp", "enemy_attack", "reward_strength" })
            {
                if (!profile.TryGetTrack(id, out var track) || track.AxisId != "wave" || track.RangeMin > 1 ||
                    track.RangeMax < Define.WavesPerRun || !track.HasModel)
                {
                    throw new InvalidOperationException($"Run profile requires '{id}' on the wave axis covering 1..5 with a model.");
                }
            }

            var preview = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                // 保存済み Table に触る前に、ゲームの安全範囲も全五ウェーブで検査する。
                BalanceTableBaker.Bake(profile, preview);
                FulcrumBalanceAdapter.Validate(preview);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        /// <summary>
        /// 明示されたゲーム用 Profile を読み、未設定なら処理を止めます。
        /// [KO] 명시된 게임용 Profile을 읽고 미설정이면 처리를 중단합니다.
        /// [EN] Loads an explicit game profile and stops if it is missing.
        /// </summary>
        private static BalanceProfile LoadProfile(string difficulty)
            => AssetDatabase.LoadAssetAtPath<BalanceProfile>(BalanceDirectory + "BalanceProfile_" + difficulty + ".asset")
               ?? throw new InvalidOperationException($"Missing {difficulty} run balance profile.");

        /// <summary>
        /// 両 Profile の全ウェーブを比較し、敵80%・報酬同値の難易度契約を出力前に確認します。
        /// [KO] 두 Profile의 전체 웨이브를 비교해 적80%·보상 동일의 난이도 계약을 출력 전에 확인합니다.
        /// [EN] Compares both profiles across every wave to check eighty-percent enemy stats and identical rewards before export.
        /// </summary>
        private static void ValidateDifficultyPair(BalanceProfile standard, BalanceProfile easy)
        {
            var standardTable = ScriptableObject.CreateInstance<BalanceTable>();
            var easyTable = ScriptableObject.CreateInstance<BalanceTable>();
            try
            {
                BalanceTableBaker.Bake(standard, standardTable);
                BalanceTableBaker.Bake(easy, easyTable);
                foreach (var id in new[] { "enemy_hp", "enemy_attack", "reward_strength" })
                {
                    for (var wave = 1; wave <= Define.WavesPerRun; wave++)
                    {
                        standardTable.TryGet(id, wave, out var standardValue);
                        easyTable.TryGet(id, wave, out var easyValue);
                        var expected = id == "reward_strength" ? standardValue : standardValue * Define.EasyEnemyStatPercent / Define.PercentScale;
                        // 9.6 と9.600000000000001のような double 演算の端数だけを許容する。
                        if (Math.Abs(easyValue - expected) > 1e-12 * Math.Max(1, Math.Abs(expected)))
                            throw new InvalidOperationException($"Easy profile '{id}' at wave {wave} must match the difficulty ratio.");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(standardTable);
                UnityEngine.Object.DestroyImmediate(easyTable);
            }
        }
    }
}
