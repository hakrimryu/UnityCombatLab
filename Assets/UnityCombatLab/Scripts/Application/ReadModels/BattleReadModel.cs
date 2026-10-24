using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Application.ReadModels
{
    /// <summary>
    /// 各ユニットの計算上の確定 HP・盾・結果を保持します。命中時刻の HUD は別の表示モデルを使います。
    /// [KO] 유닛별 계산상 확정 HP·방어막·결과를 보관하며 명중 시각의 HUD는 별도 표시 모델을 사용합니다.
    /// [EN] Stores committed simulation health, shields and outcome; impact-timed HUD uses a separate presentation model.
    /// </summary>
    public sealed class BattleReadModel : IDisposable
    {
        /// <summary>
        /// 確定状態のユニットを一度だけ作って保持します。内部ではこの配列を直接巡回し、外部には Units で同じ内容を公開します。
        /// [KO] 확정 상태 유닛을 한 번만 만들어 보관합니다. 내부에서는 이 배열을 직접 순회하고 외부에는 Units로 같은 내용을 보여줍니다.
        /// [EN] Stores committed-state units created once; internal code iterates this array while Units exposes the same contents to callers.
        /// </summary>
        private readonly UnitReadModel[] _unitReadModels;

        /// <summary>
        /// 終了通知は最後のイベント消費時にだけ変更します。
        /// [KO] 종료 알림은 마지막 이벤트 소비 시에만 변경합니다.
        /// [EN] Changes the outcome only when consuming the final event.
        /// </summary>
        private readonly ReactiveProperty<Define.BattleResult> _result = new(Define.BattleResult.Ongoing);

        /// <summary>
        /// 確定状態の一覧。外部から項目の追加・入替はできませんが、イベントで各 HP は変わります。
        /// [KO] 확정 상태 목록이며 외부에서 항목을 추가·교체할 수 없지만 이벤트로 각 HP는 바뀝니다.
        /// [EN] Committed-state list; callers cannot add or replace items, while events update each health value.
        /// </summary>
        public IReadOnlyList<UnitReadModel> Units { get; }

        /// <summary>
        /// 計算が確定した結果。画面へ公開する時刻は表示側が決めます。
        /// [KO] 계산이 확정한 결과이며 화면 공개 시각은 표시 측에서 결정합니다.
        /// [EN] Simulation-committed outcome; presentation decides when to publish it on screen.
        /// </summary>
        public ReadOnlyReactiveProperty<Define.BattleResult> Result => _result;

        /// <summary>
        /// Runner から開始データを受け取り、各ユニットの初期 HP と盾を確定状態として保持します。
        /// [KO] Runner에서 시작 데이터를 받아 각 유닛의 초기 HP와 방어막을 확정 상태로 보관합니다.
        /// [EN] Receives starting inputs from the runner and stores initial health and shield as committed state.
        /// </summary>
        internal BattleReadModel(IReadOnlyList<BattleUnitInput> inputs)
        {
            _unitReadModels = inputs.Select(input => new UnitReadModel(input)).ToArray();
            // 配列やユニットを複製せず、外部から項目を入れ替えられない窓口だけを作る。
            Units = Array.AsReadOnly(_unitReadModels);
        }

        /// <summary>
        /// Runner がイベントごとに呼び、被害対象の確定 HP・盾または確定結果を更新します。
        /// [KO] Runner가 이벤트마다 호출하여 피해 대상의 확정 HP·방어막 또는 확정 결과를 갱신합니다.
        /// [EN] Called by the runner for each event to update committed unit values or the committed outcome.
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
                // これは即時の確定結果。画面への公開時刻は表示側が別に管理する。
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
