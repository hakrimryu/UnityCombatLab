using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityCombatLab.Presentation.Units;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 6 人の固定スロット上に状態を表示します。BattleEntryPoint が Dungeon 専用 Canvas に作ります。
    /// [KO] 6명의 고정 슬롯 위에 상태를 표시하며 BattleEntryPoint가 Dungeon 전용 Canvas에 생성합니다.
    /// [EN] Displays status above six fixed slots, created by BattleEntryPoint under the Dungeon-owned canvas.
    /// </summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        /// <summary>
        /// 味方・敵を交互に並べた 6 人分の状態パネル。
        /// [KO] 아군·적을 번갈아 배치한 6명의 상태 패널입니다.
        /// [EN] Six unit status panels ordered alternately by ally and enemy.
        /// </summary>
        [SerializeField] private UnitStatusView[] _units;

        /// <summary>
        /// 「戦闘中」「勝利」など、戦闘状況を示す見出し。
        /// [KO] '전투 중'·'승리' 등 전투 상황을 표시할 제목입니다.
        /// [EN] Heading showing battle status, such as ongoing combat or victory.
        /// </summary>
        [SerializeField] private TMP_Text _result;

        /// <summary>
        /// 画面の安全領域に合わせて伸縮する HUD の配置範囲。
        /// [KO] 화면 안전 영역에 맞춰 크기를 조정할 HUD 배치 영역입니다.
        /// [EN] HUD layout region sized to the screen safe area.
        /// </summary>
        [SerializeField] private RectTransform _safeArea;

        /// <summary>
        /// 終了時に広げる見出しの背景。文字の長い敗北理由も一行で収めます。
        /// [KO] 종료 시 넓히는 제목 배경입니다. 긴 패배 이유도 한 줄에 담습니다.
        /// [EN] Header background expanded at battle end to fit longer defeat messages on one line.
        /// </summary>
        [SerializeField] private RectTransform _resultPanel;

        /// <summary>
        /// 状態パネルの底を置く頭上の高さ。Unity ワールド単位で、拡大した隊長にはその倍率を適用します。
        /// [KO] 상태 패널 하단을 놓을 머리 위 높이입니다. Unity 월드 단위이며 확대된 대장에는 그 배율을 적용합니다.
        /// [EN] World-unit height of the panel bottom above a slot, scaled for the larger captain.
        /// </summary>
        [SerializeField] private float _panelHeight = 2.65f;

        /// <summary>
        /// パネル・見出し・アイコンを色の役割に結び付ける設定。状態で変わる色は UnitStatusView が扱います。
        /// [KO] 패널·제목·아이콘을 색상 역할에 연결하는 설정입니다. 상태에 따라 변하는 색은 UnitStatusView가 처리합니다.
        /// [EN] Maps panels, headings and icons to palette roles; UnitStatusView handles state-dependent colors.
        /// </summary>
        [SerializeField] private PaletteGraphicBinding[] _paletteBindings;

        /// <summary>
        /// 被害数字の材質の原型。色を変える前に HUD 専用のコピーを一つだけ作ります。
        /// [KO] 피해 숫자 재질의 원형입니다. 색을 바꾸기 전에 HUD 전용 복사본 하나만 만듭니다.
        /// [EN] Damage material template copied once for this HUD before changing its outline color.
        /// </summary>
        [SerializeField] private Material _damageMaterialSource;

        /// <summary>
        /// プール内の全被害数字で共有する HUD 所有のコピー。HUD 破棄時に解放します。
        /// [KO] 풀의 모든 피해 숫자가 공유하는 HUD 소유 복사본이며 HUD 파괴 시 해제합니다.
        /// [EN] HUD-owned copy shared by all pooled damage labels and released when the HUD is destroyed.
        /// </summary>
        private Material _damageMaterial;

        /// <summary>
        /// 開始時に借りた Dungeon カメラ。シーンが消えれば配置更新を止めます。
        /// [KO] 시작 시 빌린 Dungeon 카메라입니다. 씬이 제거되면 배치 갱신을 멈춥니다.
        /// [EN] Borrowed Dungeon camera; layout stops when its scene is destroyed.
        /// </summary>
        private Camera _battleCamera;

        /// <summary>
        /// BindLayout がスロット座標と高さから計算し、パネルの画面位置へ投影する頭上の座標。
        /// [KO] BindLayout이 슬롯 좌표와 높이로 계산하고 패널의 화면 위치로 투영할 머리 위 좌표입니다.
        /// [EN] Head positions calculated by BindLayout from slot coordinates and height, then projected to panel screen positions.
        /// </summary>
        private Vector3[] _panelPositions;

        /// <summary>
        /// 各スロットの状態パネルの位置を更新する RectTransform。
        /// [KO] 각 슬롯 상태 패널의 위치를 갱신할 RectTransform입니다.
        /// [EN] RectTransforms used to position each slot's status panel.
        /// </summary>
        private RectTransform[] _panelRects;

        /// <summary>
        /// HUD に最後に適用した画面の安全領域。
        /// [KO] HUD에 마지막으로 적용한 화면 안전 영역입니다.
        /// [EN] Screen safe area last applied to the HUD.
        /// </summary>
        private Rect _lastSafeArea;

        /// <summary>
        /// HUD の配置を最後に更新した時の画面サイズ。
        /// [KO] HUD 배치를 마지막으로 갱신했을 때의 화면 크기입니다.
        /// [EN] Screen size used for the last HUD layout update.
        /// </summary>
        private Vector2Int _lastScreenSize;

        /// <summary>
        /// 表示対象の読み取り専用一覧。
        /// [KO] 표시 대상의 읽기 전용 목록.
        /// [EN] Read-only collection of display targets.
        /// </summary>
        public IReadOnlyList<UnitStatusView> Units => _units;

        /// <summary>
        /// HUD に現在表示している戦闘状況の文字列。
        /// [KO] 현재 HUD에 표시 중인 전투 상황 문자열입니다.
        /// [EN] Battle-status text currently displayed by the HUD.
        /// </summary>
        public string ResultText => _result.text;

        /// <summary>
        /// 必要な UI 参照と状態パネル数を確認します。
        /// [KO] 필수 UI 참조와 상태 패널 수를 확인합니다.
        /// [EN] Checks required UI references and the number of status panels.
        /// </summary>
        public void Validate(int unitCount)
        {
            if (_units == null || _units.Length != unitCount || _units.Any(unit => unit == null) ||
                _result == null || _safeArea == null || _resultPanel == null || _damageMaterialSource == null ||
                _paletteBindings == null || _paletteBindings.Length == 0 ||
                _paletteBindings.Any(binding => binding == null))
            {
                throw new InvalidOperationException("Battle HUD references must match the battle slots.");
            }
        }

        /// <summary>
        /// 戦闘状況の文字列を設定し、終了時は見出しの背景を広げます。
        /// [KO] 전투 상황 문자열을 설정하고 종료 시 제목 배경을 넓힙니다.
        /// [EN] Sets battle-status text and expands the heading background when finished.
        /// </summary>
        public void SetResult(string text, bool isFinished)
        {
            _result.text = text;
            _result.fontSize = isFinished ? 38 : 28;
            _resultPanel.sizeDelta = new Vector2(isFinished ? 660 : 240, isFinished ? 76 : 56);
        }

        /// <summary>
        /// Presenter が購読を始める前に呼び、共通設定を固定表示・各ユニット・被害材質へ適用します。
        /// [KO] Presenter가 구독을 시작하기 전에 호출해 공통 설정을 고정 표시·유닛·피해 재질에 적용합니다.
        /// [EN] Called before presenter subscriptions to apply shared colors to static graphics, units and the damage material.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
        {
            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            foreach (var binding in _paletteBindings)
            {
                binding.Apply(colors);
            }

            // この HUD 用に一つ作った数字材質を、再接続時も共有する。
            if (_damageMaterial == null)
            {
                _damageMaterial = new Material(_damageMaterialSource);
            }

            _damageMaterial.SetColor(ShaderUtilities.ID_OutlineColor,
                colors.GetColor(Define.PaletteColor.DamageOutline));
            foreach (var unit in _units)
            {
                unit.ApplyPalette(colors, _damageMaterial);
            }
        }

        /// <summary>
        /// HUD が作成した共通数字材質を破棄します。
        /// [KO] HUD가 만든 공통 숫자 재질을 파괴합니다.
        /// [EN] Destroys the shared number material created by this HUD.
        /// </summary>
        private void OnDestroy()
        {
            if (_damageMaterial != null)
            {
                Destroy(_damageMaterial);
            }
        }

        /// <summary>
        /// BattleEntryPoint が開始時に呼び、同じ配列順のキャラクターと状態パネルの位置を接続します。
        /// [KO] BattleEntryPoint가 시작 시 호출하여 같은 배열 순서의 캐릭터와 상태 패널 위치를 연결합니다.
        /// [EN] Called by BattleEntryPoint at startup to connect characters and status panels in matching array order.
        /// </summary>
        public void BindLayout(Camera battleCamera, UnitView[] unitViews)
        {
            if (battleCamera == null || unitViews == null || unitViews.Any(unit => unit == null))
            {
                throw new ArgumentException("HUD layout requires the battle camera and unit slots.");
            }

            Validate(unitViews.Length);
            _battleCamera = battleCamera;
            _panelPositions = unitViews.Select(unit => unit.transform.position +
                                                       Vector3.up * (_panelHeight * unit.transform.lossyScale.y))
                .ToArray();
            _panelRects = _units.Select(unit => (RectTransform)unit.transform).ToArray();
            UpdateLayout(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        /// <summary>
        /// カメラの描画前に、画面サイズと安全領域に合わせて各スロットのパネル位置を更新します。
        /// [KO] 카메라 렌더링 전에 화면 크기와 안전 영역에 맞춰 각 슬롯의 패널 위치를 갱신합니다.
        /// [EN] Updates each slot panel position for the screen size and safe area before camera rendering.
        /// </summary>
        private void LateUpdate()
        {
            UpdateLayout(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        /// <summary>
        /// 画面ピクセルの安全領域を適用し、各固定スロットを Overlay Canvas の座標へ変換します。
        /// [KO] 화면 픽셀의 안전 영역을 적용하고 각 고정 슬롯을 Overlay Canvas 좌표로 변환합니다.
        /// [EN] Applies the safe rectangle in screen pixels and projects each fixed slot into overlay-canvas coordinates.
        /// </summary>
        public void UpdateLayout(Rect area, Vector2Int size)
        {
            if (_safeArea == null || size.x <= 0 || size.y <= 0)
            {
                return;
            }

            if (area != _lastSafeArea || size != _lastScreenSize)
            {
                _lastSafeArea = area;
                _lastScreenSize = size;
                // 画面幅 1000 の左端 50px は anchor x=0.05。全画面が安全なら (0,0)〜(1,1)。
                _safeArea.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
                _safeArea.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
                // 解像度変更直後は CanvasScaler の新しい寸法を確定してから射影する。
                // 古い Canvas 寸法を使うと、最初の 1 フレームだけパネルが中央で重なる。
                Canvas.ForceUpdateCanvases();
            }

            if (_battleCamera == null || _panelPositions == null)
            {
                return;
            }

            // Dungeon カメラで求めた画面位置を、Overlay Canvas の座標へ変換する。
            for (var index = 0; index < _panelPositions.Length; index++)
            {
                var screenPoint = _battleCamera.WorldToScreenPoint(_panelPositions[index]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeArea, screenPoint, null, out var point);
                var panel = _panelRects[index];
                var halfWidth = panel.rect.width * 0.5f;
                // pivot=(0.5,0) の底を頭上に置く。上 140 は見出し用、端の 16 はパネルと安全境界の余白。
                point.x = Mathf.Clamp(point.x, _safeArea.rect.xMin + halfWidth + 16,
                    _safeArea.rect.xMax - halfWidth - 16);
                point.y = Mathf.Clamp(point.y, _safeArea.rect.yMin + 16,
                    _safeArea.rect.yMax - panel.rect.height - 140);
                panel.localPosition = new Vector3(point.x, point.y, 0);
            }
        }
    }
}
