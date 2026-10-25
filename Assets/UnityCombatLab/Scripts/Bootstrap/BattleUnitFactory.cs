using System;
using System.Collections.Generic;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Infrastructure.Assets;
using UnityCombatLab.Presentation.Units;
using UnityEngine;

namespace UnityCombatLab.Bootstrap
{
    /// <summary>
    /// 確定した戦闘入力の職業を、Dungeonの配置先に生成します。
    /// [KO] 확정된 전투 입력의 직업을 Dungeon 배치 지점에 생성합니다.
    /// [EN] Spawns roles from finalized battle inputs at Dungeon placement points.
    /// </summary>
    public static class BattleUnitFactory
    {
        /// <summary>
        /// ロード済みprefabを配置順に複製します。途中で失敗すると生成済みの表示をすべて破棄します。
        /// [KO] 로드된 prefab을 배치 순서로 복제하며 중간 실패 시 생성한 표시를 모두 파괴합니다.
        /// [EN] Instantiates loaded prefabs in slot order and destroys all created views on partial failure.
        /// </summary>
        public static UnitView[] Create(IReadOnlyList<BattleUnitInput> inputs, RunAssetScope resources,
            Transform[] spawnPoints, Transform parent)
        {
            if (inputs == null || inputs.Count != Define.MaxBattleUnitCount || resources == null ||
                spawnPoints == null || spawnPoints.Length != inputs.Count || parent == null)
            {
                throw new ArgumentException("Battle creation requires six inputs, spawn points, loaded assets and a parent.");
            }

            var views = new UnitView[inputs.Count];
            try
            {
                for (var index = 0; index < views.Length; index++)
                {
                    var input = inputs[index];
                    // 0/1は味方/敵の前列、2/3は後列上、4/5は後列下。能力と表示は同じ入力の職業を使う。
                    if (input == null || input.Team != (Define.BattleTeam)(index % Define.BattleTeamCount) ||
                        input.Slot != (Define.BattleSlot)(index / Define.BattleTeamCount) || spawnPoints[index] == null)
                    {
                        throw new InvalidOperationException("Battle inputs and spawn points must follow team/slot order.");
                    }

                    var prefab = resources.GetUnit(input.Role).Prefab.GetComponent<UnitView>();
                    if (prefab == null)
                        throw new InvalidOperationException($"Unit prefab for {input.Role} requires a root UnitView.");

                    var view = UnityEngine.Object.Instantiate(prefab, parent);
                    views[index] = view;
                    view.name = prefab.name;
                    view.transform.SetPositionAndRotation(spawnPoints[index].position, spawnPoints[index].rotation);
                }

                return views;
            }
            catch
            {
                foreach (var view in views)
                {
                    if (view == null)
                        continue;

                    view.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(view.gameObject);
                }

                throw;
            }
        }
    }
}
