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
            if (inputs == null || inputs.Count < 1 || inputs.Count > Define.MaxBattleUnitCount || resources == null ||
                spawnPoints == null || spawnPoints.Length != Define.MaxBattleUnitCount || parent == null)
            {
                throw new ArgumentException("Battle creation requires one to six inputs, six spawn points, loaded assets and a parent.");
            }

            var views = new UnitView[inputs.Count];
            var occupiedSlots = 0;
            try
            {
                for (var index = 0; index < views.Length; index++)
                {
                    var input = inputs[index];
                    // 配列内の順番ではなく陣営と枠で配置する。例えば味方後列下は、敵の空き枠があっても生成位置4を使う。
                    if (input == null)
                        throw new InvalidOperationException("A spawned unit requires an input.");

                    var spawnIndex = (int)input.Slot * Define.BattleTeamCount + (int)input.Team;
                    if (spawnPoints[spawnIndex] == null || (occupiedSlots & (1 << spawnIndex)) != 0)
                    {
                        throw new InvalidOperationException("Each unit requires a unique team/slot and an assigned spawn point.");
                    }

                    occupiedSlots |= 1 << spawnIndex;

                    var prefab = resources.GetUnit(input.Role).Prefab.GetComponent<UnitView>();
                    if (prefab == null)
                        throw new InvalidOperationException($"Unit prefab for {input.Role} requires a root UnitView.");

                    var view = UnityEngine.Object.Instantiate(prefab, parent);
                    views[index] = view;
                    view.name = prefab.name;
                    view.transform.SetPositionAndRotation(spawnPoints[spawnIndex].position, spawnPoints[spawnIndex].rotation);
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
