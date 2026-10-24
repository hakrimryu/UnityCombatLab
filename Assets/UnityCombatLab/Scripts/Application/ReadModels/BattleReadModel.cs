using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Application.ReadModels
{
    /// <summary>
    /// 各ユニットの画面用 HP・防御膜と戦闘結果をまとめます。実際の戦闘判定は BattleSimulation が行います。
    /// [KO] 유닛별 화면용 HP·방어막과 전투 결과를 모읍니다. 실제 전투 판정은 BattleSimulation이 수행합니다.
    /// [EN] Groups display health, shields and outcome. BattleSimulation remains responsible for combat decisions.
    /// </summary>
    public sealed class BattleReadModel : IDisposable
    {
        /// <summary>
        /// 画面用のユニットを一度だけ作って保持します。内部ではこの配列を直接巡回し、外部には Units で同じ内容を公開します。
        /// [KO] 화면용 유닛을 한 번만 만들어 보관합니다. 내부에서는 이 배열을 직접 순회하고 외부에는 Units로 같은 내용을 보여줍니다.
        /// [EN] Stores display units created once; internal code iterates this array while Units exposes the same contents to callers.
        /// </summary>
        private readonly UnitReadModel[] _unitReadModels;

        /// <summary>
        /// 終了通知は最後のイベント消費時にだけ変更します。
        /// [KO] 종료 알림은 마지막 이벤트 소비 시에만 변경합니다.
        /// [EN] Changes the outcome only when consuming the final event.
        /// </summary>
        private readonly ReactiveProperty<Define.BattleResult> _result = new(Define.BattleResult.Ongoing);

        /// <summary>
        /// 表示するユニット一覧。外部から項目の追加・入替はできませんが、各ユニットの表示 HP は変わります。
        /// [KO] 표시할 유닛 목록입니다. 외부에서 항목을 추가·교체할 수 없지만 각 유닛의 표시 HP는 바뀝니다.
        /// [EN] Display unit list; callers cannot add or replace items, while each unit’s displayed health can change.
        /// </summary>
        public IReadOnlyList<UnitReadModel> Units { get; }

        /// <summary>
        /// 確定した表示結果。
        /// [KO] 확정된 표시 결과.
        /// [EN] Committed display outcome.
        /// </summary>
        public ReadOnlyReactiveProperty<Define.BattleResult> Result => _result;

        /// <summary>
        /// Runner から開始データを受け取り、各ユニットの初期 HP と防御膜を画面用にコピーします。
        /// [KO] Runner에서 시작 데이터를 받아 각 유닛의 초기 HP와 방어막을 화면 표시용으로 복사합니다.
        /// [EN] Receives starting inputs from the runner and copies initial health and shield for display.
        /// </summary>
        internal BattleReadModel(IReadOnlyList<BattleUnitInput> inputs)
        {
            _unitReadModels = inputs.Select(input => new UnitReadModel(input)).ToArray();
            // 配列やユニットを複製せず、外部から項目を入れ替えられない窓口だけを作る。
            Units = Array.AsReadOnly(_unitReadModels);
        }

        /// <summary>
        /// Runner がイベントごとに呼びます。被害対象の表示 HP を減らし、終了イベントなら結果の文字を更新させます。
        /// [KO] Runner가 이벤트마다 호출합니다. 피해 대상의 표시 HP를 줄이고, 종료 이벤트라면 결과 문구 갱신을 알립니다.
        /// [EN] Called by the runner for each event to update the damaged unit or notify the HUD of the battle outcome.
        /// </summary>
        internal void Apply(BattleEvent battleEvent)
        {
            if (battleEvent.Kind == Define.BattleEventKind.DamageApplied)
            {
                // イベントの対象 ID と同じユニットを探す。並び順ではなく ID で相手を一致させる。
                // 配列を直接 foreach すると、被害のたびに巡回用の一時オブジェクトを作らずに済む。
                foreach (var unit in _unitReadModels)
                {
                    if (unit.Input.Id == battleEvent.TargetId)
                    {
                        unit.ApplyDamage(battleEvent);
                        return;
                    }
                }

                throw new InvalidOperationException("The damage target has no read model.");
            }

            if (battleEvent.Kind == Define.BattleEventKind.BattleEnded)
            {
                // 結果の値が変わると、R3 を通して BattleHudPresenter.HandleResultChanged が呼ばれる。
                _result.Value = battleEvent.Result;
            }
        }

        /// <summary>
        /// Presenter の解除後に通知元を破棄します。
        /// [KO] Presenter 해제 후 알림 원본을 폐기합니다.
        /// [EN] Disposes notification sources after presenters detach.
        /// </summary>
        public void Dispose()
        {
            foreach (var unit in _unitReadModels)
            {
                unit.Dispose();
            }

            _result.Dispose();
        }
    }
}
