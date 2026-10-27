using System;
using System.Collections.Generic;
using System.Linq;
using Fulcrum;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Balance
{
    /// <summary>
    /// 五ウェーブと六強化の参照をまとめ、ロードした役割設定と一緒に固定します。
    /// [KO] 다섯 웨이브·여섯 강화 참조를 묶고 로드한 역할 설정과 함께 고정합니다.
    /// [EN] Groups five wave and six upgrade references and freezes them with loaded role templates.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/Run Definition")]
    public sealed class RunDefinition : ScriptableObject
    {
        /// <summary>
        /// 1から5の順のウェーブ参照。
        /// [KO] 1부터 5 순서의 웨이브 참조입니다.
        /// [EN] Wave references ordered from one to five.
        /// </summary>
        [SerializeField] private WaveDefinition[] _waves;

        /// <summary>
        /// 重複のない六種類の強化参照。
        /// [KO] 중복 없는 여섯 종류의 강화 참조입니다.
        /// [EN] References to the six distinct upgrades.
        /// </summary>
        [SerializeField] private RewardDefinition[] _rewards;

        /// <summary>
        /// 標準難易度の Fulcrum 出力。
        /// [KO] 표준 난이도의 Fulcrum 출력입니다.
        /// [EN] Fulcrum output for standard difficulty.
        /// </summary>
        [SerializeField] private BalanceTable _standardBalance;

        /// <summary>
        /// 敵能力を80%にした Fulcrum 出力。
        /// [KO] 적 능력을 80%로 만든 Fulcrum 출력입니다.
        /// [EN] Fulcrum output authored with enemy stats at eighty percent.
        /// </summary>
        [SerializeField] private BalanceTable _easyBalance;

        /// <summary>
        /// 選択難易度の Table と配置・強化・ユニット定義から、この Run だけの固定入力を作ります。
        /// [KO] 선택한 난이도의 Table·배치·강화·유닛 정의에서 이번 Run만의 고정 입력을 만듭니다.
        /// [EN] Builds this run's fixed inputs from the selected difficulty table, slots, upgrades and role definitions.
        /// </summary>
        public RunGameData CreateSnapshot(IReadOnlyList<BattleUnitInput> roles, RunConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (_waves == null || _rewards == null || _waves.Any(wave => wave == null) || _rewards.Any(reward => reward == null))
                throw new InvalidOperationException("Run definition has missing wave or reward references.");

            var table = configuration.Difficulty == Define.Difficulty.Easy ? _easyBalance : _standardBalance;
            return new RunGameData
            (FulcrumBalanceAdapter.CreateWaves(table, _waves),
                _rewards.Select(reward => reward.CreateSnapshot()).ToArray(),
                roles,
                configuration.Difficulty);
        }
    }
}
