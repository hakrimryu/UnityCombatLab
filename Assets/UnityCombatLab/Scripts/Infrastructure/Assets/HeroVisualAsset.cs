using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 英雄カードへ渡す画像参照と色をカタログに保存します。
    /// [KO] 영웅 카드에 전달할 이미지 참조와 색상을 카탈로그에 저장합니다.
    /// [EN] Stores a hero card's image reference and tint in the catalog.
    /// </summary>
    [Serializable]
    public sealed class HeroVisualAsset
    {
        /// <summary>
        /// ローカルグループにある職業アイコン。
        /// [KO] 로컬 그룹에 있는 직업 아이콘입니다.
        /// [EN] Role icon in a local group.
        /// </summary>
        [SerializeField] private AssetReferenceSprite _icon;

        /// <summary>
        /// 職業ごとのアイコン色。
        /// [KO] 직업별 아이콘 색상입니다.
        /// [EN] Icon tint for this role.
        /// </summary>
        [SerializeField] private Color _tint = Color.white;

        /// <summary>
        /// 型付きローダーへ渡すアイコン参照。
        /// [KO] 타입 지정 로더로 넘길 아이콘 참조입니다.
        /// [EN] Icon reference supplied to the typed loader.
        /// </summary>
        public AssetReferenceSprite Icon => _icon;

        /// <summary>
        /// 読込後の画像へ適用する色。
        /// [KO] 로드 후 이미지에 적용할 색상입니다.
        /// [EN] Color applied after loading the image.
        /// </summary>
        public Color Tint => _tint;
    }
}
