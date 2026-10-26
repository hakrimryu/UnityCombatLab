using System;
using System.Collections.Generic;
using UnityCombatLab.Application.ReadModels;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Domain.Run;

namespace UnityCombatLab.Application.Battle
{
    /// <summary>
    /// 経過秒を 0.05 秒ごとの計算に分け、発生した攻撃・被害・死亡を画面側へ順に知らせます。
    /// [KO] 경과 시간을 0.05초 단위 계산으로 나누고, 발생한 공격·피해·사망을 화면 쪽에 순서대로 알립니다.
    /// [EN] Splits elapsed seconds into 0.05-second steps and reports attacks, damage and deaths to the display in order.
    /// </summary>
    public sealed class BattleRunner : IDisposable
    {
        /// <summary>
        /// Advance 一回で処理する最大 tick 数。残り時間は次回へ繰り越します。
        /// [KO] Advance 한 번에 처리할 최대 tick 수이며 남은 시간은 다음 호출로 넘깁니다.
        /// [EN] Maximum ticks processed by one Advance call; remaining time carries over.
        /// </summary>
        public const int MaxTicksPerAdvance = 8;

        /// <summary>
        /// この Runner が進める戦闘シミュレーション。
        /// [KO] 이 Runner가 진행하는 전투 시뮬레이션입니다.
        /// [EN] Battle simulation advanced by this runner.
        /// </summary>
        private readonly BattleSimulation _simulation;

        /// <summary>
        /// 未処理の時間を tick 単位で保持します。
        /// [KO] 미처리 시간을 tick 단위로 보관합니다.
        /// [EN] Retains pending time in tick units.
        /// </summary>
        private double _pendingTicks;

        /// <summary>
        /// Advance の処理中かを記録し、通知中の呼び直しを検出します。
        /// [KO] Advance 처리 중인지 기록해 알림 중 재호출을 감지합니다.
        /// [EN] Tracks whether Advance is running to detect calls made during event notification.
        /// </summary>
        private bool _isAdvancing;

        /// <summary>
        /// 通知元の二重破棄を防ぎます。
        /// [KO] 알림 원본의 중복 해제를 막습니다.
        /// [EN] Prevents duplicate disposal of notification sources.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// イベントの処理に失敗し、戦闘の進行を停止したかを示します。
        /// [KO] 이벤트 처리 실패로 전투 진행을 중단했는지 나타냅니다.
        /// [EN] Whether event processing failed and combat advancement stopped.
        /// </summary>
        public bool HasFaulted { get; private set; }

        /// <summary>
        /// 戦闘計算で確定した各ユニットの HP・盾・結果。
        /// [KO] 전투 계산으로 확정한 각 유닛의 HP·방어막·결과입니다.
        /// [EN] Unit health, shields and result committed by combat calculations.
        /// </summary>
        public BattleReadModel ReadModel { get; }

        /// <summary>
        /// 処理を終えた戦闘 tick 数。
        /// [KO] 처리가 끝난 전투 tick 수입니다.
        /// [EN] Number of completed combat ticks.
        /// </summary>
        public int CurrentTick => _simulation.CurrentTick;

        /// <summary>
        /// 戦闘計算で決まった勝敗。
        /// [KO] 전투 계산으로 결정한 승패입니다.
        /// [EN] Outcome determined by combat calculations.
        /// </summary>
        public Define.BattleResult Result => _simulation.Result;

        /// <summary>
        /// 計算後のイベントを BattlePresenter などへ発生順に通知します。
        /// [KO] 계산 후 이벤트를 BattlePresenter 등에 발생 순서로 전달합니다.
        /// [EN] Delivers calculated events to BattlePresenter and other listeners in occurrence order.
        /// </summary>
        public event Action<BattleEvent> EventOccurred;

        /// <summary>
        /// 開始データで戦闘を作成し、初期 HP と盾を ReadModel に設定します。
        /// [KO] 시작 데이터로 전투를 만들고 초기 HP와 방어막을 ReadModel에 설정합니다.
        /// [EN] Creates combat from starting data and initializes ReadModel health and shields.
        /// </summary>
        public BattleRunner(Guid runId, int wave, IReadOnlyList<BattleUnitInput> inputs, RunRewards rewards = null)
        {
            _simulation = new BattleSimulation(runId, wave, inputs, rewards);
            ReadModel = new BattleReadModel(inputs);
        }

        /// <summary>
        /// 指定した陣営・スロットの現在の戦闘状態を返します。
        /// [KO] 지정한 팀·슬롯의 현재 전투 상태를 반환합니다.
        /// [EN] Returns the current combat state for the specified team and slot.
        /// </summary>
        public BattleUnitSnapshot GetUnit(Define.BattleTeam team, Define.BattleSlot slot)
        {
            return _simulation.GetUnit(team, slot);
        }

        /// <summary>
        /// BattleEntryPoint.Tick から秒数を受け取り、足りた時間だけ計算します。勝敗が決まった最後の通知も届けます。
        /// [KO] BattleEntryPoint.Tick에서 초를 받아 누적 시간이 찬 만큼 계산합니다. 승패가 난 마지막 tick의 알림도 전달합니다.
        /// [EN] Receives seconds from BattleEntryPoint.Tick and advances due steps, including notifications from the final step.
        /// </summary>
        public void Advance(double unscaledDeltaSeconds)
        {
            // 通知先がこのメソッドを呼び直すと、読み取り中のイベントが上書きされるため再入を拒否する。
            // 最終 Step では結果が既に確定しているので、終了判定より先に検査する。
            if (_isAdvancing)
            {
                throw new InvalidOperationException("Combat cannot advance from an event callback.");
            }

            if (_isDisposed || HasFaulted || Result != Define.BattleResult.Ongoing)
            {
                return;
            }

            if (double.IsNaN(unscaledDeltaSeconds) || double.IsInfinity(unscaledDeltaSeconds) ||
                unscaledDeltaSeconds < 0 || double.IsInfinity(unscaledDeltaSeconds * Define.TicksPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            }

            _isAdvancing = true;
            try
            {
                // 秒を tick 数に換算する。例: 0.025 秒なら 0.5 tick なので次回まで待ち、2 回分で 1 tick 進む。
                _pendingTicks += unscaledDeltaSeconds * Define.TicksPerSecond;
                // 遅れたフレームで計算を詰め込みすぎないよう、一度に最大 8 tick。残りは _pendingTicks に残す。
                for (var step = 0; step < MaxTicksPerAdvance && _pendingTicks >= 1; step++)
                {
                    _pendingTicks -= 1;
                    _simulation.Step();
                    for (var index = 0; index < _simulation.EventCount; index++)
                    {
                        // 計算済みの状態を ReadModel に反映してから、表示側へイベントの値を通知する。
                        var battleEvent = _simulation.GetEvent(index);
                        ReadModel.Apply(battleEvent);
                        EventOccurred?.Invoke(battleEvent);
                        if (_isDisposed)
                        {
                            return;
                        }
                    }

                    // この tick の全通知が終わったことをコアに伝え、次の Step でイベント配列を再利用できるようにする。
                    _simulation.AcknowledgeEvents();
                    if (Result != Define.BattleResult.Ongoing)
                    {
                        _pendingTicks = 0;
                        break;
                    }
                }
            }
            catch
            {
                // イベント処理の途中で失敗した戦闘は停止する。
                HasFaulted = true;
                throw;
            }
            finally
            {
                _isAdvancing = false;
            }
        }

        /// <summary>
        /// 終了後の tick と通知を無効にします。
        /// [KO] 종료 후 tick과 알림을 무효화합니다.
        /// [EN] Disables subsequent ticks and notifications.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            EventOccurred = null;
            ReadModel.Dispose();
        }
    }
}
