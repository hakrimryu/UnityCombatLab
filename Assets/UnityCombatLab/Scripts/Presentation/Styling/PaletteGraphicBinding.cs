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
        /// 色を受け取る既存の Image または文字。購入原本ではなく HUD の部品を指定します。
        /// [KO] 색상을 적용할 기존 Image 또는 텍스트입니다. 구매 원본 대신 HUD 안의 요소를 지정합니다.
        /// [EN] Existing image or text in the HUD instance, rather than a purchased source asset.
        /// </summary>
        [SerializeField] private Graphic _graphic;

        /// <summary>
        /// 色の値ではなく用途だけを保存します。
        /// [KO] 색상 값 대신 용도만 저장합니다.
        /// [EN] Stores the color's purpose instead of its value.
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
