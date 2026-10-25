using System;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 共通の配置枠に現在の英雄を表示し、設定された移動先を通知します。
    /// [KO] 공통 배치 슬롯에 현재 영웅을 표시하고 설정된 이동 위치를 알립니다.
    /// [EN] Shows the current hero in a shared slot and emits its assigned destination.
    /// </summary>
    public sealed class FormationSlotView : MonoBehaviour
    {
        /// <summary>
        /// 共通prefab内の配置ボタンと表示文字。
        /// [KO] 공통 prefab 내부 배치 버튼과 표시 글자입니다.
        /// [EN] Formation button and text within the shared prefab.
        /// </summary>
        [SerializeField] private Button _button;
        /// <summary>
        /// 現在の配置名と英雄名の表示先。
        /// [KO] 현재 배치 이름과 영웅 이름의 표시 대상입니다.
        /// [EN] Text displaying the current position and hero.
        /// </summary>
        [SerializeField] private TMP_Text _text;

        /// <summary>
        /// この枠に割り当てた配置先と入力接続状態。
        /// [KO] 이 슬롯에 할당한 배치 위치와 입력 연결 상태입니다.
        /// [EN] Destination assigned to this slot and input connection state.
        /// </summary>
        private Define.BattleSlot _slot;
        /// <summary>
        /// 再設定時の入力接続重複を防ぎます。
        /// [KO] 재설정 시 입력 연결 중복을 막습니다.
        /// [EN] Prevents duplicate input connections on rebinding.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// Presenterへ転送する配置先の選択。
        /// [KO] Presenter로 전달할 배치 위치 선택입니다.
        /// [EN] Destination selection forwarded to the presenter.
        /// </summary>
        public event Action<Define.BattleSlot> Selected;

        /// <summary>
        /// 枠の配置先と共通文字色を設定し、入力を一度接続します。
        /// [KO] 슬롯 위치와 공통 글자색을 설정하고 입력을 한 번 연결합니다.
        /// [EN] Assigns the destination and shared text color and connects input once.
        /// </summary>
        public void Bind(Define.BattleSlot slot, GameColorPalette colors)
        {
            if ((int)slot < 0 || (int)slot >= Define.SlotsPerTeam)
                throw new ArgumentOutOfRangeException(nameof(slot));

            if (colors == null || _button == null || _text == null)
                throw new InvalidOperationException("FormationSlot requires a palette, button and text.");

            _slot = slot;
            _text.color = colors.GetColor(Define.PaletteColor.OnAccent);
            if (_isInitialized)
                return;

            _button.onClick.AddListener(HandleClicked);
            _isInitialized = true;
        }

        /// <summary>
        /// 交換後の英雄と配置名を表示します。
        /// [KO] 교환 후 영웅과 배치 이름을 표시합니다.
        /// [EN] Displays the hero and position name after a swap.
        /// </summary>
        public void Show(string text)
            => _text.text = text;

        /// <summary>
        /// 入力された配置先を通知します。
        /// [KO] 입력한 배치 위치를 알립니다.
        /// [EN] Emits the clicked destination.
        /// </summary>
        private void HandleClicked()
            => Selected?.Invoke(_slot);

        /// <summary>
        /// 枠の破棄時に入力と外部通知を解除します。
        /// [KO] 슬롯 파괴 시 입력과 외부 알림을 해제합니다.
        /// [EN] Disconnects input and external notifications on destruction.
        /// </summary>
        private void OnDestroy()
        {
            if (_button != null && _isInitialized)
                _button.onClick.RemoveListener(HandleClicked);

            Selected = null;
        }
    }
}
