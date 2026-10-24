using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 6 人分のカードと結果の文字を持つ HUD。BattleEntryPoint が共通 Canvas に作り、退場時に削除します。
    /// [KO] 6명의 카드와 결과 문구를 가진 HUD입니다. BattleEntryPoint가 공통 Canvas에 만들고 씬을 떠날 때 삭제합니다.
    /// [EN] HUD with six unit cards and an outcome label, created under the shared canvas and removed by BattleEntryPoint.
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
                _result == null || _safeArea == null)
            {
                throw new InvalidOperationException("Battle HUD references must match the battle slots.");
            }
        }

        /// <summary>
        /// Presenter が選んだ見出しだけを表示します。
        /// [KO] Presenter가 선택한 제목만 표시합니다.
        /// [EN] Displays only the outcome label selected by the presenter.
        /// </summary>
        public void SetResult(string text)
        {
            _result.text = text;
        }

        /// <summary>
        /// 画面サイズやノッチを避ける領域が変わった時だけ、HUD を収める範囲を更新します。HP はここで更新しません。
        /// [KO] 화면 크기나 노치를 피할 영역이 바뀔 때만 HUD 배치 범위를 갱신합니다. HP는 여기서 갱신하지 않습니다.
        /// [EN] Updates HUD bounds only when the screen or safe area changes; does not update health here.
        /// </summary>
        private void LateUpdate()
        {
            var area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (_safeArea == null || size.x <= 0 || size.y <= 0 ||
                (area == _lastSafeArea && size == _lastScreenSize))
            {
                return;
            }

            _lastSafeArea = area;
            _lastScreenSize = size;
            // ピクセル座標を Canvas の 0〜1 の割合へ直す。画面幅 1000 の左端 50px は anchor x=0.05。
            // 全画面が安全なら anchorMin=(0,0)、anchorMax=(1,1) となる。
            _safeArea.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            _safeArea.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
        }
    }
}
