using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// FrontendとHUDの操作領域を画面の安全領域に合わせ、画面サイズが変わった時だけ配置を更新します。
    /// [KO] Frontend와 HUD의 조작 영역을 화면 안전 영역에 맞추고 화면 크기가 바뀔 때만 배치를 갱신합니다.
    /// [EN] Fits Frontend and HUD controls to the screen safe area, updating layout only when screen geometry changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>
        /// このコンポーネントを付けた操作領域。
        /// [KO] 이 컴포넌트가 붙은 조작 영역입니다.
        /// [EN] Control region carrying this component.
        /// </summary>
        private RectTransform _rect;

        /// <summary>
        /// 最後に適用した安全領域。ピクセル単位で比較します。
        /// [KO] 마지막 적용 안전 영역이며 픽셀 단위로 비교합니다.
        /// [EN] Last applied safe rectangle, compared in screen pixels.
        /// </summary>
        private Rect _lastArea;

        /// <summary>
        /// 最後に適用した画面幅と高さ。
        /// [KO] 마지막 적용 화면 너비와 높이입니다.
        /// [EN] Last applied screen width and height.
        /// </summary>
        private Vector2Int _lastSize;

        /// <summary>
        /// ゲーム画面の向き・サイズ・ノッチ領域の変化を反映します。
        /// [KO] 게임 화면의 방향·크기·노치 영역 변화를 반영합니다.
        /// [EN] Applies changes to game-screen orientation, dimensions and cutout insets.
        /// </summary>
        private void LateUpdate()
            => Apply(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));

        /// <summary>
        /// 画面ピクセルを全画面に対するアンカー比率へ変換し、変更した場合だけtrueを返します。
        /// [KO] 화면 픽셀을 전체 화면 대비 앵커 비율로 변환하고 변경했을 때만 true를 반환합니다.
        /// [EN] Converts screen pixels to full-screen anchor ratios and returns true only when layout changes.
        /// </summary>
        public bool Apply(Rect area, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0 || (area == _lastArea && size == _lastSize))
                return false;

            if (_rect == null)
                _rect = (RectTransform)transform;

            _lastArea = area;
            _lastSize = size;
            // 画面幅1000の左端50pxはanchor x=0.05。全画面が安全なら(0,0)〜(1,1)。
            _rect.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            _rect.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            // HUDの射影にはCanvasScaler反映後の寸法が必要なので、変更したフレームで確定する。
            Canvas.ForceUpdateCanvases();

            return true;
        }
    }
}
