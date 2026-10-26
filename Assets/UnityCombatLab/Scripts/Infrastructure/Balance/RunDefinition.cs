using System;
using System.Collections.Generic;
using System.Linq;
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
        /// アセット値とロード済みユニット定義を検査し、Run 専用の不変入力を返します。
        /// [KO] 에셋 값·로드한 유닛 정의를 검사하고 Run 전용 불변 입력을 반환합니다.
        /// [EN] Validates assets and loaded unit templates and returns immutable inputs dedicated to the run.
        /// </summary>
        public RunGameData CreateSnapshot(IReadOnlyList<BattleUnitInput> roles)
        {
            if (_waves == null || _rewards == null || _waves.Any(wave => wave == null) || _rewards.Any(reward => reward == null))
                throw new InvalidOperationException("Run definition has missing wave or reward references.");

            return new RunGameData(_waves.Select(wave => wave.CreateSnapshot()).ToArray(),
                _rewards.Select(reward => reward.CreateSnapshot()).ToArray(), roles);
        }
    }
}
