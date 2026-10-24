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
    }
}
