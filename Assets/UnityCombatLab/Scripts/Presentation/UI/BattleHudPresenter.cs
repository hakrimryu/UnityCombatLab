using System;
using R3;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 画面用 HP・防御膜の変更通知を受け、対応する HUD を更新します。Dispose で通知の接続を外します。
    /// [KO] 화면용 HP·방어막의 변경 알림을 받아 해당 HUD를 갱신합니다. Dispose에서 알림 연결을 해제합니다.
    /// [EN] Receives display health and shield changes and updates the matching HUD; Dispose detaches notifications.
    /// </summary>
    public sealed class BattleHudPresenter : IDisposable
    {
        /// <summary>
        /// HP・盾・勝敗の通知接続をまとめ、Dispose で解除します。
        /// [KO] HP·방어막·승패의 알림 연결을 모아 Dispose에서 해제합니다.
        /// [EN] Groups health, shield and outcome subscriptions for disposal.
        /// </summary>
        private readonly CompositeDisposable _subscriptions = new();

        /// <summary>
        /// HP・盾・勝敗を表示する HUD。生成と破棄は BattleEntryPoint が担当します。
        /// [KO] HP·방어막·승패를 표시할 HUD이며 생성·파괴는 BattleEntryPoint가 담당합니다.
        /// [EN] HUD displaying health, shields and the outcome, created and destroyed by BattleEntryPoint.
        /// </summary>
        private readonly BattleHudView _view;

        /// <summary>
        /// 重複解除を防ぐ状態。
        /// [KO] 중복 해제를 방지하는 상태.
        /// [EN] State preventing duplicate disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// BattleEntryPoint.Start が呼びます。初期 HP をすぐ表示し、その後は値が変わった時だけ同じ HUD を更新します。
        /// [KO] BattleEntryPoint.Start가 호출합니다. 초기 HP를 즉시 표시하고 이후에는 값이 바뀔 때 같은 HUD를 갱신합니다.
        /// [EN] Called by BattleEntryPoint.Start to display initial health immediately and then update the HUD on changes.
        /// </summary>
        public BattleHudPresenter(BattlePresentationReadModel model, BattleHudView view, GameColorPalette colors)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            view.Validate(model.Units.Count);
            try
            {
                // 同じパレットを固定表示と状態別表示へ渡してから、R3 の初期通知を受け取る。
                view.ApplyPalette(colors);
                for (var index = 0; index < model.Units.Count; index++)
                {
                    // 同じ index のユニットと状態パネルを接続する。
                    var unit = model.Units[index];
                    var status = view.Units[index];
                    status.Initialize(RoleName(unit.Input.Role), unit.Input.Team);
                    // 初期 HP と命中後の HP を対応するパネルへ表示する。
                    unit.Health.Subscribe(value =>
                    {
                        if (!_isDisposed && status != null)
                        {
                            status.SetHealth(value, unit.Input.MaxHealth);
                        }
                    }).AddTo(_subscriptions);
                    // 命中前の被害はゲージの損失予告へ反映する。
                    unit.PreviewHealth.Subscribe(value =>
                    {
                        if (!_isDisposed && status != null)
                        {
                            status.SetPreviewHealth(value, unit.Input.MaxHealth);
                        }
                    }).AddTo(_subscriptions);
                    unit.Shield.Subscribe(value =>
                    {
                        if (!_isDisposed && status != null)
                        {
                            status.SetShield(value);
                        }
                    }).AddTo(_subscriptions);
                }

                // 演出終了後に公開された勝敗で結果表示を切り替える。
                model.Result.Subscribe(HandleResultChanged).AddTo(_subscriptions);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// ユニットの役割を日本語の表示名へ変換します。
        /// [KO] 유닛 역할을 일본어 표시 이름으로 변환합니다.
        /// [EN] Converts a unit role to its Japanese display name.
        /// </summary>
        private static string RoleName(Define.UnitRole role)
        {
            return role switch
            {
                Define.UnitRole.Knight => "騎士",
                Define.UnitRole.Ranger => "弓使い",
                Define.UnitRole.Mage => "魔法使い",
                Define.UnitRole.SkeletonMelee => "骸骨兵",
                Define.UnitRole.SkeletonRanged => "骸骨射手",
                Define.UnitRole.SkeletonCaptain => "骸骨隊長",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        /// <summary>
        /// 通知された勝敗に応じて、HUD の結果表示を切り替えます。
        /// [KO] 알림으로 받은 승패에 따라 HUD 결과 표시를 전환합니다.
        /// [EN] Switches the HUD result display for the notified battle outcome.
        /// </summary>
        private void HandleResultChanged(Define.BattleResult result)
        {
            if (_isDisposed || _view == null)
            {
                return;
            }

            _view.SetResult(result switch
            {
                Define.BattleResult.Ongoing => "戦闘中",
                Define.BattleResult.Victory => "勝利",
                Define.BattleResult.Defeat => "パーティが全滅しました。",
                Define.BattleResult.Timeout => "制限時間に達しました。",
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            }, result);
        }

        /// <summary>
        /// HP・盾・勝敗の通知を解除し、HUD の一時表示を消します。
        /// [KO] HP·방어막·승패 알림을 해제하고 HUD의 일시 표시를 지웁니다.
        /// [EN] Unsubscribes from health, shield and outcome changes and clears transient HUD displays.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _subscriptions.Dispose();
            if (_view != null)
            {
                foreach (var unit in _view.Units)
                {
                    if (unit != null)
                    {
                        unit.ClearTransient();
                    }
                }
            }
        }
    }
}
