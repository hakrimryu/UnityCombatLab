using System;
using UnityCombatLab.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.Styling
{
    /// <summary>
    /// 各画面に保存するUI部品と色の役割の組。FrontendViewとBattleHudViewが開始時に適用します。
    /// [KO] 각 화면에 저장하는 UI 요소와 색상 역할의 쌍이며 FrontendView와 BattleHudView가 시작 시 적용합니다.
    /// [EN] A graphic and color role stored in each screen and applied by FrontendView or BattleHudView at startup.
    /// </summary>
    [Serializable]
    public sealed class PaletteGraphicBinding
    {
        /// <summary>
        /// パレットの色を適用する 画面内のImage または文字。
        /// [KO] 팔레트 색상을 적용할 화면의 Image 또는 글자입니다.
        /// [EN] Screen image or text that receives the palette color.
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
