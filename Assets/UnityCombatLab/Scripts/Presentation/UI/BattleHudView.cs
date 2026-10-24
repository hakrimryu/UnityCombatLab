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
    /// 6 人の固定スロット上に状態を表示します。BattleEntryPoint が共通 Canvas に作り、退場時に削除します。
    /// [KO] 6명의 고정 슬롯 위에 상태를 표시합니다. BattleEntryPoint가 공통 Canvas에 만들고 퇴장 시 삭제합니다.
    /// [EN] Displays status above six fixed slots; BattleEntryPoint creates it under the shared canvas and removes it on exit.
    /// </summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        /// <summary>
        /// 交互スロット順の HP 表示。
        /// [KO] 교차 슬롯 순서의 HP 표시.
        /// [EN] Health views in interleaved slot order.
        /// </summary>
        [SerializeField] private UnitStatusView[] _units;

        /// <summary>
        /// 戦闘終了だけを示す短い見出し。
        /// [KO] 전투 종료만 알리는 짧은 제목.
        /// [EN] Short label indicating combat outcome only.
        /// </summary>
        [SerializeField] private TMP_Text _result;

        /// <summary>
        /// 画面内に収める UI 領域。
        /// [KO] 화면 안에 배치할 UI 영역.
        /// [EN] UI region constrained to the safe area.
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
        /// 12 個の被害文字で共有する HUD 所有のコピー。HUD 破棄時に解放します。
        /// [KO] 피해 텍스트 12개가 공유하는 HUD 소유 복사본입니다. HUD 파괴 시 해제합니다.
        /// [EN] HUD-owned copy shared by twelve damage labels and released when the HUD is destroyed.
        /// </summary>
        private Material _damageMaterial;

        /// <summary>
        /// 開始時に借りた Dungeon カメラ。シーンが消えれば配置更新を止めます。
        /// [KO] 시작 시 빌린 Dungeon 카메라입니다. 씬이 제거되면 배치 갱신을 멈춥니다.
        /// [EN] Borrowed Dungeon camera; layout stops when its scene is destroyed.
        /// </summary>
        private Camera _battleCamera;

        /// <summary>
        /// 開始時にコピーしたスロット頭上の位置。動くモデルや再利用イベントは保持しません。
        /// [KO] 시작 시 복사한 슬롯 머리 위 위치입니다. 움직이는 모델이나 재사용 이벤트는 보관하지 않습니다.
        /// [EN] Slot head positions copied at startup, without retaining moving models or reused events.
        /// </summary>
        private Vector3[] _panelPositions;

        /// <summary>
        /// 一度取得して再利用する 6 枚の RectTransform。毎フレームのコンポーネント検索を避けます。
        /// [KO] 한 번 얻어 재사용하는 패널 6개의 RectTransform입니다. 매 프레임 컴포넌트 검색을 피합니다.
        /// [EN] Six panel transforms cached once to avoid per-frame component searches.
        /// </summary>
        private RectTransform[] _panelRects;

        /// <summary>
        /// 最後に適用した画面領域。
        /// [KO] 마지막으로 적용한 화면 영역.
        /// [EN] Last applied screen region.
        /// </summary>
        private Rect _lastSafeArea;

        /// <summary>
        /// 画面の寸法変更も検出します。
        /// [KO] 화면 크기 변경도 검출합니다.
        /// [EN] Detects screen dimension changes as well.
        /// </summary>
        private Vector2Int _lastScreenSize;

        /// <summary>
        /// 表示対象の読み取り専用一覧。
        /// [KO] 표시 대상의 읽기 전용 목록.
        /// [EN] Read-only collection of display targets.
        /// </summary>
        public IReadOnlyList<UnitStatusView> Units => _units;

        /// <summary>
        /// 検証用の実際の表示文。
        /// [KO] 검증용 실제 표시 문구.
        /// [EN] Actual label text for validation.
        /// </summary>
        public string ResultText => _result.text;

        /// <summary>
        /// 参照不足を開始前に拒否します。
        /// [KO] 시작 전에 참조 누락을 거부합니다.
        /// [EN] Rejects missing references before starting.
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
        /// Presenter が選んだ見出しだけを表示します。
        /// [KO] Presenter가 선택한 제목만 표시합니다.
        /// [EN] Displays only the outcome label selected by the presenter.
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

            // 再バインド時は同じコピーを再利用する。アセット自体と他の HUD の材質は変更しない。
            if (_damageMaterial == null)
            {
                _damageMaterial = new Material(_damageMaterialSource);
            }

            _damageMaterial.SetColor(ShaderUtilities.ID_OutlineColor, colors.GetColor(Define.PaletteColor.DamageOutline));
            foreach (var unit in _units)
            {
                unit.ApplyPalette(colors, _damageMaterial);
            }
        }

        /// <summary>
        /// BattleEntryPoint が HUD を削除した時、借用アセットではなく自分で作った材質だけを解放します。
        /// [KO] BattleEntryPoint가 HUD를 삭제하면 빌린 에셋 대신 직접 만든 재질만 해제합니다.
        /// [EN] Releases only the owned material copy when BattleEntryPoint destroys the HUD, preserving borrowed assets.
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
        /// カメラの描画前に固定スロットと安全領域へパネルを合わせます。HP の更新や戦闘計算は行いません。
        /// [KO] 카메라 렌더링 전에 고정 슬롯과 안전 영역에 패널을 맞춥니다. HP 갱신이나 전투 계산은 하지 않습니다.
        /// [EN] Aligns panels to fixed slots and the safe area before camera rendering, without health updates or combat logic.
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

            // Overlay Canvas なので UI カメラ引数は null。射影には Dungeon カメラだけを使う。
            // 6 個の固定配列を再利用する。HP 通知や被害イベントの消費はここでは行わない。
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
