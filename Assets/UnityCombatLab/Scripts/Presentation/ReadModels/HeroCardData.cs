using System;
using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// 読込済み画像と実際の能力説明を一枚のカードへ渡す表示データ。
    /// [KO] 로드된 이미지와 실제 능력 설명을 카드 하나에 전달할 표시 데이터입니다.
    /// [EN] Display data supplying a loaded image and actual stat description to one card.
    /// </summary>
    public sealed class HeroCardData
    {
        /// <summary>
        /// クリック時に選ぶ職業。
        /// [KO] 클릭 시 선택할 직업입니다.
        /// [EN] Role selected on click.
        /// </summary>
        public Define.UnitRole Role { get; }
        /// <summary>
        /// 職業名と能力説明。
        /// [KO] 직업 이름과 능력 설명입니다.
        /// [EN] Role name and stat description.
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// 所有側がカードの破棄まで保持する画像。
        /// [KO] 소유자가 카드 파괴까지 유지할 이미지입니다.
        /// [EN] Image retained by the owner until the card is destroyed.
        /// </summary>
        public Sprite Icon { get; }
        /// <summary>
        /// 職業のアイコン色。
        /// [KO] 직업 아이콘 색상입니다.
        /// [EN] Role icon color.
        /// </summary>
        public Color IconTint { get; }

        /// <summary>
        /// 読込完了後にカードへ渡す値を確定します。
        /// [KO] 로딩 완료 후 카드에 전달할 값을 확정합니다.
        /// [EN] Captures the values supplied to a card after loading.
        /// </summary>
        public HeroCardData(Define.UnitRole role, string description, Sprite icon, Color iconTint)
        {
            if ((int)role < 0 || (int)role >= Define.SlotsPerTeam)
                throw new ArgumentOutOfRangeException(nameof(role));

            Role = role;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Icon = icon != null ? icon : throw new ArgumentNullException(nameof(icon));
            IconTint = iconTint;
        }
    }
}
