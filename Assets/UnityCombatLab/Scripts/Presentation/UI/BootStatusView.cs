using System;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// Boot 専用の起動表示を設定します。Single 切替時に Canvas と共に破棄されます。
    /// [KO] Boot 전용 시작 표시를 설정하며 Single 전환 시 Canvas와 함께 파괴됩니다.
    /// [EN] Initializes the Boot-only startup display, destroyed with its canvas on Single transition.
    /// </summary>
    public sealed class BootStatusView : MonoBehaviour
    {
        /// <summary>
        /// Boot Canvas にある、ゲームタイトルを表示する起動ラベル。
        /// [KO] 게임 제목을 표시할 Boot Canvas의 시작 라벨입니다.
        /// [EN] Startup label on the Boot canvas displaying the game title.
        /// </summary>
        [SerializeField] private Text _statusText;

        /// <summary>
        /// 起動文字に使う OnBackground 色を取得する共通パレット。
        /// [KO] 시작 글자에 사용할 OnBackground 색상을 가져올 공통 팔레트입니다.
        /// [EN] Shared palette providing the OnBackground color for startup text.
        /// </summary>
        [SerializeField] private GameColorPalette _colors;

        /// <summary>
        /// Boot 読込時にタイトルとパレットの OnBackground 色をラベルへ適用します。
        /// [KO] Boot 로드 시 라벨에 제목과 팔레트의 OnBackground 색상을 적용합니다.
        /// [EN] Applies the title and palette OnBackground color to the label when Boot loads.
        /// </summary>
        private void Awake()
        {
            if (_statusText == null || _colors == null)
                throw new InvalidOperationException("Boot status requires its label and shared palette.");
            _statusText.text = "Unity Combat Lab";
            _statusText.color = _colors.GetColor(Define.PaletteColor.OnBackground);
        }
    }
}
