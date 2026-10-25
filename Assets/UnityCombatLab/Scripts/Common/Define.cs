namespace UnityCombatLab.Common
{
    /// <summary>
    /// アプリ共通の固定定義をまとめます。
    /// [KO] 앱 공통 고정 정의를 모읍니다.
    /// [EN] Groups fixed definitions shared across the app.
    /// </summary>
    public static class Define
    {
        #region Presentation

        /// <summary>
        /// Inspector の UI 部品を共通パレットの色へ結び付ける固定キー。値はアセットに保存するため番号を維持します。
        /// [KO] Inspector의 UI 요소를 공통 팔레트 색에 연결하는 고정 키입니다. 에셋에 저장되므로 번호를 유지합니다.
        /// [EN] Stable keys connecting Inspector UI graphics to shared palette colors; keep their serialized numeric values.
        /// </summary>
        public enum PaletteColor
        {
            /// <summary>
            /// 味方の見出し・HP・役割印。
            /// [KO] 아군 제목·HP·역할 배지 색입니다.
            /// [EN] Ally headings, health and role badges.
            /// </summary>
            Ally = 0,

            /// <summary>
            /// 敵の見出し・HP・役割印。
            /// [KO] 적 제목·HP·역할 배지 색입니다.
            /// [EN] Enemy headings, health and role badges.
            /// </summary>
            Enemy = 1,

            /// <summary>
            /// パネルと吹き出しの明るい面。
            /// [KO] 패널과 말풍선의 밝은 면 색입니다.
            /// [EN] Light panel and callout surfaces.
            /// </summary>
            Surface = 2,

            /// <summary>
            /// 明るい面に置く本文と HP 数字。
            /// [KO] 밝은 면 위의 본문과 HP 숫자 색입니다.
            /// [EN] Body text and HP numbers on light surfaces.
            /// </summary>
            Text = 3,

            /// <summary>
            /// 戦闘不能と防御膜ゼロの表示。
            /// [KO] 전투 불능과 방어막 0의 표시 색입니다.
            /// [EN] Defeated states and zero-shield labels.
            /// </summary>
            MutedText = 4,

            /// <summary>
            /// 役割アイコンと HP 損失の数字。
            /// [KO] 역할 아이콘과 HP 피해 숫자 색입니다.
            /// [EN] Role icons and health-damage numbers.
            /// </summary>
            OnAccent = 5,

            /// <summary>
            /// 暗い背景に置くタイトルと起動表示。
            /// [KO] 어두운 배경 위 제목과 시작 안내 색입니다.
            /// [EN] Title and startup text on the dark background.
            /// </summary>
            OnBackground = 6,

            /// <summary>
            /// 残っている防御膜の数字。
            /// [KO] 남아 있는 방어막 수치 색입니다.
            /// [EN] Remaining shield values.
            /// </summary>
            Shield = 7,

            /// <summary>
            /// 現在の防御膜に添えるアイコン。
            /// [KO] 현재 방어막 수치 옆의 아이콘 색입니다.
            /// [EN] Icons beside current shield values.
            /// </summary>
            ShieldIcon = 8,

            /// <summary>
            /// 防御膜の吸収数字とアイコン。
            /// [KO] 방어막 흡수 숫자와 아이콘 색입니다.
            /// [EN] Shield-absorption numbers and icons.
            /// </summary>
            ShieldDamage = 9,

            /// <summary>
            /// HP ゲージの固定枠。
            /// [KO] HP 게이지의 고정 테두리 색입니다.
            /// [EN] Fixed health-gauge borders.
            /// </summary>
            BarBorder = 10,

            /// <summary>
            /// HP ゲージの減った領域。
            /// [KO] HP 게이지의 소모된 영역 색입니다.
            /// [EN] Depleted areas of health gauges.
            /// </summary>
            BarEmpty = 11,

            /// <summary>
            /// パネルの影。透明度もこの値で指定します。
            /// [KO] 패널 그림자 색입니다. 투명도도 이 값에서 지정합니다.
            /// [EN] Panel shadows, including their opacity.
            /// </summary>
            Shadow = 12,

            /// <summary>
            /// 戦闘カメラが描く舞台の外側。
            /// [KO] 전투 카메라가 그리는 무대 바깥 배경색입니다.
            /// [EN] Camera background outside the battle stage.
            /// </summary>
            Background = 13,

            /// <summary>
            /// HP 損失と吸収数字の輪郭。
            /// [KO] HP 손실과 흡수 숫자의 외곽선 색입니다.
            /// [EN] Outlines around health-loss and absorption numbers.
            /// </summary>
            DamageOutline = 14,

            /// <summary>
            /// 本体ゲージが減る前に示す HP 損失範囲。
            /// [KO] 본 게이지가 줄기 전에 보여주는 HP 손실 구간입니다.
            /// [EN] Health-loss region shown before the main fill shrinks.
            /// </summary>
            DamagePreview = 15,

            /// <summary>
            /// 遅れて減る直前 HP。
            /// [KO] 늦게 줄어드는 직전 HP입니다.
            /// [EN] Old health that shrinks after a delay.
            /// </summary>
            TrailingHealth = 16
        }

        #endregion

        #region Scenes

        /// <summary>
        /// ビルドインデックスに対応するシーン種別。
        /// [KO] 빌드 인덱스에 대응하는 씬 종류입니다.
        /// [EN] Scene types corresponding to build indices.
        /// </summary>
        public enum SceneType
        {
            /// <summary>
            /// 初期化シーン。ビルドインデックス 0。
            /// [KO] 초기화 씬이며 빌드 인덱스 0입니다.
            /// [EN] The initialization scene at build index 0.
            /// </summary>
            Boot = 0,

            /// <summary>
            /// 戦闘の舞台。ビルドインデックス 1。
            /// [KO] 전투 무대이며 빌드 인덱스 1입니다.
            /// [EN] The battle stage at build index 1.
            /// </summary>
            Dungeon = 1,

            /// <summary>
            /// タイトル・編成・結果を表示するシーン。ビルドインデックス 2。
            /// [KO] 타이틀·편성·결과를 표시하는 씬이며 빌드 인덱스 2입니다.
            /// [EN] Scene displaying title, formation and results at build index 2.
            /// </summary>
            Frontend = 2
        }

        #endregion

        #region Run

        /// <summary>
        /// シード未指定または0の出発に使う固定値。
        /// [KO] 시드 미지정 또는 0인 출발에 사용할 고정값입니다.
        /// [EN] Fixed seed used when a departure omits a seed or supplies zero.
        /// </summary>
        public const uint DefaultRunSeed = 20261024u;

        /// <summary>
        /// やさしい難易度で敵のHPと攻撃力に掛ける百分率。
        /// [KO] 쉬움 난이도에서 적 HP와 공격력에 곱할 백분율입니다.
        /// [EN] Percentage applied to enemy health and attack on easy difficulty.
        /// </summary>
        public const int EasyEnemyStatPercent = 80;

        /// <summary>
        /// 一回の挑戦で許可する処理段階。
        /// [KO] 한 번의 도전에서 허용할 처리 단계입니다.
        /// [EN] Processing phases permitted during one run.
        /// </summary>
        public enum RunPhase
        {
            Loading,
            Battle,
            CleaningUp
        }

        /// <summary>
        /// 出発時に固定する敵の難易度。
        /// [KO] 출발 시 고정하는 적 난이도입니다.
        /// [EN] Enemy difficulty fixed at departure.
        /// </summary>
        public enum Difficulty
        {
            Standard,
            Easy
        }

        /// <summary>
        /// Frontend シーンが切り替えて表示する画面。
        /// [KO] Frontend 씬에서 전환해 표시할 화면입니다.
        /// [EN] Screens switched within the Frontend scene.
        /// </summary>
        public enum FrontendPage
        {
            Title,
            Formation,
            Results,
            Error
        }

        #endregion

        #region Battle

        /// <summary>
        /// 一秒あたりの戦闘 tick 数。1 tick は 0.05 秒です。
        /// [KO] 1초당 전투 tick 수이며 1 tick은 0.05초입니다.
        /// [EN] Combat ticks per second; one tick is 0.05 seconds.
        /// </summary>
        public const int TicksPerSecond = 20;

        /// <summary>
        /// 倍率計算の基準値。10000 を 100% として扱います。
        /// [KO] 배율 계산의 기준값이며 10000을 100%로 취급합니다.
        /// [EN] Scale used for ratio calculations; 10000 represents 100%.
        /// </summary>
        public const int BasisPoints = 10000;

        /// <summary>
        /// 各陣営のスロット数。
        /// [KO] 팀별 슬롯 수.
        /// [EN] Slots per team.
        /// </summary>
        public const int SlotsPerTeam = 3;

        /// <summary>
        /// 交互に行動する陣営数。
        /// [KO] 교대로 행동하는 팀 수.
        /// [EN] Number of teams taking interleaved actions.
        /// </summary>
        public const int BattleTeamCount = 2;

        /// <summary>
        /// 全陣営のスロットから求める最大ユニット数。
        /// [KO] 전체 팀의 슬롯으로 계산한 최대 유닛 수.
        /// [EN] Maximum unit count derived from all team slots.
        /// </summary>
        public const int MaxBattleUnitCount = BattleTeamCount * SlotsPerTeam;

        /// <summary>
        /// 最初の攻撃ティック。
        /// [KO] 첫 공격 틱.
        /// [EN] First attack tick.
        /// </summary>
        public const int FirstAttackTick = 10;

        /// <summary>
        /// 戦闘の制限ティック。
        /// [KO] 전투 제한 틱.
        /// [EN] Combat tick limit.
        /// </summary>
        public const int BattleTickLimit = 1800;

        /// <summary>
        /// 一つの挑戦で進行するウェーブ数。
        /// [KO] 한 도전에서 진행하는 웨이브 수.
        /// [EN] Number of waves in one run.
        /// </summary>
        public const int WavesPerRun = 5;

        #region BattleRules

        /// <summary>
        /// 百分率の100%を表す値。
        /// [KO] 백분율의 100%를 나타내는 값.
        /// [EN] Percentage denominator representing 100%.
        /// </summary>
        public const int PercentScale = 100;

        /// <summary>
        /// 騎士が軽減する被害の百分率。
        /// [KO] 기사가 경감하는 피해의 백분율.
        /// [EN] Percentage of incoming damage reduced by the knight.
        /// </summary>
        public const int KnightDamageReductionPercent = 20;

        /// <summary>
        /// レンジャーの特殊攻撃が発生する攻撃周期。
        /// [KO] 궁수의 고유 공격이 발생하는 공격 횟수 주기.
        /// [EN] Attack-count period of the ranger special attack.
        /// </summary>
        public const int RangerSpecialAttackPeriod = 4;

        /// <summary>
        /// レンジャーの特殊攻撃の百分率。
        /// [KO] 궁수 고유 공격의 피해 백분율.
        /// [EN] Damage percentage of the ranger special attack.
        /// </summary>
        public const int RangerSpecialDamagePercent = 200;

        /// <summary>
        /// 魔術師の範囲攻撃が発生する攻撃周期。
        /// [KO] 마법사의 광역 공격이 발생하는 공격 횟수 주기.
        /// [EN] Attack-count period of the mage area attack.
        /// </summary>
        public const int MageAreaAttackPeriod = 3;

        /// <summary>
        /// 魔術師の範囲攻撃の百分率。
        /// [KO] 마법사 광역 공격의 피해 백분율.
        /// [EN] Damage percentage of the mage area attack.
        /// </summary>
        public const int MageAreaDamagePercent = 70;

        /// <summary>
        /// 隊長の範囲攻撃が発生する攻撃周期。
        /// [KO] 대장의 광역 공격이 발생하는 공격 횟수 주기.
        /// [EN] Attack-count period of the captain area attack.
        /// </summary>
        public const int CaptainAreaAttackPeriod = 3;

        /// <summary>
        /// 隊長の範囲攻撃の百分率。
        /// [KO] 대장 광역 공격의 피해 백분율.
        /// [EN] Damage percentage of the captain area attack.
        /// </summary>
        public const int CaptainAreaDamagePercent = 60;

        /// <summary>
        /// 攻撃間隔の下限ティック。
        /// [KO] 공격 간격의 최소 틱.
        /// [EN] Minimum attack interval in ticks.
        /// </summary>
        public const int MinimumAttackIntervalTicks = 4;

        /// <summary>
        /// 盾吸収より前に保証する最小被害。
        /// [KO] 방어막 흡수 전에 보장하는 최소 피해.
        /// [EN] Minimum damage guaranteed before shield absorption.
        /// </summary>
        public const int MinimumDamage = 1;

        #endregion

        /// <summary>
        /// 陣営。
        /// [KO] 팀.
        /// [EN] Combat side.
        /// </summary>
        public enum BattleTeam
        {
            Ally,
            Enemy
        }

        /// <summary>
        /// 論理スロット順。
        /// [KO] 논리 슬롯 순서.
        /// [EN] Logical slot order.
        /// </summary>
        public enum BattleSlot
        {
            Front,
            BackUpper,
            BackLower
        }

        /// <summary>
        /// 固定の戦闘役割。
        /// [KO] 고정 전투 역할.
        /// [EN] Fixed combat roles.
        /// </summary>
        public enum UnitRole
        {
            Knight,
            Ranger,
            Mage,
            SkeletonMelee,
            SkeletonRanged,
            SkeletonCaptain
        }

        /// <summary>
        /// 戦闘イベント種別。
        /// [KO] 전투 이벤트 종류.
        /// [EN] Combat event kind.
        /// </summary>
        public enum BattleEventKind
        {
            AttackExecuted,
            DamageApplied,
            UnitDied,
            BattleEnded
        }

        /// <summary>
        /// 戦闘結果。
        /// [KO] 전투 결과.
        /// [EN] Combat outcome.
        /// </summary>
        public enum BattleResult
        {
            Ongoing,
            Victory,
            Defeat,
            Timeout
        }

        /// <summary>
        /// Runの失敗箇所。表示する文章はPresentationが決めます。
        /// [KO] Run 실패 지점이며 표시 문장은 Presentation에서 결정합니다.
        /// [EN] Run failure location; Presentation supplies its displayed wording.
        /// </summary>
        public enum RunFailure
        {
            None,
            Startup,
            Battle,
            Frontend
        }

        /// <summary>
        /// 共通切替表示が示す処理段階。
        /// [KO] 공통 전환 표시에서 나타내는 처리 단계입니다.
        /// [EN] Processing phase shown by the shared transition display.
        /// </summary>
        public enum RunTransitionPhase
        {
            LoadingDungeon,
            CleaningUp,
            RecoveryFailed
        }

        #endregion
    }
}
