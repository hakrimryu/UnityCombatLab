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
        /// 各ユニットの確定 HP と盾を、開始データと同じ順に保持します。
        /// [KO] 각 유닛의 확정 HP와 방어막을 시작 데이터와 같은 순서로 보관합니다.
        /// [EN] Stores committed unit health and shields in starting-data order.
        /// </summary>
        private readonly UnitReadModel[] _unitReadModels;

        /// <summary>
        /// 戦闘終了イベントで更新する勝敗。
        /// [KO] 전투 종료 이벤트로 갱신할 승패입니다.
        /// [EN] Outcome updated by the battle-ended event.
        /// </summary>
        private readonly ReactiveProperty<Define.BattleResult> _result = new(Define.BattleResult.Ongoing);

        /// <summary>
        /// 開始データと同じ順で取得する各ユニットの確定状態。
        /// [KO] 시작 데이터와 같은 순서로 조회할 각 유닛의 확정 상태입니다.
        /// [EN] Committed unit states in starting-data order.
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
            Units = Array.AsReadOnly(_unitReadModels);
        }

        /// <summary>
        /// Runner がイベントごとに呼び、被害対象の確定 HP・盾または確定結果を更新します。
        /// [KO] Runner가 이벤트마다 호출하여 피해 대상의 확정 HP·방어막 또는 확정 결과를 갱신합니다.
        /// [EN] Called by the runner for each event to update committed unit values or the committed outcome.
        /// </summary>
        internal void Apply(BattleEvent battleEvent)
        {
            if (battleEvent.Kind == Define.BattleEventKind.DamageApplied || battleEvent.Kind == Define.BattleEventKind.HealthRestored)
            {
                // イベントの対象 ID に一致するユニットの HP と盾を更新する。
                foreach (var unit in _unitReadModels)
                {
                    if (unit.Input.Id == battleEvent.TargetId)
                    {
                        unit.ApplyChange(battleEvent);
                        return;
                    }
                }

                throw new InvalidOperationException("The damage target has no read model.");
            }

            if (battleEvent.Kind == Define.BattleEventKind.BattleEnded)
            {
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
