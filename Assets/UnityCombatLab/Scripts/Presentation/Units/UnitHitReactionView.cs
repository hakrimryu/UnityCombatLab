using System;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// モデルの表示位置と色を動かし、被弾時の押し戻しと閃光を描きます。
    /// [KO] 모델의 표시 위치와 색을 바꿔 피격 시 밀림과 섬광을 표현합니다.
    /// [EN] Animates the model's display position and color for hit recoil and flash.
    /// </summary>
    public sealed class UnitHitReactionView : MonoBehaviour
    {
        /// <summary>
        /// この反動軸の子にある、材質を一時上書きするモデル。
        /// [KO] 이 반동 축의 자식에 있는 재질을 임시 오버라이드할 모델입니다.
        /// [EN] Child model whose renderer properties are temporarily overridden.
        /// </summary>
        [SerializeField] private Transform _visualRoot;

        /// <summary>
        /// 閃光の全体秒数。
        /// [KO] 섬광 전체 시간(초)입니다.
        /// [EN] Total flash duration in seconds.
        /// </summary>
        [SerializeField] private float _flashSeconds = 0.1f;

        /// <summary>
        /// 最大輝度を保つ秒数。
        /// [KO] 최대 밝기 유지 시간(초)입니다.
        /// [EN] Seconds spent at full flash brightness.
        /// </summary>
        [SerializeField] private float _flashHoldSeconds = 0.035f;

        /// <summary>
        /// 共通パレットの明色へ掛ける発光の強さ。
        /// [KO] 공통 팔레트의 밝은 색에 곱할 발광 강도입니다.
        /// [EN] Emission intensity multiplying the shared palette's bright color.
        /// </summary>
        [SerializeField] private float _flashIntensity = 3f;

        /// <summary>
        /// 攻撃元から離れる距離。ワールド座標の単位で指定します。
        /// [KO] 공격 원점에서 멀어질 거리이며 월드 좌표 단위로 지정합니다.
        /// [EN] Distance to move away from the attacker, measured in world units.
        /// </summary>
        [SerializeField] private float _recoilDistance = 0.14f;

        /// <summary>
        /// 押されて元へ戻る全体秒数。
        /// [KO] 밀렸다가 원위치로 돌아올 전체 시간(초)입니다.
        /// [EN] Total seconds for push and return.
        /// </summary>
        [SerializeField] private float _recoilSeconds = 0.18f;

        /// <summary>
        /// 最大変位まで動く秒数。残り時間で元へ戻します。
        /// [KO] 최대 변위까지 움직일 시간(초)이며 나머지 시간에 원위치로 돌아옵니다.
        /// [EN] Seconds spent moving outward, with the remaining time used to return.
        /// </summary>
        [SerializeField] private float _recoilOutSeconds = 0.045f;

        /// <summary>
        /// ApplyPalette で借用し、個体の閃光色だけに使用する共通設定。
        /// [KO] ApplyPalette로 빌려 개체 섬광색에만 사용할 공통 설정입니다.
        /// [EN] Shared settings borrowed through ApplyPalette and used only for the instance flash color.
        /// </summary>
        private GameColorPalette _colors;

        /// <summary>
        /// 初期位置を保存し、反動終了・解除時に戻します。
        /// [KO] 반동 종료·해제 때 돌아갈 초기 위치입니다.
        /// [EN] Initial local position restored after recoil or release.
        /// </summary>
        private Vector3 _homePosition;

        /// <summary>
        /// この表示だけが上書きする Renderer 一覧。
        /// [KO] 이 표시만 오버라이드할 Renderer 목록입니다.
        /// [EN] Renderers whose properties this view overrides.
        /// </summary>
        private Renderer[] _renderers;

        /// <summary>
        /// 閃光終了時に個体ごとに復元する元の設定。
        /// [KO] 섬광 종료 때 개체별로 복원할 원래 설정입니다.
        /// [EN] Original per-renderer property blocks restored after the flash.
        /// </summary>
        private MaterialPropertyBlock[] _originalProperties;

        /// <summary>
        /// 閃光から戻すため保存した Renderer の元の色。
        /// [KO] 섬광에서 복원하려고 저장한 Renderer 원래 색입니다.
        /// [EN] Original renderer colors retained for flash restoration.
        /// </summary>
        private Color[] _originalColors;

        /// <summary>
        /// このユニットの色と発光を設定するために再利用する MaterialPropertyBlock。
        /// [KO] 이 유닛의 색상과 발광을 설정하도록 재사용할 MaterialPropertyBlock입니다.
        /// [EN] Reusable MaterialPropertyBlock for this unit's color and emission.
        /// </summary>
        private MaterialPropertyBlock _flashProperties;

        /// <summary>
        /// 次の Advance が進める閃光の残り秒数。
        /// [KO] 다음 Advance가 진행할 섬광 잔여 시간(초)입니다.
        /// [EN] Flash seconds remaining for the next advance.
        /// </summary>
        private float _flashRemaining;

        /// <summary>
        /// 次の Advance が進める反動の残り秒数。
        /// [KO] 다음 Advance가 진행할 반동 잔여 시간(초)입니다.
        /// [EN] Recoil seconds remaining for the next advance.
        /// </summary>
        private float _recoilRemaining;

        /// <summary>
        /// 連続被弾の開始位置。現在の変位を維持して次へつなぎます。
        /// [KO] 현재 변위를 유지해 다음 피격으로 이어갈 시작 위치입니다.
        /// [EN] Current offset retained as the start of a repeated hit.
        /// </summary>
        private Vector3 _recoilStart;

        /// <summary>
        /// 現在の攻撃元から離れる、親座標での目標位置。
        /// [KO] 현재 공격 원점에서 멀어질 부모 좌표의 목표 위치입니다.
        /// [EN] Parent-space target position away from the current attacker.
        /// </summary>
        private Vector3 _recoilTarget;

        /// <summary>
        /// 死亡位置の反動を固定し、新しい反応を拒否する状態。
        /// [KO] 사망 위치의 반동을 고정하고 새 반응을 거부할 상태입니다.
        /// [EN] State freezing recoil at death and rejecting new reactions.
        /// </summary>
        private bool _isFrozen;

        /// <summary>
        /// 個体の基本色を上書きする URP プロパティ番号。
        /// [KO] 개체 기본색을 오버라이드할 URP 프로퍼티 번호입니다.
        /// [EN] URP property ID overriding the instance base color.
        /// </summary>
        private static readonly int _baseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 個体の発光色を上書きする URP プロパティ番号。
        /// [KO] 개체 발광색을 오버라이드할 URP 프로퍼티 번호입니다.
        /// [EN] URP property ID overriding the instance emission color.
        /// </summary>
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// 閃光の間だけ内蔵白マップへ切り替える番号。
        /// [KO] 섬광 동안만 내장 흰 맵으로 바꿀 번호입니다.
        /// [EN] Property ID switching to the built-in white map during the flash.
        /// </summary>
        private static readonly int _emissionMap = Shader.PropertyToID("_EmissionMap");

        /// <summary>
        /// 反動で移動した距離と方向をワールド座標で返します。
        /// [KO] 반동으로 이동한 거리와 방향을 월드 좌표로 반환합니다.
        /// [EN] Returns recoil displacement in world coordinates.
        /// </summary>
        public Vector3 Displacement => transform.parent.TransformVector(transform.localPosition - _homePosition);

        /// <summary>
        /// モデルと時間設定を確認し、初期位置と Renderer の色・材質設定を保存します。
        /// [KO] 모델과 시간 설정을 확인하고 초기 위치와 Renderer의 색상·재질 설정을 저장합니다.
        /// [EN] Checks the model and timings and saves its initial position and renderer color and material settings.
        /// </summary>
        private void Awake()
        {
            if (_visualRoot == null || !_visualRoot.IsChildOf(transform) || transform.parent == null ||
                _flashSeconds <= 0 || _flashHoldSeconds < 0 || _flashHoldSeconds >= _flashSeconds ||
                _flashIntensity <= 0 || _recoilDistance < 0 || _recoilSeconds <= 0 ||
                _recoilOutSeconds <= 0 || _recoilOutSeconds >= _recoilSeconds)
            {
                throw new InvalidOperationException("Hit reaction references and timings must be valid.");
            }

            _homePosition = transform.localPosition;
            _renderers = _visualRoot.GetComponentsInChildren<Renderer>();
            _originalProperties = new MaterialPropertyBlock[_renderers.Length];
            _originalColors = new Color[_renderers.Length];
            _flashProperties = new MaterialPropertyBlock();
            for (var i = 0; i < _renderers.Length; i++)
            {
                var block = new MaterialPropertyBlock();
                _renderers[i].GetPropertyBlock(block);
                _originalProperties[i] = block;
                var material = _renderers[i].sharedMaterial;
                _originalColors[i] = block.HasColor(_baseColor) ? block.GetColor(_baseColor) :
                    material != null && material.HasProperty(_baseColor) ? material.GetColor(_baseColor) : default;
            }
        }

        /// <summary>
        /// UnitView から、閃光色に使う共通パレットを受け取ります。
        /// [KO] UnitView에서 섬광 색상에 사용할 공통 팔레트를 전달받습니다.
        /// [EN] Receives the shared flash-color palette from UnitView.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
        {
            _colors = colors != null ? colors : throw new ArgumentNullException(nameof(colors));
        }

        /// <summary>
        /// 現在の攻撃元から離れる反動と閃光を開始します。連続被害は現在位置からつなぎます。
        /// [KO] 현재 공격 원점에서 멀어질 반동·섬광을 시작하며 연속 피해는 현재 위치에서 이어갑니다.
        /// [EN] Starts flash and recoil away from the attacker, continuing repeated hits from the current offset.
        /// </summary>
        public void Play(Vector3 sourcePosition)
        {
            if (_isFrozen || !isActiveAndEnabled) return;
            if (_colors == null)
                throw new InvalidOperationException("Apply the hit palette before playing a reaction.");
            var direction = _visualRoot.position - sourcePosition;
            direction.y = 0;
            if (direction.sqrMagnitude < Define.MinimumDirectionSqrMagnitude) direction = -_visualRoot.forward;
            // 例: 距離0.14を親の縮尺で換算し、モデルの大きさによらず同じ幅だけ動かす。
            _recoilStart = transform.localPosition;
            _recoilTarget = _homePosition +
                            transform.parent.InverseTransformVector(direction.normalized * _recoilDistance);
            _flashRemaining = _flashSeconds;
            _recoilRemaining = _recoilSeconds;
            RenderFlash(1f);
        }

        /// <summary>
        /// 経過秒に応じて、閃光の明るさと反動の位置を更新します。
        /// [KO] 경과 시간에 따라 섬광 밝기와 반동 위치를 갱신합니다.
        /// [EN] Updates flash brightness and recoil position from elapsed seconds.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (_recoilRemaining > 0)
            {
                _recoilRemaining = Mathf.Max(0, _recoilRemaining - deltaSeconds);
                var elapsed = _recoilSeconds - _recoilRemaining;
                transform.localPosition = elapsed < _recoilOutSeconds
                    ? Vector3.Lerp(_recoilStart, _recoilTarget, elapsed / _recoilOutSeconds)
                    : Vector3.Lerp(_recoilTarget, _homePosition,
                        (elapsed - _recoilOutSeconds) / (_recoilSeconds - _recoilOutSeconds));
            }

            if (_flashRemaining <= 0) return;
            _flashRemaining = Mathf.Max(0, _flashRemaining - deltaSeconds);
            RenderFlash(Mathf.Clamp01(_flashRemaining / (_flashSeconds - _flashHoldSeconds)));
        }

        /// <summary>
        /// 死亡表示の位置を固定します。閃光だけは残り時間で消えます。
        /// [KO] 사망 표시 위치를 고정하며 섬광만 남은 시간에 사라집니다.
        /// [EN] Freezes the death position while the flash fades for its remaining duration.
        /// </summary>
        public void Freeze()
        {
            _isFrozen = true;
            _recoilRemaining = 0;
        }

        /// <summary>
        /// UnitView の解除・再接続で呼び、位置・閃光・死亡固定をすべて戻します。
        /// [KO] UnitView 해제·재연결 때 호출해 위치·섬광·사망 고정을 모두 복원합니다.
        /// [EN] Called on UnitView release or rebind to restore position, flash and the death freeze.
        /// </summary>
        public void Clear()
        {
            _flashRemaining = 0;
            _recoilRemaining = 0;
            _isFrozen = false;
            if (_renderers == null) return;
            transform.localPosition = _homePosition;
            RenderFlash(0);
        }

        /// <summary>
        /// 閃光の強さに合わせて色と発光を設定し、強さ 0 で保存済みの設定に戻します。
        /// [KO] 섬광 강도에 따라 색상과 발광을 설정하고 강도가 0이면 저장한 설정으로 되돌립니다.
        /// [EN] Sets color and emission for flash strength and restores saved settings at zero strength.
        /// </summary>
        private void RenderFlash(float strength)
        {
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (strength <= 0)
                {
                    _renderers[i].SetPropertyBlock(_originalProperties[i]);
                    continue;
                }

                _renderers[i].GetPropertyBlock(_flashProperties);
                var color = _colors.GetColor(Define.PaletteColor.OnAccent);
                _flashProperties.SetColor(_baseColor, Color.Lerp(_originalColors[i], color, strength * 0.65f));
                _flashProperties.SetTexture(_emissionMap, Texture2D.whiteTexture);
                _flashProperties.SetColor(_emissionColor, color * (_flashIntensity * strength));
                _renderers[i].SetPropertyBlock(_flashProperties);
            }
        }

        /// <summary>
        /// 非表示時に反動位置と Renderer の設定を戻します。
        /// [KO] 비활성화 시 반동 위치와 Renderer 설정을 되돌립니다.
        /// [EN] Restores recoil position and renderer settings when disabled.
        /// </summary>
        private void OnDisable() => Clear();
    }
}
