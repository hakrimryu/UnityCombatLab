using System;
using System.Collections.Generic;
using UnityCombatLab.Application.ReadModels;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;

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
        /// 一フレームの上限。残時間は捨てず次回へ持ち越します。
        /// [KO] 한 프레임 상한이며 남은 시간은 다음 호출로 보존합니다.
        /// [EN] Per-frame limit; remaining time carries into the next call.
        /// </summary>
        public const int MaxTicksPerAdvance = 8;

        /// <summary>
        /// 状態変更権を公開しないシミュレーション。
        /// [KO] 상태 변경권을 공개하지 않는 시뮬레이션.
        /// [EN] Simulation whose mutation access stays private.
        /// </summary>
        private readonly BattleSimulation _simulation;

        /// <summary>
        /// 未処理の時間を tick 単位で保持します。
        /// [KO] 미처리 시간을 tick 단위로 보관합니다.
        /// [EN] Retains pending time in tick units.
        /// </summary>
        private double _pendingTicks;

        /// <summary>
        /// 再入による同一バッファの二重消費を拒否します。
        /// [KO] 재진입으로 같은 버퍼를 중복 소비하지 않게 합니다.
        /// [EN] Rejects reentrant consumption of the same buffer.
        /// </summary>
        private bool _isAdvancing;

        /// <summary>
        /// 通知元の二重破棄を防ぎます。
        /// [KO] 알림 원본의 중복 해제를 막습니다.
        /// [EN] Prevents duplicate disposal of notification sources.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 失敗した消費から戦闘を再開しません。
        /// [KO] 실패한 소비 지점부터 전투를 재개하지 않습니다.
        /// [EN] Prevents resuming combat after a failed consumer.
        /// </summary>
        public bool HasFaulted { get; private set; }

        /// <summary>
        /// Runner が所有する表示モデル。
        /// [KO] Runner가 소유하는 표시 모델.
        /// [EN] Display model owned by this runner.
        /// </summary>
        public BattleReadModel ReadModel { get; }

        /// <summary>
        /// 完了した論理 tick。
        /// [KO] 완료한 논리 tick.
        /// [EN] Completed logical ticks.
        /// </summary>
        public int CurrentTick => _simulation.CurrentTick;

        /// <summary>
        /// コアが確定した結果。
        /// [KO] 코어가 확정한 결과.
        /// [EN] Outcome committed by the core.
        /// </summary>
        public Define.BattleResult Result => _simulation.Result;

        /// <summary>
        /// Step 後、イベント一件ごとに BattlePresenter などをその場で呼ぶ通知。呼び出しが戻るまで次へ進みません。
        /// [KO] Step 후 이벤트마다 BattlePresenter 등을 즉시 호출하는 알림입니다. 호출이 돌아올 때까지 다음 처리로 넘어가지 않습니다.
        /// [EN] Calls BattlePresenter and other listeners for each event after Step, waiting for each call to return.
        /// </summary>
        public event Action<BattleEvent> EventOccurred;

        /// <summary>
        /// コア検証後に表示モデルを生成します。
        /// [KO] 코어 검증 후 표시 모델을 생성합니다.
        /// [EN] Creates the display model after core validation.
        /// </summary>
        public BattleRunner(Guid runId, int wave, IReadOnlyList<BattleUnitInput> inputs)
        {
            _simulation = new BattleSimulation(runId, wave, inputs);
            ReadModel = new BattleReadModel(inputs);
        }

        /// <summary>
        /// 副作用なしの状態コピーを返します。
        /// [KO] 부수 효과 없는 상태 복사본을 반환합니다.
        /// [EN] Returns a side-effect-free state snapshot.
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
                        // コアの再利用配列ではなく、イベント一件の値を取り出す。
                        // 先に表示用 HP を更新すると R3 が HUD を更新し、次に EventOccurred で演出を呼ぶ。
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
                // 消費失敗を成功確認せず、この戦闘を再開不能にする。
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
