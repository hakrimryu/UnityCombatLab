using System;
using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Presentation.Styling
{
    /// <summary>
    /// プロジェクト共通の色設定。AppLifetimeScope が一つのアセットを登録し、画面の開始時に View へ渡します。
    /// [KO] 프로젝트 공통 색상 설정입니다. AppLifetimeScope가 에셋 하나를 등록해 화면 시작 시 View에 전달합니다.
    /// [EN] Project-wide colors registered as one asset by AppLifetimeScope and passed to views at screen startup.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCombatLab/Game Color Palette", fileName = "UI_GameColors")]
    public sealed class GameColorPalette : ScriptableObject
    {
        /// <summary>
        /// 味方の見出し・HP・役割印。
        /// [KO] 아군 제목·HP·역할 배지 색입니다.
        /// [EN] Ally headings, health and role badges.
        /// </summary>
        [SerializeField] private Color _ally;

        /// <summary>
        /// 敵の見出し・HP・役割印。
        /// [KO] 적 제목·HP·역할 배지 색입니다.
        /// [EN] Enemy headings, health and role badges.
        /// </summary>
        [SerializeField] private Color _enemy;

        /// <summary>
        /// パネルと吹き出しの明るい面。
        /// [KO] 패널과 말풍선의 밝은 면 색입니다.
        /// [EN] Light panel and callout surfaces.
        /// </summary>
        [SerializeField] private Color _surface;

        /// <summary>
        /// 明るい面に置く本文と HP 数字。
        /// [KO] 밝은 면 위의 본문과 HP 숫자 색입니다.
        /// [EN] Body text and HP numbers on light surfaces.
        /// </summary>
        [SerializeField] private Color _text;

        /// <summary>
        /// 戦闘不能と防御膜ゼロの表示。
        /// [KO] 전투 불능과 방어막 0의 표시 색입니다.
        /// [EN] Defeated states and zero-shield labels.
        /// </summary>
        [SerializeField] private Color _mutedText;

        /// <summary>
        /// 役割アイコンと HP 損失の数字。
        /// [KO] 역할 아이콘과 HP 피해 숫자 색입니다.
        /// [EN] Role icons and health-damage numbers.
        /// </summary>
        [SerializeField] private Color _onAccent;

        /// <summary>
        /// 暗い背景に置くタイトルと起動表示。
        /// [KO] 어두운 배경 위 제목과 시작 안내 색입니다.
        /// [EN] Title and startup text on the dark background.
        /// </summary>
        [SerializeField] private Color _onBackground;

        /// <summary>
        /// 残っている防御膜の数字。
        /// [KO] 남아 있는 방어막 수치 색입니다.
        /// [EN] Remaining shield values.
        /// </summary>
        [SerializeField] private Color _shield;

        /// <summary>
        /// 現在の防御膜に添えるアイコン。
        /// [KO] 현재 방어막 수치 옆의 아이콘 색입니다.
        /// [EN] Icons beside current shield values.
        /// </summary>
        [SerializeField] private Color _shieldIcon;

        /// <summary>
        /// 防御膜の吸収数字とアイコン。
        /// [KO] 방어막 흡수 숫자와 아이콘 색입니다.
        /// [EN] Shield-absorption numbers and icons.
        /// </summary>
        [SerializeField] private Color _shieldDamage;

        /// <summary>
        /// HP ゲージの固定枠。
        /// [KO] HP 게이지의 고정 테두리 색입니다.
        /// [EN] Fixed health-gauge borders.
        /// </summary>
        [SerializeField] private Color _barBorder;

        /// <summary>
        /// HP ゲージの減った領域。
        /// [KO] HP 게이지의 소모된 영역 색입니다.
        /// [EN] Depleted areas of health gauges.
        /// </summary>
        [SerializeField] private Color _barEmpty;

        /// <summary>
        /// パネルの影。透明度もこの値で指定します。
        /// [KO] 패널 그림자 색입니다. 투명도도 이 값에서 지정합니다.
        /// [EN] Panel shadows, including their opacity.
        /// </summary>
        [SerializeField] private Color _shadow;

        /// <summary>
        /// 戦闘カメラが描く舞台の外側。
        /// [KO] 전투 카메라가 그리는 무대 바깥 배경색입니다.
        /// [EN] Camera background outside the battle stage.
        /// </summary>
        [SerializeField] private Color _background;

        /// <summary>
        /// HP 損失と吸収数字の輪郭。
        /// [KO] HP 손실과 흡수 숫자의 외곽선 색입니다.
        /// [EN] Outlines around health-loss and absorption numbers.
        /// </summary>
        [SerializeField] private Color _damageOutline;

        /// <summary>
        /// 本体ゲージが減る前に損失範囲を示す色。
        /// [KO] 본 게이지가 줄기 전에 손실 구간을 보여주는 색입니다.
        /// [EN] Color highlighting the loss region before the main gauge shrinks.
        /// </summary>
        [SerializeField] private Color _damagePreview;

        /// <summary>
        /// 減少後に短時間残す直前 HP の色。
        /// [KO] 감소 후 잠시 남기는 직전 HP 색입니다.
        /// [EN] Color of old health briefly retained after damage.
        /// </summary>
        [SerializeField] private Color _trailingHealth;

        /// <summary>
        /// View が要求した役割の色を返します。色の実値は Inspector のアセットだけで編集します。
        /// [KO] View가 요청한 역할의 색을 반환합니다. 실제 색상 값은 Inspector의 에셋에서만 편집합니다.
        /// [EN] Returns the color for a view role; actual values are edited only in the asset Inspector.
        /// </summary>
        public Color GetColor(Define.PaletteColor role)
        {
            return role switch
            {
                Define.PaletteColor.Ally => _ally,
                Define.PaletteColor.Enemy => _enemy,
                Define.PaletteColor.Surface => _surface,
                Define.PaletteColor.Text => _text,
                Define.PaletteColor.MutedText => _mutedText,
                Define.PaletteColor.OnAccent => _onAccent,
                Define.PaletteColor.OnBackground => _onBackground,
                Define.PaletteColor.Shield => _shield,
                Define.PaletteColor.ShieldIcon => _shieldIcon,
                Define.PaletteColor.ShieldDamage => _shieldDamage,
                Define.PaletteColor.BarBorder => _barBorder,
                Define.PaletteColor.BarEmpty => _barEmpty,
                Define.PaletteColor.Shadow => _shadow,
                Define.PaletteColor.Background => _background,
                Define.PaletteColor.DamageOutline => _damageOutline,
                Define.PaletteColor.DamagePreview => _damagePreview,
                Define.PaletteColor.TrailingHealth => _trailingHealth,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown palette role.")
            };
        }

        /// <summary>
        /// UnitStatusView が初期化時に陣営を渡し、HP と役割印に同じ色を適用します。
        /// [KO] UnitStatusView가 초기화 시 팀을 전달해 HP와 역할 배지에 같은 색을 적용합니다.
        /// [EN] UnitStatusView passes its team at initialization to use one color for health and its role badge.
        /// </summary>
        public Color GetTeamColor(Define.BattleTeam team)
        {
            return team switch
            {
                Define.BattleTeam.Ally => _ally,
                Define.BattleTeam.Enemy => _enemy,
                _ => throw new ArgumentOutOfRangeException(nameof(team), team, "Unknown battle team.")
            };
        }
    }
}
