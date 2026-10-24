using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// BattleEntryPoint が所有する表示用状態。命中済みの数値と最後の演出後の結果だけを HUD へ通知します。
    /// [KO] BattleEntryPoint가 소유하는 표시 상태이며 명중한 수치와 마지막 연출 후 결과만 HUD에 알립니다.
    /// [EN] BattleEntryPoint-owned presentation state notifying the HUD of arrived hits and the result after final effects.
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
        /// 検証済みの開始入力から HUD 専用の状態を一度作ります。
        /// [KO] 검증된 시작 입력으로 HUD 전용 상태를 한 번 만듭니다.
        /// [EN] Creates HUD-only state once from validated starting inputs.
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
        /// Presenter が最後の演出を確認した時、既に確定していた勝敗を表示へ渡します。
        /// [KO] Presenter가 마지막 연출을 확인하면 이미 확정된 승패를 표시로 전달합니다.
        /// [EN] Publishes the already committed result when the presenter confirms final presentation is complete.
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
