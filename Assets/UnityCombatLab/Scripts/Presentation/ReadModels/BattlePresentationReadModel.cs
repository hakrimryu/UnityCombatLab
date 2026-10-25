using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// 各ユニットの表示用 HP・盾と、演出終了後に公開する勝敗を保持します。
    /// [KO] 각 유닛의 표시용 HP·방어막과 연출 종료 후 공개할 승패를 보관합니다.
    /// [EN] Stores displayed unit health and shields and the outcome published after effects finish.
    /// </summary>
    public sealed class BattlePresentationReadModel : IDisposable
    {
        /// <summary>
        /// 六つの固定スロットに対応する表示状態。
        /// [KO] 고정 여섯 슬롯의 표시 상태입니다.
        /// [EN] Presentation states for the six fixed slots.
        /// </summary>
        private readonly UnitPresentationReadModel[] _units;

        /// <summary>
        /// 最後の命中と死亡演出が済むまで Ongoing のままの表示結果。
        /// [KO] 마지막 명중·사망 연출이 끝날 때까지 Ongoing을 유지하는 표시 결과입니다.
        /// [EN] Display result that stays Ongoing until final hits and death presentation finish.
        /// </summary>
        private readonly ReactiveProperty<Define.BattleResult> _result = new(Define.BattleResult.Ongoing);

        /// <summary>
        /// HUD と Presenter が同じ順番で使う一覧。
        /// [KO] HUD와 Presenter가 같은 순서로 사용하는 목록입니다.
        /// [EN] Collection used in matching order by HUD and battle presenters.
        /// </summary>
        public IReadOnlyList<UnitPresentationReadModel> Units { get; }

        /// <summary>
        /// 演出完了後の表示結果。
        /// [KO] 연출 완료 후 표시 결과입니다.
        /// [EN] Result presented after effects complete.
        /// </summary>
        public ReadOnlyReactiveProperty<Define.BattleResult> Result => _result;

        /// <summary>
        /// 戦闘開始データから各ユニットの表示用 HP と盾を初期化します。
        /// [KO] 전투 시작 데이터로 각 유닛의 표시용 HP와 방어막을 초기화합니다.
        /// [EN] Initializes displayed unit health and shields from starting combat data.
        /// </summary>
        public BattlePresentationReadModel(IEnumerable<BattleUnitInput> inputs)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            _units = inputs.Select(input => new UnitPresentationReadModel(input)).ToArray();
            Units = Array.AsReadOnly(_units);
        }

        /// <summary>
        /// Presenter が演出終了を確認したら、確定した勝敗を HUD へ通知します。
        /// [KO] Presenter가 연출 종료를 확인하면 확정한 승패를 HUD에 알립니다.
        /// [EN] Notifies the HUD of the committed outcome once the presenter confirms effects have finished.
        /// </summary>
        public void Complete(Define.BattleResult result)
        {
            _result.Value = result;
        }

        /// <summary>
        /// 全 HUD 購読の解除後に各スロットと結果を解放します。
        /// [KO] 모든 HUD 구독을 끊은 뒤 슬롯과 결과를 해제합니다.
        /// [EN] Releases slots and result after all HUD subscriptions detach.
        /// </summary>
        public void Dispose()
        {
            foreach (var unit in _units)
            {
                unit.Dispose();
            }

            _result.Dispose();
        }
    }
}
