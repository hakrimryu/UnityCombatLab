using System;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 実行時に組み立てる日本語表示をまとめ、能力説明には戦闘と同じ規則を使います。
    /// [KO] 실행 중 조립하는 일본어 표시를 모으고 능력 설명에는 전투와 같은 규칙을 사용합니다.
    /// [EN] Centralizes runtime Japanese text and uses the combat rules for stat descriptions.
    /// </summary>
    public static class GameText
    {
        /// <summary>
        /// Applicationが通知した失敗箇所を画面の説明へ変換します。
        /// [KO] Application에서 알린 실패 지점을 화면 설명으로 변환합니다.
        /// [EN] Converts the failure location reported by Application to screen wording.
        /// </summary>
        public static string Failure(Define.RunFailure failure)
            => failure switch
            {
                Define.RunFailure.None => null,
                Define.RunFailure.Startup => StartupFailed,
                Define.RunFailure.Battle => BattleFailed,
                Define.RunFailure.Frontend => FrontendFailed,
                _ => throw new ArgumentOutOfRangeException(nameof(failure))
            };

        /// <summary>
        /// 共通表示の処理段階を画面の文章へ変換します。
        /// [KO] 공통 표시의 처리 단계를 화면 문장으로 변환합니다.
        /// [EN] Converts a transition phase into display text.
        /// </summary>
        public static string Transition(Define.RunTransitionPhase phase)
            => phase switch
            {
                Define.RunTransitionPhase.LoadingDungeon => LoadingDungeon,
                Define.RunTransitionPhase.CleaningUp => CleaningUp,
                Define.RunTransitionPhase.RecoveryFailed => FrontendFailed,
                _ => throw new ArgumentOutOfRangeException(nameof(phase))
            };

        /// <summary>
        /// Dungeon の資産とシーンを待つ間の案内。
        /// [KO] Dungeon 자산과 씬을 기다리는 동안의 안내입니다.
        /// [EN] Message shown while waiting for Dungeon assets and its scene.
        /// </summary>
        public const string LoadingDungeon = "ダンジョンを準備しています…";

        /// <summary>
        /// Run の資源解放と画面復帰を待つ間の案内。
        /// [KO] Run 자원 해제와 화면 복귀를 기다리는 동안의 안내입니다.
        /// [EN] Message shown while run resources are released and the screen returns.
        /// </summary>
        public const string CleaningUp = "終了処理中…";

        /// <summary>
        /// 開始に失敗した挑戦を同じ編成で再試行できる案内。
        /// [KO] 시작에 실패한 도전을 같은 편성으로 재시도할 수 있다는 안내입니다.
        /// [EN] Message allowing a failed startup to be retried with the same formation.
        /// </summary>
        public const string StartupFailed = "ダンジョンの準備に失敗しました。\nもう一度お試しください。";

        /// <summary>
        /// 戦闘進行が失敗し、今回の挑戦を終了した案内。
        /// [KO] 전투 진행이 실패하여 이번 도전을 종료했다는 안내입니다.
        /// [EN] Message explaining that a combat failure ended this run.
        /// </summary>
        public const string BattleFailed = "進行を続けられないため、今回の挑戦を終了しました。";

        /// <summary>
        /// 復帰先の画面を開けず、アプリの再起動が必要な案内。
        /// [KO] 복귀 화면을 열지 못하여 앱 재시작이 필요하다는 안내입니다.
        /// [EN] Message explaining that the destination screen could not open and the app needs restarting.
        /// </summary>
        public const string FrontendFailed = "画面を開けませんでした。\nアプリを再起動してください。";

        /// <summary>
        /// HUD と編成で共有する役割名を返します。
        /// [KO] HUD와 편성에서 공유하는 역할 이름을 반환합니다.
        /// [EN] Returns the role name shared by the HUD and formation screens.
        /// </summary>
        public static string RoleName(Define.UnitRole role)
            => role switch
            {
                Define.UnitRole.Knight => "騎士",
                Define.UnitRole.Ranger => "弓使い",
                Define.UnitRole.Mage => "魔法使い",
                Define.UnitRole.SkeletonMelee => "骸骨兵",
                Define.UnitRole.SkeletonRanged => "骸骨射手",
                Define.UnitRole.SkeletonCaptain => "骸骨隊長",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };

        /// <summary>
        /// 英雄の実際の HP・攻撃力と、Define の特性規則をカードの説明へ変換します。
        /// [KO] 영웅의 실제 HP·공격력과 Define의 특성 규칙을 카드 설명으로 변환합니다.
        /// [EN] Formats a hero card from actual health and attack and the trait rules in Define.
        /// </summary>
        public static string HeroDescription(BattleUnitInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var trait = input.Role switch
            {
                Define.UnitRole.Knight => $"受けるダメージを{Define.KnightDamageReductionPercent}%軽減。",
                Define.UnitRole.Ranger => $"{Define.RangerSpecialAttackPeriod}回ごとに強力な一撃。",
                Define.UnitRole.Mage => $"{Define.MageAreaAttackPeriod}回ごとに敵全体を攻撃。",
                _ => throw new ArgumentOutOfRangeException(nameof(input), "Hero cards require an ally role.")
            };

            return $"{RoleName(input.Role)}\nHP {input.MaxHealth}   ATK {input.Attack}\n{trait}";
        }

        /// <summary>
        /// 前列・後列上・後列下の配置名を返します。
        /// [KO] 전열·위쪽 후열·아래쪽 후열의 배치 이름을 반환합니다.
        /// [EN] Returns the front, upper-back or lower-back slot label.
        /// </summary>
        public static string SlotName(Define.BattleSlot slot)
            => slot switch
            {
                Define.BattleSlot.Front => "前衛",
                Define.BattleSlot.BackUpper => "後衛・上",
                Define.BattleSlot.BackLower => "後衛・下",
                _ => throw new ArgumentOutOfRangeException(nameof(slot))
            };

        /// <summary>
        /// 編成と結果で同じ難易度名を表示します。
        /// [KO] 편성과 결과에서 같은 난이도 이름을 표시합니다.
        /// [EN] Returns the same difficulty label for formation and results.
        /// </summary>
        public static string DifficultyName(Define.Difficulty difficulty)
            => difficulty switch
            {
                Define.Difficulty.Standard => "標準",
                Define.Difficulty.Easy => "やさしい",
                _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
            };

        /// <summary>
        /// 難易度ボタンの選択印と、実際の敵能力倍率を表示します。
        /// [KO] 난이도 버튼 선택 표시와 실제 적 능력 배율을 표시합니다.
        /// [EN] Formats a difficulty button with its selection mark and actual enemy stat scale.
        /// </summary>
        public static string DifficultyOption(Define.Difficulty option, Define.Difficulty selected)
            => (option == selected ? "● " : "○ ") + DifficultyName(option) +
               (option == Define.Difficulty.Easy ? $"\n敵HP・攻撃力\n{Define.EasyEnemyStatPercent}%" : "");

        /// <summary>
        /// 勝敗の説明を返します。進行中は HUD の見出しとして使います。
        /// [KO] 승패 설명을 반환하며 진행 중에는 HUD 제목으로 사용합니다.
        /// [EN] Returns an outcome explanation, or the HUD heading while combat is ongoing.
        /// </summary>
        public static string Outcome(Define.BattleResult result)
            => result switch
            {
                Define.BattleResult.Ongoing => "戦闘中",
                Define.BattleResult.Victory => "戦闘に勝利しました！",
                Define.BattleResult.Defeat => "パーティが全滅しました。",
                Define.BattleResult.Timeout => "制限時間に達しました。",
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            };

        /// <summary>
        /// 独立した戦闘 HUD の短い勝利見出しと、その他の終了説明を返します。
        /// [KO] 독립 전투 HUD의 짧은 승리 제목과 나머지 종료 설명을 반환합니다.
        /// [EN] Returns the standalone HUD's short victory heading or the other outcome explanations.
        /// </summary>
        public static string BattleHeading(Define.BattleResult result)
            => result == Define.BattleResult.Victory ? "勝利" : Outcome(result);

        /// <summary>
        /// 一戦の確定結果を、難易度・到達数・分秒を含む結果説明へ変換します。
        /// [KO] 한 전투의 확정 결과를 난이도·도달 수·분초를 포함한 결과 설명으로 변환합니다.
        /// [EN] Formats a committed single-battle result with difficulty, progress and elapsed minutes and seconds.
        /// </summary>
        public static string ResultDetails(RunResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var elapsed = TimeSpan.FromSeconds((double)result.Ticks / Define.TicksPerSecond);
            return $"{Outcome(result.Outcome)}\n\n難易度：{DifficultyName(result.Configuration.Difficulty)}\n" +
                   $"クリア数：{result.ClearedWaves} / {Define.WavesPerRun}　到達ウェーブ：{result.ReachedWave}\n" +
                   $"戦闘時間：{elapsed.Minutes:00}:{elapsed.Seconds:00}\n\n{RewardSummary(result.Rewards, true)}";
        }

        /// <summary>
        /// basis point を余分なゼロのない百分率へ変換します。
        /// [KO] basis point를 불필요한 0 없는 백분율로 변환합니다.
        /// [EN] Formats basis points as a percentage without unnecessary zeroes.
        /// </summary>
        public static string Percent(int basisPoints)
            => (basisPoints / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";

        /// <summary>
        /// 強化 ID に対応する日本語名を返します。
        /// [KO] 강화 ID에 대응하는 일본어 이름을 반환합니다.
        /// [EN] Returns the Japanese name for an upgrade kind.
        /// </summary>
        public static string RewardName(Define.RewardKind kind)
            => kind switch
            {
                Define.RewardKind.Power => "力の紋章",
                Define.RewardKind.Vitality => "生命の紋章",
                Define.RewardKind.Haste => "迅速の紋章",
                Define.RewardKind.Barrier => "守護の紋章",
                Define.RewardKind.Renewal => "再生の紋章",
                Define.RewardKind.Ambush => "先制の紋章",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

        /// <summary>
        /// 実際に適用する増加量と効果の単位・期間を表示します。
        /// [KO] 실제 적용할 증가량과 효과의 단위·기간을 표시합니다.
        /// [EN] Describes actual increments with their effect units and duration.
        /// </summary>
        public static string RewardEffect(Define.RewardKind kind, int increaseBp)
            => kind switch
            {
                Define.RewardKind.Power => $"攻撃力 +{Percent(increaseBp)}",
                Define.RewardKind.Vitality => $"最大HP +{Percent(increaseBp)}",
                Define.RewardKind.Haste => $"攻撃速度 +{Percent(increaseBp)}",
                Define.RewardKind.Barrier => $"開始時の盾：最大HPの{Percent(increaseBp)}",
                Define.RewardKind.Renewal => $"撃破ごとに最大HPの{Percent(increaseBp)}を回復",
                Define.RewardKind.Ambush => $"開始6秒間 攻撃力 +{Percent(increaseBp)}",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

        /// <summary>
        /// 強化を種類ごとにまとめ、HUD は回数、結果は実効果まで表示します。
        /// [KO] 강화를 종류별로 묶어 HUD에는 횟수, 결과에는 실제 효과까지 표시합니다.
        /// [EN] Groups upgrades by kind, showing stacks in HUD and actual effects in results.
        /// </summary>
        public static string RewardSummary(RunRewards rewards, bool includesEffects)
        {
            var text = new System.Text.StringBuilder();
            for (var index = 0; index < 6; index++)
            {
                var kind = (Define.RewardKind)index;
                var stacks = rewards.GetStacks(kind);
                if (stacks == 0)
                    continue;

                if (text.Length > 0)
                    text.Append(includesEffects ? "\n" : "　");

                text.Append(RewardName(kind)).Append(" ×").Append(stacks);
                if (includesEffects)
                    text.Append("　").Append(RewardEffect(kind, rewards.GetEffect(kind)));
            }

            return text.Length == 0 ? "強化の獲得なし" : text.ToString();
        }
    }
}
