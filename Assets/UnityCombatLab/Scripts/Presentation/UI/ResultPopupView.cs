using System;
using DG.Tweening;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 勝敗パネルの登場・装飾を再生し、非表示時に Tween と粒子を停止します。
    /// [KO] 승패 패널의 등장·장식을 재생하고 비활성화 시 Tween과 입자를 정지합니다.
    /// [EN] Animates result panel entrances and decorations, stopping tweens and particles when hidden.
    /// </summary>
    public sealed class ResultPopupView : MonoBehaviour
    {
        /// <summary>
        /// 拡大して表示するイラスト・見出しの親。
        /// [KO] 확대하며 표시할 일러스트·제목의 부모입니다.
        /// [EN] Parent of the illustration and heading scaled during entrance.
        /// </summary>
        [SerializeField] private RectTransform _content;

        /// <summary>
        /// 背景を含むパネル全体の透明度。
        /// [KO] 배경을 포함한 패널 전체의 투명도입니다.
        /// [EN] Opacity of the entire panel, including its background.
        /// </summary>
        [SerializeField] private CanvasGroup _group;

        /// <summary>
        /// 勝利イラストの背後で回す光。装飾のないパネルでは未指定です。
        /// [KO] 승리 일러스트 뒤에서 회전할 광원이며 장식이 없는 패널은 지정하지 않습니다.
        /// [EN] Light rotated behind the victory illustration; unset on panels without this decoration.
        /// </summary>
        [SerializeField] private RectTransform _rotatingLight;

        /// <summary>
        /// 勝利イラストの中央から外側へ放つ紙吹雪。
        /// [KO] 승리 일러스트 중앙에서 바깥으로 방출할 색종이입니다.
        /// [EN] Confetti emitted outward from the center of the victory illustration.
        /// </summary>
        [SerializeField] private UiConfettiView _confetti;

        /// <summary>
        /// パネルが弾んで等倍に落ち着くまでの秒数。
        /// [KO] 패널이 튀어 오른 뒤 원래 크기로 안정될 때까지의 초입니다.
        /// [EN] Seconds for the panel to pop and settle at its normal scale.
        /// </summary>
        [SerializeField, Min(0.01f)] private float _popSeconds = 0.45f;

        /// <summary>
        /// 光が一周する秒数。
        /// [KO] 광원이 한 바퀴 회전하는 초입니다.
        /// [EN] Seconds per complete light rotation.
        /// </summary>
        [SerializeField, Min(0.01f)] private float _rotationSeconds = 16f;

        /// <summary>
        /// このパネルが停止する登場 Tween。
        /// [KO] 이 패널이 정지할 등장 Tween입니다.
        /// [EN] Entrance tween stopped by this panel.
        /// </summary>
        private Sequence _entrance;

        /// <summary>
        /// パネルを閉じるまで続ける光の回転。
        /// [KO] 패널을 닫을 때까지 계속할 광원 회전입니다.
        /// [EN] Light rotation continued until the panel is closed.
        /// </summary>
        private Tween _rotation;

        /// <summary>
        /// 非表示時に戻す光の向き。
        /// [KO] 비활성화 시 복원할 광원 방향입니다.
        /// [EN] Light orientation restored when hidden.
        /// </summary>
        private Quaternion _lightHomeRotation = Quaternion.identity;

        /// <summary>
        /// 必須参照と再生時間を確認し、光の初期方向を保存します。
        /// [KO] 필수 참조와 재생 시간을 확인하고 광원의 초기 방향을 저장합니다.
        /// [EN] Checks required references and durations, and saves the light's initial orientation.
        /// </summary>
        private void Awake()
        {
            if (_content == null || _group == null || _popSeconds <= 0f || _rotationSeconds <= 0f)
                throw new InvalidOperationException("A result popup requires content, opacity and positive durations.");
            if (_rotatingLight != null) _lightHomeRotation = _rotatingLight.localRotation;
        }

        /// <summary>
        /// パネルの表示に合わせ、登場・回転・紙吹雪を最初から再生します。
        /// [KO] 패널 표시에 맞춰 등장·회전·색종이를 처음부터 재생합니다.
        /// [EN] Restarts the entrance, rotation and confetti when the panel becomes visible.
        /// </summary>
        private void OnEnable()
        {
            Clear();
            _entrance = UiTweenUtility.PopIn(_content, _group, _popSeconds);
            if (_rotatingLight != null)
                _rotation = UiTweenUtility.RotateContinuously(_rotatingLight, _rotationSeconds);
            if (_confetti != null) _confetti.Play();
        }

        /// <summary>
        /// 完了処理を呼ばずに Tween を止め、次の表示に備えて姿勢と粒子を戻します。
        /// [KO] 완료 처리를 호출하지 않고 Tween을 정지한 뒤 다음 표시를 위해 자세와 입자를 초기화합니다.
        /// [EN] Stops tweens without completion and resets transforms and particles for the next display.
        /// </summary>
        private void Clear()
        {
            _entrance?.Kill();
            _rotation?.Kill();
            _entrance = null;
            _rotation = null;
            if (_content != null) _content.localScale = Vector3.one;
            if (_group != null) _group.alpha = 1f;
            if (_rotatingLight != null) _rotatingLight.localRotation = _lightHomeRotation;
            if (_confetti != null) _confetti.Clear();
        }

        /// <summary>
        /// 親 HUD の終了・パネル切替時に演出を解除します。
        /// [KO] 부모 HUD 종료·패널 전환 시 연출을 해제합니다.
        /// [EN] Clears effects when the parent HUD ends or the panel is switched.
        /// </summary>
        private void OnDisable() => Clear();
    }
}
