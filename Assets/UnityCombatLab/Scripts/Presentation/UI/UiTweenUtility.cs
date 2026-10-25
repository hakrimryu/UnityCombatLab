using DG.Tweening;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// UI の登場と回転を作ります。返した Tween は呼び出した View が停止します。
    /// [KO] UI 등장과 회전을 만들며 반환한 Tween은 호출한 View가 정지합니다.
    /// [EN] Creates UI entrance and rotation tweens, stopped by the calling view.
    /// </summary>
    public static class UiTweenUtility
    {
        /// <summary>
        /// 内容を小さく透明にしてから、少し弾ませて等倍へ戻します。
        /// [KO] 내용을 작고 투명하게 시작해 살짝 튀어 오르며 원래 크기로 표시합니다.
        /// [EN] Reveals content from a small, transparent state with a slight scale overshoot.
        /// </summary>
        public static Sequence PopIn(RectTransform content, CanvasGroup group, float seconds)
        {
            content.localScale = Vector3.one * 0.65f;
            group.alpha = 0f;
            // View が停止まで参照するため、完了後も別の Tween として再利用させない。
            return DOTween.Sequence()
                .Append(content.DOScale(Vector3.one, seconds).SetEase(Ease.OutBack, 1.7f))
                .Join(DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f,
                    Mathf.Min(seconds, 0.18f)).SetEase(Ease.OutQuad))
                .SetUpdate(true)
                .SetRecyclable(false);
        }

        /// <summary>
        /// UI の光などを、指定秒数で一周する一定速度で回し続けます。
        /// [KO] UI 광원 등을 지정한 초마다 한 바퀴 도는 일정한 속도로 계속 회전합니다.
        /// [EN] Rotates a UI decoration continuously at one revolution per supplied duration.
        /// </summary>
        public static Tween RotateContinuously(RectTransform target, float seconds)
        {
            return target.DOLocalRotate(new Vector3(0, 0, 360), seconds, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true)
                .SetRecyclable(false);
        }
    }
}
