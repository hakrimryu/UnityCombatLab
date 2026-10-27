using System;
using System.Collections.Generic;
using System.Linq;
using Fulcrum;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Editor;
using UnityCombatLab.Infrastructure.Balance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// 実際の Profile・Table とゲームコアで、数値変換・拒否・Run と replay の固定入力を確認します。
    /// [KO] 실제 Profile·Table·게임 코어로 수치 변환·거부·Run 및 replay의 고정 입력을 확인합니다.
    /// [EN] Uses real profiles, tables and the game core to check conversion, rejection and fixed run/replay inputs.
    /// </summary>
    public sealed class FulcrumBalanceAdapterTests
    {
        /// <summary>
        /// テストが複製したオブジェクトだけを終了時に破棄します。
        /// [KO] 테스트가 복제한 객체만 종료 시 파괴합니다.
        /// [EN] Only the objects cloned by the test are destroyed during teardown.
        /// </summary>
        private readonly List<Object> _owned = new();

        /// <summary>
        /// 保存済みゲーム資産を変更せず、一時 Profile・Table・Run 定義を解放します。
        /// [KO] 저장된 게임 에셋을 변경하지 않고 임시 Profile·Table·Run 정의를 해제합니다.
        /// [EN] Releases temporary profiles, tables and run definitions without changing saved game assets.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var item in _owned)
                Object.DestroyImmediate(item);

            _owned.Clear();
        }

        /// <summary>
        /// 保存済みの両難易度を読み、全ウェーブの整数値と役割適用前の値を照合します。
        /// [KO] 저장된 두 난이도를 읽고 전체 웨이브 정수값·역할 적용 전 값을 대조합니다.
        /// [EN] Checks all waves of both saved difficulties against the expected pre-role integer values.
        /// </summary>
        [TestCase(Define.Difficulty.Standard)]
        [TestCase(Define.Difficulty.Easy)]
        public void SavedTablesSupplyEveryWaveWithoutAnotherDifficultyScale(Define.Difficulty difficulty)
        {
            var configuration = RunConfiguration.CreateDefault().WithDifficulty(difficulty);
            var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/UnityCombatLab/Data/RunDef_Default.asset");
            var data = definition.CreateSnapshot(LoadRoles(), configuration);
            var health = difficulty == Define.Difficulty.Easy ? new[] { 336, 400, 480, 560, 640 } : new[] { 420, 500, 600, 700, 800 };
            var attack = difficulty == Define.Difficulty.Easy ? new[] { 10, 11, 14, 16, 19 } : new[] { 12, 14, 17, 20, 24 };
            for (var wave = 1; wave <= Define.WavesPerRun; wave++)
            {
                var value = data.GetWave(wave);
                Assert.That((value.EnemyHealth, value.EnemyAttack, value.RewardStrengthBp),
                    Is.EqualTo((health[wave - 1], attack[wave - 1], 10000 + (wave - 1) * 1000)));
                var front = data.CreateUnits(configuration, wave, RunRewards.Empty)
                    .Single(unit => unit.Team == Define.BattleTeam.Enemy && unit.Slot == Define.BattleSlot.Front);
                Assert.That(front.MaxHealth, Is.EqualTo(wave == 5 ? health[wave - 1] * 25000 / 10000 : health[wave - 1]));
                Assert.That(front.Attack, Is.EqualTo(wave == 5 ? attack[wave - 1] * 18000 / 10000 : attack[wave - 1]));
            }

            Assert.Throws<ArgumentException>(() => data.CreateUnits(configuration.WithDifficulty(
                difficulty == Define.Difficulty.Easy ? Define.Difficulty.Standard : Define.Difficulty.Easy), 1, RunRewards.Empty));
        }

        /// <summary>
        /// 実測済み M5 の標準入力と選択に対し、tick・全イベント・replay の回帰基準を維持します。
        /// [KO] 실측한 M5 표준 입력·선택에서 tick·전체 이벤트·replay의 회귀 기준을 유지합니다.
        /// [EN] Preserves the measured M5 tick, event and replay baseline for its standard inputs and choices.
        /// </summary>
        [Test]
        public void StandardOutputPreservesMeasuredM5Result()
        {
            var configuration = RunConfiguration.CreateDefault().Swap(Define.UnitRole.Mage, Define.BattleSlot.Front);
            var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/UnityCombatLab/Data/RunDef_Default.asset");
            var record = Record(definition.CreateSnapshot(LoadRoles(), configuration), configuration);
            Assert.That(record.Result.Outcome, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(record.Result.Ticks, Is.EqualTo(1090));
            Assert.That(record.Events.Count, Is.EqualTo(455));
            Assert.That(RunReplayVerifier.Hash(record.Events), Is.EqualTo(0x3F80450CE314E9DBUL));
            Assert.That(RunReplayVerifier.Verify(record).IsMatch, Is.True);
        }

        /// <summary>
        /// Profile の再出力は次の Run だけを変え、既存 Run と記録の値・結果を保持します。
        /// [KO] Profile 재출력은 다음 Run만 바꾸며 기존 Run·기록의 값과 결과를 유지합니다.
        /// [EN] Re-exporting a profile changes the next run while retaining existing run and replay values and results.
        /// </summary>
        [TestCase(Define.Difficulty.Standard)]
        [TestCase(Define.Difficulty.Easy)]
        public void ReexportChangesNextRunButKeepsActiveSnapshotAndReplay(Define.Difficulty difficulty)
        {
            var profile = CloneProfile(difficulty);
            var table = Bake(profile);
            var definition = CloneDefinition(table, difficulty);
            var configuration = RunConfiguration.CreateDefault().WithDifficulty(difficulty).Swap(Define.UnitRole.Mage, Define.BattleSlot.Front);
            var current = definition.CreateSnapshot(LoadRoles(), configuration);
            var record = Record(current, configuration);
            var hash = table.SourceProfileHash;
            profile.TryGetTrack("enemy_hp", out var hp);
            ((TableModel)hp.Model).Values = Enumerable.Repeat(1000000d, 5).ToArray();
            BalanceTableBaker.Bake(profile, table);
            var next = definition.CreateSnapshot(LoadRoles(), configuration);
            var changed = Record(next, configuration);

            Assert.That(table.SourceProfileHash, Is.Not.EqualTo(hash));
            Assert.That(current.GetWave(1).EnemyHealth, Is.EqualTo(difficulty == Define.Difficulty.Easy ? 336 : 420));
            Assert.That(next.GetWave(1).EnemyHealth, Is.EqualTo(1000000));
            // 第1ウェーブのHP100万は制限1800tickで倒せず、生存者がいるため時間切れになる。
            Assert.That(changed.Result.Outcome, Is.EqualTo(Define.BattleResult.Timeout));
            Assert.That(changed.Result.Ticks, Is.EqualTo(Define.BattleTickLimit));
            Assert.That(record.Result.Outcome, Is.EqualTo(Define.BattleResult.Victory));
            Assert.That(RunReplayVerifier.Hash(changed.Events), Is.Not.EqualTo(RunReplayVerifier.Hash(record.Events)));
            Assert.That(RunReplayVerifier.Verify(record).IsMatch, Is.True);
            Assert.That(RunReplayVerifier.Hash(Record(current, configuration).Events), Is.EqualTo(RunReplayVerifier.Hash(record.Events)));
        }

        /// <summary>
        /// 小数の中間値はゼロから離れる方向へ丸め、役割係数の前に整数化します。
        /// [KO] 소수 중간값을 0에서 먼 방향으로 반올림하고 역할 계수 전에 정수화합니다.
        /// [EN] Rounds midpoint fractions away from zero before applying role coefficients.
        /// </summary>
        [Test]
        public void MidpointsRoundAwayFromZero()
        {
            var profile = CloneProfile(Define.Difficulty.Standard);
            profile.TryGetTrack("enemy_hp", out var hp);
            profile.TryGetTrack("enemy_attack", out var attack);
            ((TableModel)hp.Model).Values[0] = 420.5;
            ((TableModel)attack.Model).Values[0] = 12.5;
            var data = CloneDefinition(Bake(profile)).CreateSnapshot(LoadRoles(), RunConfiguration.CreateDefault());
            Assert.That(data.GetWave(1).EnemyHealth, Is.EqualTo(421));
            Assert.That(data.GetWave(1).EnemyAttack, Is.EqualTo(13));
        }

        /// <summary>
        /// Table から分からない軸名と難易度比率の不正を、保存前の Editor 検査で拒否します。
        /// [KO] Table에서 알 수 없는 축 이름·난이도 비율 오류를 저장 전 Editor 검사에서 거부합니다.
        /// [EN] Rejects invalid axis names and difficulty ratios through the Editor check before saving tables.
        /// </summary>
        [TestCase("axis")]
        [TestCase("axis-range")]
        [TestCase("track-axis")]
        [TestCase("enemy-ratio")]
        [TestCase("reward-ratio")]
        public void EditorRejectsInvalidProfileContract(string defect)
        {
            var standard = CloneProfile(Define.Difficulty.Standard);
            var easy = CloneProfile(Define.Difficulty.Easy);
            standard.TryGetTrack("enemy_hp", out var hp);
            easy.TryGetTrack("reward_strength", out var reward);
            switch (defect)
            {
                case "axis":
                    standard.Axes[0].Id = "level";
                    break;
                case "axis-range":
                    standard.Axes[0].Max = 4;
                    break;
                case "track-axis":
                    hp.AxisId = "level";
                    break;
                case "enemy-ratio":
                    ((TableModel)hp.Model).Values[0] += 100;
                    break;
                case "reward-ratio":
                    ((TableModel)reward.Model).Values[0] = 2;
                    break;
            }

            Assert.Throws<InvalidOperationException>(() => RunBalanceExporter.ValidateProfiles(standard, easy));
        }

        /// <summary>
        /// 保存された標準/緩和 Profile が Editor の軸・比率検査を満たします。
        /// [KO] 저장된 표준·완화 Profile이 Editor의 축·비율 검사를 만족합니다.
        /// [EN] Checks that the saved standard/easy profiles satisfy the Editor axis and ratio contract.
        /// </summary>
        [Test]
        public void SavedProfilesSatisfyEditorContract()
            => Assert.DoesNotThrow(() => RunBalanceExporter.ValidateProfiles(CloneProfile(Define.Difficulty.Standard), CloneProfile(Define.Difficulty.Easy)));

        /// <summary>
        /// 各必須トラックの欠落と先頭・末尾不足を、ゲーム開始入力の生成時に拒否します。
        /// [KO] 각 필수 트랙 누락·시작 및 끝 범위 부족을 게임 시작 입력 생성 시 거부합니다.
        /// [EN] Rejects each missing track and insufficient range start/end during game snapshot creation.
        /// </summary>
        [TestCase("enemy_hp", "missing")]
        [TestCase("enemy_attack", "missing")]
        [TestCase("reward_strength", "missing")]
        [TestCase("enemy_hp", "start")]
        [TestCase("enemy_attack", "start")]
        [TestCase("reward_strength", "start")]
        [TestCase("enemy_hp", "end")]
        [TestCase("enemy_attack", "end")]
        [TestCase("reward_strength", "end")]
        public void MissingTrackOrInsufficientRangeRejectsSnapshot(string id, string defect)
        {
            var profile = CloneProfile(Define.Difficulty.Standard);
            profile.TryGetTrack(id, out var track);
            if (defect == "missing")
                profile.Tracks.Remove(track);
            else if (defect == "start")
                track.RangeMin = 2;
            else
                track.RangeMax = 4;

            var definition = CloneDefinition(Bake(profile));
            Assert.Throws<InvalidOperationException>(() => definition.CreateSnapshot(LoadRoles(), RunConfiguration.CreateDefault()));
        }

        /// <summary>
        /// 保存値の非有限値と安全範囲外を、丸めや既定値への置換で受け入れません。
        /// [KO] 저장값의 비유한 수·안전 범위 밖 입력을 반올림·기본값 치환으로 허용하지 않습니다.
        /// [EN] Rejects nonfinite and unsafe stored inputs without accepting rounded or fallback values.
        /// </summary>
        [TestCase(0, double.NaN)]
        [TestCase(5, double.PositiveInfinity)]
        [TestCase(10, double.NegativeInfinity)]
        [TestCase(0, 0.99)]
        [TestCase(4, 1000000.01)]
        [TestCase(5, 0)]
        [TestCase(9, 100000.01)]
        [TestCase(10, 0.09999)]
        [TestCase(14, 3.00001)]
        public void UnsafeStoredValuesRejectSnapshot(int offset, double value)
        {
            var table = Bake(CloneProfile(Define.Difficulty.Standard));
            var serialized = new SerializedObject(table);
            serialized.FindProperty("_values").GetArrayElementAtIndex(offset).doubleValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(() => CloneDefinition(table).CreateSnapshot(LoadRoles(), RunConfiguration.CreateDefault()));
        }

        /// <summary>
        /// 壊れた保存配列・重複 ID・Table 未設定を明示的に拒否します。
        /// [KO] 손상된 저장 배열·중복 ID·Table 미설정을 명시적으로 거부합니다.
        /// [EN] Explicitly rejects damaged storage arrays, duplicate IDs and an unassigned table.
        /// </summary>
        [TestCase("_values")]
        [TestCase("_rangeMins")]
        [TestCase("_lengths")]
        [TestCase("_offsets")]
        [TestCase("duplicate")]
        [TestCase("missing")]
        public void InvalidStorageRejectsSnapshot(string defect)
        {
            var table = Bake(CloneProfile(Define.Difficulty.Standard));
            var serialized = new SerializedObject(table);
            if (defect == "duplicate")
                serialized.FindProperty("_trackIds").GetArrayElementAtIndex(1).stringValue = "enemy_hp";
            else if (defect == "missing")
                table = null;
            else
                serialized.FindProperty(defect).arraySize = 0;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(() => CloneDefinition(table).CreateSnapshot(LoadRoles(), RunConfiguration.CreateDefault()));
        }

        /// <summary>
        /// 購入アートに依存しない保存済み六役割の設定を読みます。
        /// [KO] 구매 아트와 무관한 저장된 여섯 역할 설정을 읽습니다.
        /// [EN] Loads the six saved role definitions independently of purchased art.
        /// </summary>
        private static BattleUnitInput[] LoadRoles()
            => AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/UnityCombatLab/Data/Units" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UnitDefinition>)
                .OrderBy(definition => definition.Role).Select((definition, index) => definition.CreateInput(index + 1,
                    index < 3 ? Define.BattleTeam.Ally : Define.BattleTeam.Enemy, Define.BattleSlot.Front)).ToArray();

        /// <summary>
        /// 実際の保存済み Profile を、変更可能なテスト専用コピーにします。
        /// [KO] 실제 저장된 Profile을 수정 가능한 테스트 전용 복사본으로 만듭니다.
        /// [EN] Makes a test-owned editable copy of the actual saved profile.
        /// </summary>
        private BalanceProfile CloneProfile(Define.Difficulty difficulty)
        {
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BalanceProfile>(
                "Assets/UnityCombatLab/Data/Balance/BalanceProfile_" + difficulty + ".asset"));
            _owned.Add(profile);
            return profile;
        }

        /// <summary>
        /// 原本と同じ Baker で一時 Table を出力します。
        /// [KO] 원본과 같은 Baker로 임시 Table을 출력합니다.
        /// [EN] Exports a temporary table through the original package baker.
        /// </summary>
        private BalanceTable Bake(BalanceProfile profile)
        {
            var table = ScriptableObject.CreateInstance<BalanceTable>();
            _owned.Add(table);
            BalanceTableBaker.Bake(profile, table);
            return table;
        }

        /// <summary>
        /// コピーした Run 定義の選択難易度だけを、一時 Table に接続します。
        /// [KO] 복사한 Run 정의에서 선택한 난이도만 임시 Table에 연결합니다.
        /// [EN] Connects only the selected difficulty of a copied run definition to the temporary table.
        /// </summary>
        private RunDefinition CloneDefinition(BalanceTable table, Define.Difficulty difficulty = Define.Difficulty.Standard)
        {
            var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/UnityCombatLab/Data/RunDef_Default.asset"));
            _owned.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(difficulty == Define.Difficulty.Easy ? "_easyBalance" : "_standardBalance").objectReferenceValue = table;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        /// <summary>
        /// M5 実測時の強化順を選び、実ゲームのコアから全イベントと結果を記録します。
        /// [KO] M5 실측 당시 강화 순서를 선택하고 실제 게임 코어의 전체 이벤트·결과를 기록합니다.
        /// [EN] Records events and results from the actual game core using the upgrade order from the measured M5 run.
        /// </summary>
        private static RunReplayRecord Record(RunGameData data, RunConfiguration configuration)
        {
            var progress = new RunProgress(data, configuration.Seed);
            var choices = new[] { Define.RewardKind.Power, Define.RewardKind.Power, Define.RewardKind.Renewal, Define.RewardKind.Power };
            var events = new List<BattleEvent>();
            while (!progress.IsFinished)
            {
                var wave = progress.Wave;
                var battle = new BattleSimulation(BattleSimulationTests.RunId, wave,
                    data.CreateUnits(configuration, wave, progress.Rewards), progress.Rewards);
                while (battle.Result == Define.BattleResult.Ongoing)
                {
                    battle.Step();
                    for (var index = 0; index < battle.EventCount; index++)
                        events.Add(battle.GetEvent(index));

                    battle.AcknowledgeEvents();
                }

                Assert.That(progress.TryCompleteWave(wave, battle.Result, battle.CurrentTick), Is.True);
                if (!progress.IsFinished)
                {
                    var offers = progress.GetOffers();
                    Assert.That(offers, Does.Contain(choices[wave - 1]));
                    Assert.That(progress.TryChoose(wave, choices[wave - 1]), Is.True);
                }
            }

            return new RunReplayRecord(data, progress.CreateResult(BattleSimulationTests.RunId, configuration), events);
        }
    }
}
