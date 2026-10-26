using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// Button の入力を受け、固定されたクリック領域の内側で表示部品を拡縮します。
    /// [KO] Button 입력을 받아 고정된 클릭 영역 안에서 표시 부품을 확대·축소합니다.
    /// [EN] Scales visual parts in response to Button input while keeping its hit area fixed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ButtonInteractionFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>
        /// 一緒に動かす直下の表示部品。固定クリック領域は含めません。
        /// [KO] 함께 움직일 바로 아래 표시 부품이며 고정 클릭 영역은 포함하지 않습니다.
        /// [EN] Direct visual children animated together, excluding the fixed hit area.
        /// </summary>
        [SerializeField] private RectTransform[] _visuals;

        /// <summary>
        /// カーソルを重ねたときの元サイズに対する倍率。
        /// [KO] 커서를 올렸을 때 원래 크기에 대한 배율입니다.
        /// [EN] Scale relative to the original size while hovered.
        /// </summary>
        [SerializeField, Range(1f, 1.1f)] private float _hoverScale = 1.04f;

        /// <summary>
        /// 押している間の元サイズに対する倍率。
        /// [KO] 누르는 동안 원래 크기에 대한 배율입니다.
        /// [EN] Scale relative to the original size while pressed.
        /// </summary>
        [SerializeField, Range(0.9f, 1f)] private float _pressedScale = 0.96f;

        /// <summary>
        /// ホバーや押下のサイズに移る秒数。
        /// [KO] 호버 또는 누름 크기로 전환하는 초 단위 시간입니다.
        /// [EN] Duration in seconds for entering hover or press size.
        /// </summary>
        [SerializeField, Range(0.02f, 0.2f)] private float _responseSeconds = 0.075f;

        /// <summary>
        /// 離したサイズに戻って落ち着くまでの秒数。
        /// [KO] 해제한 크기로 돌아와 안정될 때까지의 초 단위 시간입니다.
        /// [EN] Duration in seconds for returning and settling after release.
        /// </summary>
        [SerializeField, Range(0.05f, 0.4f)] private float _returnSeconds = 0.18f;

        /// <summary>
        /// 復帰の OutBack が一度だけ目標を越える強さ。0 は越えません。
        /// [KO] 복귀 OutBack이 목표를 한 번 넘는 강도이며 0이면 넘지 않습니다.
        /// [EN] Strength of the single OutBack overshoot on return; zero disables overshoot.
        /// </summary>
        [SerializeField, Range(0f, 1.7f)] private float _overshoot = 1.1f;

        /// <summary>
        /// 操作可否を確認する既存 Button と、表示全体の中心を持つ RectTransform。
        /// [KO] 조작 가능 여부를 확인할 기존 Button과 전체 표시 중심을 가진 RectTransform입니다.
        /// [EN] Existing Button used to check interaction and RectTransform defining the visual center.
        /// </summary>
        private UnityEngine.UI.Button _button;
        private RectTransform _root;

        /// <summary>
        /// 初期の位置とサイズ。毎回この値から計算し、繰り返し操作による累積を防ぎます。
        /// [KO] 초기 위치와 크기이며 매번 이 값에서 계산해 반복 조작에 따른 누적을 막습니다.
        /// [EN] Initial positions and scales used for every calculation to prevent accumulation.
        /// </summary>
        private Vector3[] _homePositions;
        private Vector3[] _homeScales;

        /// <summary>
        /// この部品だけが止める Tween と、その Tween が更新する共通倍率。
        /// [KO] 이 컴포넌트만 정지할 Tween과 해당 Tween이 갱신하는 공통 배율입니다.
        /// [EN] Tween stopped only by this component and the common scale factor it updates.
        /// </summary>
        private Tween _animation;
        private float _scale = 1f;

        /// <summary>
        /// ホバーと押下を始めたポインターを別々に覚え、別の指の解放を無視します。
        /// [KO] 호버와 누름을 시작한 포인터를 따로 기억해 다른 손가락의 해제를 무시합니다.
        /// [EN] Tracks hovering and pressing pointers separately to ignore another finger's release.
        /// </summary>
        private int? _hoverPointerId;
        private int? _pressedPointerId;
        private bool _isPressedInside;

        /// <summary>
        /// Button 自身と親 CanvasGroup の両方が操作を許可しているかを返します。
        /// [KO] Button 자체와 부모 CanvasGroup 양쪽이 조작을 허용하는지 반환합니다.
        /// [EN] Returns whether both the Button and its parent CanvasGroups allow interaction.
        /// </summary>
        private bool CanInteract
            => _button != null && _button.isActiveAndEnabled && _button.IsInteractable();

        /// <summary>
        /// prefab の表示参照を確認し、非等倍も含む元の位置とサイズを一度記録します。
        /// [KO] prefab 표시 참조를 확인하고 비등배 크기도 포함한 원래 위치와 크기를 한 번 기록합니다.
        /// [EN] Checks prefab visual references and records original positions and non-unit scales once.
        /// </summary>
        private void Awake()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
            _root = (RectTransform)transform;
            if (_visuals == null || _visuals.Length == 0)
                throw new InvalidOperationException("Button feedback requires direct visual children.");

            var homePositions = new Vector3[_visuals.Length];
            var homeScales = new Vector3[_visuals.Length];
            for (var index = 0; index < _visuals.Length; index++)
            {
                var visual = _visuals[index];
                if (visual == null || visual.parent != _root)
                    throw new InvalidOperationException("Button feedback visuals must be direct children of the Button.");

                homePositions[index] = visual.anchoredPosition3D;
                homeScales[index] = visual.localScale;
            }

            _homePositions = homePositions;
            _homeScales = homeScales;
        }

        /// <summary>
        /// タッチの進入はホバーにせず、押下中の再進入だけを押した表示に戻します。
        /// [KO] 터치 진입은 호버로 처리하지 않고 누른 채 재진입한 경우만 누름 표시로 되돌립니다.
        /// [EN] Excludes touch entry from hover and restores press visuals only on reentry while held.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanInteract)
                return;

            if (!IsTouch(eventData))
                _hoverPointerId = eventData.pointerId;

            if (_pressedPointerId == eventData.pointerId)
                _isPressedInside = true;

            if (_hoverPointerId.HasValue || _isPressedInside)
                AnimateScale(false);
        }

        /// <summary>
        /// 対象ポインターが外へ出たら押下表示を解き、再進入まで元のサイズへ戻します。
        /// [KO] 해당 포인터가 밖으로 나가면 누름 표시를 해제하고 재진입 전까지 원래 크기로 되돌립니다.
        /// [EN] Releases press visuals when the tracked pointer exits, returning to original size until reentry.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            var hasChanged = false;
            if (_hoverPointerId == eventData.pointerId)
            {
                _hoverPointerId = null;
                hasChanged = true;
            }

            if (_pressedPointerId == eventData.pointerId && _isPressedInside)
            {
                _isPressedInside = false;
                hasChanged = true;
            }

            if (hasChanged)
                AnimateScale(true);
        }

        /// <summary>
        /// 操作可能な左ボタンまたは最初の指の押下を表示へ反映します。
        /// [KO] 조작 가능한 왼쪽 버튼 또는 첫 손가락의 누름을 표시에 반영합니다.
        /// [EN] Displays a press from the left mouse button or first tracked finger when interactable.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !CanInteract || _pressedPointerId.HasValue)
                return;

            _pressedPointerId = eventData.pointerId;
            _isPressedInside = true;
            AnimateScale(false);
        }

        /// <summary>
        /// 押し始めたポインターの解放だけで、ホバーまたは元のサイズへ軽く弾ませて戻します。
        /// [KO] 누름을 시작한 포인터의 해제에만 호버 또는 원래 크기로 가볍게 튀며 복귀합니다.
        /// [EN] Returns with a light overshoot to hover or original size only for the pointer that began pressing.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _pressedPointerId != eventData.pointerId)
                return;

            _pressedPointerId = null;
            _isPressedInside = false;
            AnimateScale(true);
        }

        /// <summary>
        /// Button.enabled・interactable・親 CanvasGroup の変更を描画前に反映します。
        /// [KO] Button.enabled·interactable·부모 CanvasGroup 변경을 그리기 전에 반영합니다.
        /// [EN] Reflects changes to Button.enabled, interactable and parent CanvasGroups before rendering.
        /// </summary>
        private void LateUpdate()
        {
            // Button は操作可否の変更イベントを公開しないため、同じフレームの描画前に解除する。
            if (!CanInteract && (_animation != null || _hoverPointerId.HasValue || _pressedPointerId.HasValue))
                ResetFeedback();
        }

        /// <summary>
        /// 進行中の自分の Tween を置き換え、現在の倍率から最新の入力状態へ移します。
        /// [KO] 진행 중인 자신의 Tween을 교체하고 현재 배율에서 최신 입력 상태로 전환합니다.
        /// [EN] Replaces its own active tween and transitions from the current scale to the latest input state.
        /// </summary>
        private void AnimateScale(bool isReturning)
        {
            if (!CanInteract)
            {
                ResetFeedback();
                return;
            }

            _animation?.Kill(false);
            var isPressed = _pressedPointerId.HasValue && _isPressedInside;
            var target = isPressed ? _pressedScale : _hoverPointerId.HasValue ? _hoverScale : 1f;
            _animation = DOTween.To(() => _scale, ApplyScale, target,
                    isReturning && !isPressed ? _returnSeconds : _responseSeconds)
                .SetEase(isReturning && !isPressed ? Ease.OutBack : Ease.OutQuad, _overshoot)
                .SetUpdate(true)
                .SetRecyclable(false);
        }

        /// <summary>
        /// 共通倍率を元の位置とサイズへ適用し、別々の子部品を一つの絵として拡縮します。
        /// [KO] 공통 배율을 원래 위치와 크기에 적용해 별도 자식 부품을 하나의 그림처럼 확대·축소합니다.
        /// [EN] Applies a common factor to original positions and scales so separate children scale as one visual.
        /// </summary>
        private void ApplyScale(float scale)
        {
            _scale = scale;
            if (_homePositions == null || _root == null)
                return;

            for (var index = 0; index < _visuals.Length; index++)
            {
                var visual = _visuals[index];
                if (visual == null)
                    continue;

                var home = _homePositions[index];
                // 購入 prefab の子を移し替えず、ボタンの中心を基準に位置も拡縮する。
                // 左端からの anchor と pivot を中心からの距離へ直すので、親のサイズ変更にも追従する。
                // 例: 中心から x=-270 のアイコンは 1.04 倍で x=-280.8 となる。
                var anchor = visual.anchorMin + Vector2.Scale(visual.anchorMax - visual.anchorMin, visual.pivot);
                var fromCenter = (Vector2)home + Vector2.Scale(_root.rect.size, anchor - Vector2.one * 0.5f);
                visual.anchoredPosition3D = home + (Vector3)(fromCenter * (scale - 1f));
                var homeScale = _homeScales[index];
                visual.localScale = new Vector3(homeScale.x * scale, homeScale.y * scale, homeScale.z);
            }
        }

        /// <summary>
        /// 新 Input System の種別、または旧入力の指 ID からタッチかを判定します。
        /// [KO] 새 Input System 종류 또는 이전 입력의 손가락 ID로 터치인지 판별합니다.
        /// [EN] Identifies touch from the new Input System type or legacy finger ID.
        /// </summary>
        private static bool IsTouch(PointerEventData eventData)
            => eventData is ExtendedPointerEventData extended
                ? extended.pointerType == UIPointerType.Touch
                : eventData.pointerId >= 0;

        /// <summary>
        /// 完了通知を呼ばずに自分の Tween だけを止め、入力状態と元の姿勢を復元します。
        /// [KO] 완료 알림 없이 자신의 Tween만 멈추고 입력 상태와 원래 자세를 복원합니다.
        /// [EN] Stops only its own tween without completing it and restores input state and original transforms.
        /// </summary>
        private void ResetFeedback()
        {
            _animation?.Kill(false);
            _animation = null;
            _hoverPointerId = null;
            _pressedPointerId = null;
            _isPressedInside = false;
            ApplyScale(1f);
        }

        /// <summary>
        /// 画面を隠したときに表示と Tween を戻します。
        /// [KO] 화면을 숨길 때 표시와 Tween을 되돌립니다.
        /// [EN] Restores visuals and stops the tween when the screen is hidden.
        /// </summary>
        private void OnDisable()
            => ResetFeedback();

        /// <summary>
        /// 破棄時にも自分の Tween 参照を解放します。
        /// [KO] 파괴 시에도 자신의 Tween 참조를 해제합니다.
        /// [EN] Releases its tween reference on destruction as well.
        /// </summary>
        private void OnDestroy()
            => ResetFeedback();

        /// <summary>
        /// フォーカス喪失で解放入力が届かなくても押下表示を残しません。
        /// [KO] 포커스를 잃어 해제 입력이 오지 않아도 누름 표시를 남기지 않습니다.
        /// [EN] Clears press visuals if focus loss prevents release input from arriving.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ResetFeedback();
        }

        /// <summary>
        /// Android がバックグラウンドへ移るときに押下と Tween を解除します。
        /// [KO] Android가 백그라운드로 이동할 때 누름과 Tween을 해제합니다.
        /// [EN] Clears press state and the tween when Android enters the background.
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
                ResetFeedback();
        }
    }
}
