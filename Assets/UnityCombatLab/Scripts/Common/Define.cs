namespace UnityCombatLab.Common
{
    /// <summary>
    /// アプリ共通の固定定義をまとめます。
    /// [KO] 앱 공통 고정 정의를 모읍니다.
    /// [EN] Groups fixed definitions shared across the app.
    /// </summary>
    public static class Define
    {
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
            Dungeon = 1
        }

        #endregion

        #region Battle

        /// <summary>
        /// 戦闘の固定時間規約。
        /// [KO] 전투 고정 시간 규약.
        /// [EN] Fixed combat timing.
        /// </summary>
        public const int TicksPerSecond = 20;

        /// <summary>
        /// 比率の単位。
        /// [KO] 비율 단위.
        /// [EN] Basis point denominator.
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

        #endregion
    }
}
