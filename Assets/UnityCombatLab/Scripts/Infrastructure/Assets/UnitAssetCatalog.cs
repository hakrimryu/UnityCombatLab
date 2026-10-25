using System;
using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 職業から能力設定と表示を選び、編成画面と戦闘で同じ組を使います。
    /// [KO] 직업으로 능력 설정과 표시를 선택하여 편성 화면과 전투에서 같은 묶음을 사용합니다.
    /// [EN] Selects stats and visuals by role so formation and battle use the same pair.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/Unit Asset Catalog")]
    public sealed class UnitAssetCatalog : ScriptableObject
    {
        /// <summary>
        /// UnitRoleの順に保存する参照。職業の明示値も照合して誤配置を拒否します。
        /// [KO] UnitRole 순서로 저장하는 참조이며 명시한 직업 값도 대조해 잘못된 배치를 거부합니다.
        /// [EN] References in UnitRole order, checked against explicit roles to reject misplaced entries.
        /// </summary>
        [SerializeField] private UnitAssetReference[] _units;

        /// <summary>
        /// 指定した職業の参照を返し、欠落・順序違いをロード前に報告します。
        /// [KO] 지정 직업의 참조를 반환하며 누락·순서 오류를 로드 전에 보고합니다.
        /// [EN] Returns a role's references, reporting missing or misplaced entries before loading.
        /// </summary>
        public UnitAssetReference Get(Define.UnitRole role)
        {
            var index = (int)role;
            if (!Enum.IsDefined(typeof(Define.UnitRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));

            if (_units == null || _units.Length != Enum.GetValues(typeof(Define.UnitRole)).Length ||
                _units[index] == null || _units[index].Role != role ||
                _units[index].Definition == null || !_units[index].Definition.RuntimeKeyIsValid() ||
                _units[index].Prefab == null || !_units[index].Prefab.RuntimeKeyIsValid())
            {
                throw new InvalidOperationException($"Unit catalog requires valid references for {role} in role order.");
            }

            return _units[index];
        }
    }
}
