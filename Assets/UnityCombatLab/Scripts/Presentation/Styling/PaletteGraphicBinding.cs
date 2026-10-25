using System;
using UnityCombatLab.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.Styling
{
    /// <summary>
    /// HUD に保存する UI 部品と色の役割の組。BattleHudView が開始時にまとめて適用します。
    /// [KO] HUD에 저장하는 UI 요소와 색상 역할의 쌍입니다. BattleHudView가 시작 시 일괄 적용합니다.
    /// [EN] A graphic and palette role stored in the HUD and applied by BattleHudView at startup.
    /// </summary>
    [Serializable]
    public sealed class PaletteGraphicBinding
    {
        /// <summary>
        /// パレットの色を適用する HUD 内の Image または文字。
        /// [KO] 팔레트 색상을 적용할 HUD의 Image 또는 글자입니다.
        /// [EN] HUD image or text that receives the palette color.
        /// </summary>
        [SerializeField] private Graphic _graphic;

        /// <summary>
        /// この UI 部品に適用するパレット色の用途。
        /// [KO] 이 UI 요소에 적용할 팔레트 색상의 용도입니다.
        /// [EN] Palette role used to color this UI element.
        /// </summary>
        [SerializeField] private Define.PaletteColor _role;

        /// <summary>
        /// 共通アセットから色を読み、指定された部品だけを変更します。
        /// [KO] 공통 에셋에서 색을 읽어 지정한 요소만 변경합니다.
        /// [EN] Reads the shared asset and changes only the assigned graphic.
        /// </summary>
        public void Apply(GameColorPalette palette)
        {
            if (_graphic == null)
            {
                throw new InvalidOperationException("A palette binding requires its HUD graphic.");
            }

            _graphic.color = palette.GetColor(_role);
        }
    }
}
