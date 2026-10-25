using System;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 共通prefabに英雄データと選択表示を適用し、選択した職業だけを通知します。
    /// [KO] 공통 prefab에 영웅 데이터와 선택 표시를 적용하고 선택한 직업만 알립니다.
    /// [EN] Applies hero data and selection visuals to a shared prefab and emits the selected role.
    /// </summary>
    public sealed class HeroCardView : MonoBehaviour
    {
        /// <summary>
        /// カードの入力と表示部品。共通prefab内の子だけを参照します。
        /// [KO] 카드의 입력과 표시 부품이며 공통 prefab 내부 자식만 참조합니다.
        /// [EN] Card input and graphics, referencing only children of the shared prefab.
        /// </summary>
        [SerializeField] private Button _button;
        /// <summary>
        /// データから職業名と能力を表示する文字。
        /// [KO] 데이터로 직업 이름과 능력을 표시할 글자입니다.
        /// [EN] Text displaying the role name and stats from data.
        /// </summary>
        [SerializeField] private TMP_Text _description;
        /// <summary>
        /// データの職業画像を受け取る表示先。
        /// [KO] 데이터의 직업 이미지를 받을 표시 대상입니다.
        /// [EN] Graphic receiving the role image from data.
        /// </summary>
        [SerializeField] private Image _icon;
        /// <summary>
        /// 選択状態に合わせて色を変える背景。
        /// [KO] 선택 상태에 맞춰 색을 바꿀 배경입니다.
        /// [EN] Background tinted according to selection.
        /// </summary>
        [SerializeField] private Image _background;
        /// <summary>
        /// 選択中だけ表示するチェック印。
        /// [KO] 선택한 동안에만 표시할 체크 표시입니다.
        /// [EN] Checkmark visible only while selected.
        /// </summary>
        [SerializeField] private GameObject _selectionBadge;
        /// <summary>
        /// 選択背景色。未選択は白です。
        /// [KO] 선택 배경색이며 미선택은 흰색입니다.
        /// [EN] Selected background color; unselected is white.
        /// </summary>
        [SerializeField] private Color _selectedColor;

        /// <summary>
        /// 現在のクリックが選ぶ職業と、一度だけ接続する入力の状態。
        /// [KO] 현재 클릭이 선택할 직업과 한 번만 연결할 입력 상태입니다.
        /// [EN] Role selected by the current click and one-time input setup state.
        /// </summary>
        private Define.UnitRole _role;
        /// <summary>
        /// 再設定時の入力接続重複を防ぎます。
        /// [KO] 재설정 시 입력 연결 중복을 막습니다.
        /// [EN] Prevents duplicate input connections on rebinding.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// Presenterへ転送する職業選択。
        /// [KO] Presenter로 전달할 직업 선택입니다.
        /// [EN] Role selection forwarded to the presenter.
        /// </summary>
        public event Action<Define.UnitRole> Selected;

        /// <summary>
        /// 生成時にデータ・共通色を設定します。再設定しても入力通知を重複させません。
        /// [KO] 생성 시 데이터·공통 색상을 설정하며 재설정해도 입력 알림을 중복하지 않습니다.
        /// [EN] Sets data and palette on creation without duplicating input notifications on rebinding.
        /// </summary>
        public void Bind(HeroCardData data, GameColorPalette colors)
        {
            if (data == null || colors == null)
                throw new ArgumentNullException(data == null ? nameof(data) : nameof(colors));

            if (_button == null || _description == null || _icon == null || _background == null || _selectionBadge == null)
                throw new InvalidOperationException("HeroCard requires its button, description, icon, background and selection badge.");

            _role = data.Role;
            _description.text = data.Description;
            _description.color = colors.GetColor(Define.PaletteColor.Text);
            _icon.sprite = data.Icon;
            _icon.color = data.IconTint;
            Show(data.Description, false);
            if (_isInitialized)
                return;

            _button.onClick.AddListener(HandleClicked);
            _isInitialized = true;
        }

        /// <summary>
        /// 同じ表示値から説明・背景色・バッジを一緒に更新します。
        /// [KO] 같은 표시 값으로 설명·배경색·배지를 함께 갱신합니다.
        /// [EN] Updates description, background and badge from the same display values.
        /// </summary>
        public void Show(string description, bool isSelected)
        {
            _description.text = description;
            _background.color = isSelected ? _selectedColor : Color.white;
            _selectionBadge.SetActive(isSelected);
        }

        /// <summary>
        /// 入力元のカードに設定された職業を通知します。
        /// [KO] 입력한 카드에 설정된 직업을 알립니다.
        /// [EN] Emits the role assigned to the clicked card.
        /// </summary>
        private void HandleClicked()
            => Selected?.Invoke(_role);

        /// <summary>
        /// カード終了時に入力と外部通知を解除します。
        /// [KO] 카드 종료 시 입력과 외부 알림을 해제합니다.
        /// [EN] Disconnects input and external notifications when the card ends.
        /// </summary>
        private void OnDestroy()
        {
            if (_button != null && _isInitialized)
                _button.onClick.RemoveListener(HandleClicked);

            Selected = null;
        }
    }
}
