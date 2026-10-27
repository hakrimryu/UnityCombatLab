using System;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 提示された一枚の強化を表示し、共通ボタンから選択を通知します。
    /// [KO] 제시한 강화 카드 한 장을 표시하고 공통 버튼을 통해 선택을 알립니다.
    /// [EN] Displays one offered upgrade and reports selection through the common button.
    /// </summary>
    public sealed class RewardCardView : CommonButtonViewBase
    {
        /// <summary>
        /// 強化の日本語名を表示するラベル。
        /// [KO] 강화 일본어 이름을 표시할 라벨입니다.
        /// [EN] Label displaying the Japanese upgrade name.
        /// </summary>
        [SerializeField] private TMP_Text _title;

        /// <summary>
        /// 選択したときに得る効果を説明するラベル。
        /// [KO] 선택하면 얻을 효과를 설명하는 라벨입니다.
        /// [EN] Label explaining the effect granted by selection.
        /// </summary>
        [SerializeField] private TMP_Text _details;

        /// <summary>
        /// 効果説明の長さによらず、累積率と重複数を固定位置に表示します。
        /// [KO] 효과 설명 길이와 무관하게 누적 비율·중첩 수를 고정 위치에 표시합니다.
        /// [EN] Displays cumulative percentage and stacks at a fixed position regardless of description length.
        /// </summary>
        [SerializeField] private TMP_Text _totals;

        /// <summary>
        /// 今回の増加率を数字専用フォントで表示します。
        /// [KO] 이번 증가율을 숫자 전용 폰트로 표시합니다.
        /// [EN] Displays this offer's increase using the numeric font.
        /// </summary>
        [SerializeField] private TMP_Text _value;

        /// <summary>
        /// Bind が候補の種類に合わせて更新するアイコン。
        /// [KO] Bind가 후보 종류에 맞춰 갱신하는 아이콘입니다.
        /// [EN] Icon updated by Bind to match the offered reward kind.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _icon;

        /// <summary>
        /// 候補の種類に対応する色を表示する見出し背景。
        /// [KO] 후보 종류에 대응하는 색상을 표시하는 머리글 배경입니다.
        /// [EN] Heading background displaying the color assigned to the offered kind.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _accent;

        /// <summary>
        /// 見出しとアイコン枠の装飾。素材の明暗を保ち、候補の色相だけを揃えます。
        /// [KO] 머리글·아이콘 틀 장식이며 소재의 명암을 유지하고 후보의 색조를 맞춥니다.
        /// [EN] Heading and icon-frame decorations whose hue follows the offer while preserving authored shading.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image[] _accentParts;

        /// <summary>
        /// RewardKind 順で、候補のアイコンへ割り当てる六種類の画像。
        /// [KO] RewardKind 순서로 후보 아이콘에 할당하는 여섯 종류 이미지입니다.
        /// [EN] Six images in RewardKind order assigned to the offered icon.
        /// </summary>
        [SerializeField] private Sprite[] _rewardIcons;

        /// <summary>
        /// RewardKind 順で、見出しと装飾の色相を選ぶ六種類の色。
        /// [KO] RewardKind 순서로 머리글·장식의 색조를 선택하는 여섯 종류 색상입니다.
        /// [EN] Six colors in RewardKind order selecting the heading and decoration hue.
        /// </summary>
        [SerializeField] private Color[] _rewardColors;

        /// <summary>
        /// 選択後の重複数を三つの宝石で示します。
        /// [KO] 선택 후 중첩 수를 세 보석으로 표시합니다.
        /// [EN] Three gems showing the stack count after selection.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image[] _stackGems;

        /// <summary>
        /// このカードを提示したクリア済みウェーブ。
        /// [KO] 이 카드를 제시한 클리어 웨이브입니다.
        /// [EN] Cleared wave that offered this card.
        /// </summary>
        private int _wave;

        /// <summary>
        /// 現在表示している強化種類。
        /// [KO] 현재 표시하는 강화 종류입니다.
        /// [EN] Currently displayed upgrade kind.
        /// </summary>
        private Define.RewardKind _kind;

        /// <summary>
        /// 画面が渡し、破棄時に解除する選択通知。
        /// [KO] 화면이 전달하고 파괴 시 해제할 선택 알림입니다.
        /// [EN] Selection callback provided by the screen and released on destruction.
        /// </summary>
        private Action<int, Define.RewardKind> _selected;

        /// <summary>
        /// 購入フレームを利用したカードの明るい本文面。
        /// [KO] 구매 프레임을 이용한 카드의 밝은 본문 면입니다.
        /// [EN] Light card body using the purchased frame artwork.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _background;

        /// <summary>
        /// 本文へ共通文字色を適用し、フレームと見出しの配色は素材に保持します。
        /// [KO] 본문에 공통 글자색을 적용하고 프레임·머리글 배색은 소재에 유지합니다.
        /// [EN] Applies shared body-text colors while retaining authored frame and heading colors.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
        {
            if (colors == null || _background == null || _title == null || _details == null || _totals == null)
                throw new InvalidOperationException("Reward card palette requires its labels and background.");

            _title.color = _details.color = _totals.color = colors.GetColor(Define.PaletteColor.Text);
        }

        /// <summary>
        /// 候補の固定値を表示し、クリック時に同じウェーブと種類を返します。
        /// [KO] 후보 고정값을 표시하고 클릭하면 같은 웨이브·종류를 반환합니다.
        /// [EN] Displays fixed offer values and returns that wave and kind on click.
        /// </summary>
        public void Bind(RewardCardDisplay display, Action<int, Define.RewardKind> selected)
        {
            if (_title == null || _details == null || _totals == null || _value == null || _icon == null || _accent == null ||
                _rewardIcons == null || _rewardIcons.Length != 6 || _rewardColors == null || _rewardColors.Length != 6 ||
                _accentParts == null || _accentParts.Length == 0 || _stackGems == null || _stackGems.Length != 3 ||
                display == null || selected == null)
            {
                throw new InvalidOperationException("A reward card requires its labels, data and selection callback.");
            }

            InitializeButton();
            _wave = display.Wave;
            _kind = display.Kind;
            _selected = selected;
            _title.text = display.Title;
            var kindIndex = (int)display.Kind;
            if (_rewardIcons[kindIndex] == null)
                throw new InvalidOperationException("A reward kind requires its authored icon.");

            _icon.sprite = _rewardIcons[kindIndex];
            _accent.color = _rewardColors[kindIndex];
            Color.RGBToHSV(_accent.color, out var hue, out _, out _);
            foreach (var part in _accentParts)
            {
                if (part == null)
                    throw new InvalidOperationException("A reward card accent is missing.");

                Color.RGBToHSV(part.color, out _, out var saturation, out var brightness);
                part.color = Color.HSVToRGB(hue, saturation, brightness);
            }

            _value.text = display.Value;
            _details.text = display.Details;
            _totals.text = display.Totals;
            for (var index = 0; index < _stackGems.Length; index++)
            {
                if (_stackGems[index] == null)
                    throw new InvalidOperationException("A reward card stack gem is missing.");

                _stackGems[index].gameObject.SetActive(index < display.StacksAfter);
            }
        }

        /// <summary>
        /// 共通 listener から今回の候補だけを通知します。
        /// [KO] 공통 listener에서 이번 후보만 알립니다.
        /// [EN] Reports only the currently bound offer from the common listener.
        /// </summary>
        protected override void HandleClicked()
            => _selected?.Invoke(_wave, _kind);

        /// <summary>
        /// 共通 listener を解除してから画面への通知を解放します。
        /// [KO] 공통 listener를 해제한 뒤 화면 알림을 해제합니다.
        /// [EN] Releases the screen callback after detaching the common listener.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            _selected = null;
        }
    }
}
