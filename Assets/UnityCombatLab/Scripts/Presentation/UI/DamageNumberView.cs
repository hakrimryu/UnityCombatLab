using System;
using DG.Tweening;
using TMPro;
using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 一人のユニットに届いた HP 損失と盾吸収を三枠で示し、各表示の終了と解除を管理します。
    /// [KO] 한 유닛이 받은 HP 손실과 방어막 흡수를 세 슬롯에 표시하고 각 표시의 종료와 해제를 관리합니다.
    /// [EN] Shows one unit's health loss and shield absorption in three slots and owns their expiry and cleanup.
    /// </summary>
    public sealed class DamageNumberView : MonoBehaviour
    {
        /// <summary>
        /// 命中から数字を消すまでの秒数。各枠は別々にこの時間を数えます。
        /// [KO] 명중부터 숫자를 지울 때까지의 초 단위 시간이며 각 슬롯이 독립적으로 셉니다.
        /// [EN] Seconds from impact until a number clears, counted independently for each slot.
        /// </summary>
        private const float DisplaySeconds = 0.55f;

        /// <summary>
        /// プレハブで接続した三つの表示枠。数字、アイコン、タイマーを同じ枠で保持します。
        /// [KO] 프리팹에서 연결한 세 표시 슬롯이며 숫자·아이콘·타이머를 같은 슬롯에 보관합니다.
        /// [EN] Three prefab-authored slots, each keeping its numbers, icon and timer together.
        /// </summary>
        [SerializeField] private DamageSlot[] _slots = Array.Empty<DamageSlot>();

        /// <summary>
        /// 同時表示の上限を越えた時に 0→1→2→0 の順で再利用する枠。
        /// [KO] 동시 표시 한도를 넘으면 0→1→2→0 순서로 재사용할 슬롯입니다.
        /// [EN] Slot reused in the order 0, 1, 2, then 0 when concurrent hits exceed capacity.
        /// </summary>
        private int _nextSlot;

        /// <summary>
        /// 一回の被害表示に必要な参照と、古い終了処理を拒否する番号をまとめます。
        /// [KO] 한 피해 표시에 필요한 참조와 이전 종료 처리를 거부할 번호를 함께 보관합니다.
        /// [EN] Groups one hit's display references with the version that rejects stale completion callbacks.
        /// </summary>
        [Serializable]
        private sealed class DamageSlot
        {
            /// <summary>
            /// HP 損失の数字を表示する TMP。
            /// [KO] HP 손실 수치를 표시하는 TMP입니다.
            /// [EN] TMP label displaying health loss.
            /// </summary>
            [SerializeField] private TMP_Text _healthDamage;

            /// <summary>
            /// HP 損失とは別の行で盾吸収を示す TMP。
            /// [KO] HP 손실과 다른 줄에 방어막 흡수를 표시하는 TMP입니다.
            /// [EN] TMP label displaying shield absorption on a separate line.
            /// </summary>
            [SerializeField] private TMP_Text _shieldDamage;

            /// <summary>
            /// 盾吸収がある間だけ数字に添えるアイコン。
            /// [KO] 방어막 흡수가 있을 때만 숫자 옆에 표시할 아이콘입니다.
            /// [EN] Icon shown beside the number only when shield damage exists.
            /// </summary>
            [SerializeField] private UnityEngine.UI.Image _shieldIcon;

            /// <summary>
            /// この枠だけの表示終了を予約する Tween。再利用と解除で停止します。
            /// [KO] 이 슬롯의 표시 종료를 예약하는 Tween이며 재사용·해제 때 멈춥니다.
            /// [EN] Tween scheduling this slot's expiry, stopped on reuse and cleanup.
            /// </summary>
            private Tween _timer;

            /// <summary>
            /// 再表示または解除ごとに増やし、前の表示の callback を無効にする番号。
            /// [KO] 재표시나 해제마다 증가하여 이전 표시 콜백을 무효화하는 번호입니다.
            /// [EN] Counter increased on display or cleanup to invalidate callbacks from earlier uses.
            /// </summary>
            private int _version;

            /// <summary>
            /// 数字とアイコンが全て接続されているか確認します。
            /// [KO] 숫자와 아이콘이 모두 연결되었는지 확인합니다.
            /// [EN] Checks that both labels and the icon are assigned.
            /// </summary>
            public void Validate()
            {
                if (_healthDamage == null || _shieldDamage == null || _shieldIcon == null)
                    throw new InvalidOperationException("Each damage slot requires two labels and a shield icon.");
            }

            /// <summary>
            /// HUD が所有する一つの材質を両方の数字へ借用し、ここでは複製も破棄もしません。
            /// [KO] HUD 소유 재질 하나를 두 숫자에 빌려 쓰며 여기서는 복제하거나 파괴하지 않습니다.
            /// [EN] Borrows the HUD-owned material for both labels without cloning or destroying it.
            /// </summary>
            public void ApplyMaterial(Material material)
            {
                _healthDamage.fontSharedMaterial = material;
                _shieldDamage.fontSharedMaterial = material;
            }

            /// <summary>
            /// 前の予約を止めて新しい数字を表示し、この使用番号の時だけ終了させます。
            /// [KO] 이전 예약을 멈추고 새 숫자를 표시하며 이번 사용 번호일 때만 종료합니다.
            /// [EN] Replaces the previous timer with new numbers and expires only this use of the slot.
            /// </summary>
            public void Show(DamageNumberView owner, int healthDamage, int shieldDamage)
            {
                Clear();
                var version = _version;
                _healthDamage.text = healthDamage > 0 ? "-" + healthDamage : "";
                _shieldDamage.text = shieldDamage > 0 ? "-" + shieldDamage : "";
                _shieldIcon.gameObject.SetActive(shieldDamage > 0);
                // 再利用と OnDisable は番号を変える。Kill 後に古い callback が呼ばれても新表示を消さない。
                // owner は Unity の破棄済み判定に使い、表示と同じ scaled 時間でこの枠だけを終了する。
                _timer = DOVirtual.DelayedCall(DisplaySeconds, () =>
                {
                    if (owner != null && version == _version)
                    {
                        _timer = null;
                        Clear();
                    }
                }, false);
            }

            /// <summary>
            /// 古い終了処理を無効にしてタイマーと数字を消します。未初期化時の解除も受け付けます。
            /// [KO] 이전 종료 처리를 무효화하고 타이머와 숫자를 지우며 초기화 전 해제도 허용합니다.
            /// [EN] Invalidates old completions and clears the timer and labels, including cleanup before initialization.
            /// </summary>
            public void Clear()
            {
                _version++;
                _timer?.Kill();
                _timer = null;
                if (_healthDamage != null) _healthDamage.text = "";
                if (_shieldDamage != null) _shieldDamage.text = "";
                if (_shieldIcon != null) _shieldIcon.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 実行前にプレハブの三枠と各参照を確認します。
        /// [KO] 실행 전에 프리팹의 세 슬롯과 각 참조를 확인합니다.
        /// [EN] Checks the prefab's three slots and their references before use.
        /// </summary>
        private void Awake() => Validate();

        /// <summary>
        /// 三枠が揃っていることを、材質設定前にも確認します。
        /// [KO] 재질 설정 전에도 세 슬롯이 갖춰졌는지 확인합니다.
        /// [EN] Checks that all three slots exist, including before material assignment.
        /// </summary>
        private void Validate()
        {
            if (_slots == null || _slots.Length != Define.SlotsPerTeam)
                throw new InvalidOperationException("Damage numbers require three authored slots.");
            foreach (var slot in _slots)
            {
                if (slot == null) throw new InvalidOperationException("Damage slots cannot be null.");
                slot.Validate();
            }
        }

        /// <summary>
        /// BattleHudView から渡された共通数字材質を全枠へ適用します。
        /// [KO] BattleHudView가 전달한 공통 숫자 재질을 모든 슬롯에 적용합니다.
        /// [EN] Applies the shared number material supplied by BattleHudView to every slot.
        /// </summary>
        public void ApplyMaterial(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            Validate();
            foreach (var slot in _slots) slot.ApplyMaterial(material);
        }

        /// <summary>
        /// UnitStatusView が命中時に呼び、次の枠へ HP 損失と盾吸収を一緒に表示します。
        /// [KO] UnitStatusView가 명중 시 호출하여 다음 슬롯에 HP 손실과 방어막 흡수를 함께 표시합니다.
        /// [EN] Called by UnitStatusView on impact to show health loss and shield absorption in the next slot.
        /// </summary>
        public void Show(int healthDamage, int shieldDamage)
        {
            var slot = _slots[_nextSlot];
            _nextSlot = (_nextSlot + 1) % _slots.Length;
            slot.Show(this, healthDamage, shieldDamage);
        }

        /// <summary>
        /// 全枠の予約と表示を解除し、次回は最初の枠から再開します。
        /// [KO] 모든 슬롯의 예약과 표시를 해제하고 다음에는 첫 슬롯부터 재개합니다.
        /// [EN] Clears every slot and timer so the next hit starts at the first slot.
        /// </summary>
        public void Clear()
        {
            _nextSlot = 0;
            if (_slots == null) return;
            foreach (var slot in _slots) slot?.Clear();
        }

        /// <summary>
        /// 非表示または破棄時に所有する全タイマーを解除します。
        /// [KO] 비활성화 또는 파괴 시 소유한 모든 타이머를 해제합니다.
        /// [EN] Releases all owned timers when hidden or destroyed.
        /// </summary>
        private void OnDisable() => Clear();
    }
}
