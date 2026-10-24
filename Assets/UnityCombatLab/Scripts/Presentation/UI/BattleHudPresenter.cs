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
        /// HP・防御膜・結果の Subscribe が返す解除用ハンドルをまとめ、Dispose 時に全て解除します。
        /// [KO] HP·방어막·결과의 Subscribe가 반환한 해제용 핸들을 모아 Dispose할 때 모두 해제합니다.
        /// [EN] Collects handles returned by HP, shield and outcome subscriptions so Dispose can detach them all.
        /// </summary>
        private readonly CompositeDisposable _subscriptions = new();

        /// <summary>
        /// 更新先の HUD。ここは表示を更新するだけで、HUD オブジェクトの生成・破棄は BattleEntryPoint が行います。
        /// [KO] 갱신할 HUD입니다. 여기서는 표시만 갱신하며 HUD 오브젝트의 생성·파괴는 BattleEntryPoint가 담당합니다.
        /// [EN] HUD to update; BattleEntryPoint creates and destroys its GameObject, while this class only updates it.
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
                    // データと HUD は同じ配列順。例: index 0 の騎士の HP は index 0 のカードだけへ表示する。
                    var unit = model.Units[index];
                    var status = view.Units[index];
                    status.Initialize(RoleName(unit.Input.Role), unit.Input.Team);
                    // Subscribe した時に現在 HP が一度届き、以後は HP が変わるたびに value に新しい HP が届く。
                    // 例: 命中で 600 → 590 なら SetHealth(590, 600, true) を呼び、追加の予告待機を入れない。
                    // AddTo はこの接続を解除用リストへ入れる。Presenter.Dispose でまとめて通知を止める。
                    unit.Health.Subscribe(value =>
                    {
                        if (!_isDisposed && status != null)
                        {
                            status.SetHealth(value, unit.Input.MaxHealth, true);
                        }
                    }).AddTo(_subscriptions);
                    // 予約被害は数字を変えず予告範囲だけに伝える。命中した値は上の Health 接続から届く。
                    unit.PreviewHealth.Subscribe(value =>
                    {
                        if (!_isDisposed && status != null)
                        {
                            status.SetPreviewHealth(value, unit.Input.MaxHealth);
                        }
                    }).AddTo(_subscriptions);
                    // 防御膜も別に接続する。防御膜だけが減った時は HP の表示を更新する必要がない。
                    unit.Shield.Subscribe(value =>
                    {
                        if (!_isDisposed && status != null)
                        {
                            status.SetShield(value);
                        }
                    }).AddTo(_subscriptions);
                }

                // 最初は「戦闘中」、最後の命中と死亡表示が終わった通知で「勝利」などへ切り替える。
                model.Result.Subscribe(HandleResultChanged).AddTo(_subscriptions);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 画面用語をゲーム規約に合わせます。
        /// [KO] 화면 용어를 게임 규약에 맞춥니다.
        /// [EN] Maps display terms to the game contract.
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
        /// 単一戦闘の結果だけを表示します。
        /// [KO] 단일 전투 결과만 표시합니다.
        /// [EN] Displays only this battle's outcome.
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
            }, result != Define.BattleResult.Ongoing);
        }

        /// <summary>
        /// 通知より先に無効化し、所有購読を全て解除します。
        /// [KO] 먼저 무효화한 후 소유 구독을 모두 해제합니다.
        /// [EN] Invalidates first, then disposes all owned subscriptions.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            // 以後は ReadModel の HP が変わっても、この Presenter の表示更新処理は呼ばれない。
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
