using System;
using DG.Tweening;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// Runの要求を検査してAnimatorと攻撃移動を進め、効果と被弾反応を専用コンポーネントへ渡します。
    /// [KO] Run 요청을 검사해 Animator와 공격 이동을 진행하고 효과와 피격 반응을 전용 컴포넌트에 전달합니다.
    /// [EN] Validates run requests, advances the Animator and attack movement, and delegates effects and hit reactions to their components.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>
        /// 待機姿勢のAnimator状態名。
        /// [KO] 대기 자세 Animator 상태 이름입니다.
        /// [EN] Animator state for the idle pose.
        /// </summary>
        private const string IdleState = "Idle";

        /// <summary>
        /// 攻撃姿勢のAnimator状態名。
        /// [KO] 공격 자세 Animator 상태 이름입니다.
        /// [EN] Animator state for the attack pose.
        /// </summary>
        private const string AttackState = "Attack";

        /// <summary>
        /// 攻撃していない時の被弾姿勢。
        /// [KO] 공격하지 않을 때의 피격 자세입니다.
        /// [EN] Hit pose used while not attacking.
        /// </summary>
        private const string HitState = "Hit";

        /// <summary>
        /// 死亡姿勢のAnimator状態名。
        /// [KO] 사망 자세 Animator 상태 이름입니다.
        /// [EN] Animator state for the death pose.
        /// </summary>
        private const string DeathState = "Death";

        /// <summary>
        /// 接近後に武器の間合いとして残すワールド距離。
        /// [KO] 접근 후 무기 간격으로 남길 월드 거리입니다.
        /// [EN] World-space weapon reach retained after approaching.
        /// </summary>
        private const float MeleeReachWorldUnits = 1.35f;

        /// <summary>
        /// 発射までの時間のうち接近移動に使う比率。
        /// [KO] 발사까지의 시간 중 접근 이동에 사용하는 비율입니다.
        /// [EN] Fraction of the release delay spent approaching.
        /// </summary>
        private const float ApproachTravelFraction = 0.82f;

        /// <summary>
        /// 接近後、発射まで姿勢を保つ時間比率。
        /// [KO] 접근 후 발사까지 자세를 유지하는 시간 비율입니다.
        /// [EN] Fraction of the release delay spent holding the approach pose.
        /// </summary>
        private const float ApproachHoldFraction = 0.18f;

        /// <summary>
        /// 特殊な遠距離攻撃でモデルを拡大する倍率。
        /// [KO] 특수 원거리 공격에서 모델을 확대하는 배율입니다.
        /// [EN] Model scale multiplier for a special ranged attack.
        /// </summary>
        private const float SpecialAttackScaleMultiplier = 1.06f;

        /// <summary>
        /// 通常の遠距離攻撃でモデルを拡大する倍率。
        /// [KO] 일반 원거리 공격에서 모델을 확대하는 배율입니다.
        /// [EN] Model scale multiplier for a normal ranged attack.
        /// </summary>
        private const float NormalAttackScaleMultiplier = 1.025f;

        /// <summary>
        /// 手から剣先効果を前方へずらすワールド距離。
        /// [KO] 손에서 검 끝 효과를 앞쪽으로 옮길 월드 거리입니다.
        /// [EN] World-space forward offset of the blade effect from the hand.
        /// </summary>
        private const float StrikeForwardOffsetWorldUnits = 0.3f;

        /// <summary>
        /// ルート縮尺一倍での胴体命中高さ。
        /// [KO] 루트 스케일 1배일 때 몸통 명중 높이입니다.
        /// [EN] Torso impact height at unit root scale one.
        /// </summary>
        private const float ImpactHeightPerUnitScale = 1.25f;

        /// <summary>
        /// ルート縮尺一倍での死亡効果の高さ。
        /// [KO] 루트 스케일 1배일 때 사망 효과 높이입니다.
        /// [EN] Death-effect height at unit root scale one.
        /// </summary>
        private const float DeathHeightPerUnitScale = 0.5f;

        /// <summary>
        /// 自身の Animator。
        /// [KO] 자체 Animator입니다.
        /// [EN] Owned Animator.
        /// </summary>
        [SerializeField] private Animator _animator;

        /// <summary>
        /// 論理スロットと別に動かすモデル。
        /// [KO] 논리 슬롯과 별도로 움직이는 모델입니다.
        /// [EN] Model moved independently of the logical slot.
        /// </summary>
        [SerializeField] private Transform _model;

        /// <summary>
        /// 論理スロットの子に配置した、攻撃の接近・復帰だけを動かす軸。
        /// [KO] 논리 슬롯의 자식에 배치한 공격 접근·복귀만 움직일 축입니다.
        /// [EN] Authored pivot beneath the logical slot, moved only for attack approach and return.
        /// </summary>
        [SerializeField] private Transform _motionRoot;

        /// <summary>
        /// 移動軸の子で閃光・反動とその解除を担当する表示。
        /// [KO] 이동 축의 자식에서 섬광·반동과 그 해제를 담당할 표시입니다.
        /// [EN] Child view owning flash, recoil and their cleanup independently of attack movement.
        /// </summary>
        [SerializeField] private UnitHitReactionView _hitReaction;

        /// <summary>
        /// 接近や元の姿勢への復帰を含む、攻撃モーションの秒数。
        /// [KO] 접근과 원래 자세로의 복귀를 포함한 공격 모션 시간입니다.
        /// [EN] Attack-motion duration including approach and return.
        /// </summary>
        [SerializeField] private float _attackSeconds = 0.64f;

        /// <summary>
        /// 攻撃開始から斬撃・発射までの時間比率（0〜1）。
        /// [KO] 공격 시작부터 검격·발사까지의 시간 비율(0~1)입니다.
        /// [EN] Fraction of attack duration before strike or release, from 0 to 1.
        /// </summary>
        [SerializeField] private float _releaseNormalized = 0.52f;

        /// <summary>
        /// 投射体の飛行秒数。近接はゼロ。
        /// [KO] 투사체 비행 시간(초)이며 근접은 0입니다.
        /// [EN] Projectile flight seconds, zero for melee.
        /// </summary>
        [SerializeField] private float _flightSeconds;

        /// <summary>
        /// 購入クリップの元の長さ。AttackSpeed の計算に使用。
        /// [KO] 구매 클립 원래 길이이며 AttackSpeed 계산에 씁니다.
        /// [EN] Original purchased clip length used to calculate AttackSpeed.
        /// </summary>
        [SerializeField] private float _attackClipSeconds = 1.0666667f;

        /// <summary>
        /// 弓は左手、杖と銃は右手から発射。
        /// [KO] 활은 왼손, 지팡이·총은 오른손에서 발사합니다.
        /// [EN] Bows release from the left hand; staffs and crossbows use the right.
        /// </summary>
        [SerializeField] private bool _usesLeftHand;

        /// <summary>
        /// 同じユニットの効果を生成・再使用・停止するコンポーネント。
        /// [KO] 같은 유닛의 효과를 생성·재사용·정지하는 컴포넌트입니다.
        /// [EN] Component creating, reusing and stopping this unit's effects.
        /// </summary>
        [SerializeField] private UnitEffectsView _effects;

        /// <summary>
        /// 接続や攻撃が切り替わるたびに増やし、古い Tween の完了通知を見分ける番号。
        /// [KO] 연결이나 공격이 바뀔 때마다 증가해 이전 Tween 완료 알림을 구별할 번호입니다.
        /// [EN] Counter advanced when bindings or attacks change to identify stale tween completions.
        /// </summary>
        private int _generation;

        /// <summary>
        /// 現在の接続先の Run 識別子。
        /// [KO] 현재 연결된 Run 식별자입니다.
        /// [EN] Run identifier for the current binding.
        /// </summary>
        private Guid _runId;

        /// <summary>
        /// 戦闘に接続され、演出要求を受け付ける状態。
        /// [KO] 전투에 연결되어 연출 요청을 받을 수 있는 상태입니다.
        /// [EN] Whether the view is bound to a battle and can accept presentation requests.
        /// </summary>
        private bool _isBound;

        /// <summary>
        /// 解除時に戻す移動軸の初期位置。
        /// [KO] 해제 때 복원할 이동 축 초기 위치입니다.
        /// [EN] Home motion-pivot position restored on release.
        /// </summary>
        private Vector3 _homeMotionPosition;

        /// <summary>
        /// 解除時に戻すモデルの元の大きさ。
        /// [KO] 해제 때 복원할 모델 원래 크기입니다.
        /// [EN] Home model scale restored on release.
        /// </summary>
        private Vector3 _homeScale;

        /// <summary>
        /// 攻撃終了時に戻すモデルの元の向き。
        /// [KO] 공격 종료 때 복원할 모델 원래 방향입니다.
        /// [EN] Home model rotation restored after attacks.
        /// </summary>
        private Quaternion _homeRotation;

        /// <summary>
        /// 必須参照の確認後に元の姿勢を保存できた場合だけ復元を許可します。
        /// [KO] 필수 참조 확인 후 원래 자세를 저장한 경우에만 복원을 허용합니다.
        /// [EN] Allows pose restoration only after required references were validated and the home pose was captured.
        /// </summary>
        private bool _hasHomePose;

        /// <summary>
        /// この View が所有する接近・復帰 Tween。
        /// [KO] 이 View가 소유하는 접근·복귀 Tween입니다.
        /// [EN] Approach and return tween owned by this view.
        /// </summary>
        private Tween _motion;

        /// <summary>
        /// 攻撃モーションが終わるまでの残り秒数。
        /// [KO] 공격 모션이 끝날 때까지 남은 시간입니다.
        /// [EN] Seconds remaining in the attack motion.
        /// </summary>
        private float _attackRemaining;


        /// <summary>
        /// 矢・魔法弾の発射位置を読む手の骨。
        /// [KO] 화살·마법탄 발사 위치를 읽는 손 뼈입니다.
        /// [EN] Hand bone supplying arrow and bolt release positions.
        /// </summary>
        private Transform _hand;

        /// <summary>
        /// 購入クリップを指定秒数で再生する Controller パラメーター。
        /// [KO] 구매 클립을 지정 시간에 재생할 Controller 매개변수입니다.
        /// [EN] Controller parameter fitting a purchased clip into the configured duration.
        /// </summary>
        private static readonly int _attackSpeed = Animator.StringToHash("AttackSpeed");

        /// <summary>
        /// 死亡後の攻撃を拒否する状態。
        /// [KO] 사망 후 공격을 거부하는 상태입니다.
        /// [EN] State rejecting attacks after death.
        /// </summary>
        public bool IsDead { get; private set; }

        /// <summary>
        /// 最後に要求した Animator 状態。
        /// [KO] 마지막 요청 Animator 상태입니다.
        /// [EN] Last requested Animator state.
        /// </summary>
        public string CurrentAnimation { get; private set; }

        /// <summary>
        /// 現在の接続が有効か。
        /// [KO] 현재 연결이 유효한지 나타냅니다.
        /// [EN] Whether the current binding is valid.
        /// </summary>
        public bool IsBound => _isBound;

        /// <summary>
        /// 攻撃モーションが続いているかを示します。
        /// [KO] 공격 모션이 진행 중인지 나타냅니다.
        /// [EN] Whether the attack motion is still running.
        /// </summary>
        public bool IsAttacking => _attackRemaining > 0f;

        /// <summary>
        /// Presenter が攻撃終了の判定に使う秒数。
        /// [KO] Presenter가 공격 종료 판정에 사용할 시간입니다.
        /// [EN] Attack duration used by the presenter to determine completion.
        /// </summary>
        public float AttackDuration => _attackSeconds;

        /// <summary>
        /// 攻撃開始から斬撃・発射までの秒数。
        /// [KO] 공격 시작부터 베기·발사까지의 시간(초)입니다.
        /// [EN] Seconds from attack start to strike or release.
        /// </summary>
        public float ReleaseDelay => _attackSeconds * _releaseNormalized;

        /// <summary>
        /// 攻撃開始から斬撃または投射体到着までの秒数。
        /// [KO] 공격 시작부터 베기 또는 투사체 도착까지의 시간(초)입니다.
        /// [EN] Seconds from attack start to melee impact or projectile arrival.
        /// </summary>
        public float ImpactDelay => ReleaseDelay + _flightSeconds;

        /// <summary>
        /// 動くモデルの胴体位置。現在の相手を狙うために使用。
        /// [KO] 움직이는 모델 몸통 위치이며 현재 상대를 조준하는 데 씁니다.
        /// [EN] Moving torso position used to aim at the current target.
        /// </summary>
        public Vector3 ImpactPosition =>
            _model.position + Vector3.up * (ImpactHeightPerUnitScale * transform.lossyScale.y);

        /// <summary>
        /// プレハブの移動・反動軸を確認し、元の攻撃姿勢と手を一度記録します。
        /// [KO] 프리팹 이동·반동 축을 확인하고 원래 공격 자세와 손을 한 번 기록합니다.
        /// [EN] Validates authored motion and reaction pivots and caches the home attack pose and hand once.
        /// </summary>
        private void Awake()
        {
            if (_animator == null || _model == null || _motionRoot == null || _hitReaction == null ||
                _effects == null || _effects.gameObject != gameObject ||
                _motionRoot.parent != transform || _hitReaction.transform.parent != _motionRoot ||
                !_model.IsChildOf(_hitReaction.transform) || !(_attackSeconds > 0) || float.IsInfinity(_attackSeconds) ||
                !(_releaseNormalized > 0 && _releaseNormalized < 1) || !(_attackClipSeconds > 0) ||
                float.IsInfinity(_attackClipSeconds) || !(_flightSeconds >= 0) || float.IsInfinity(_flightSeconds))
            {
                throw new InvalidOperationException(
                    "Unit presentation references, authored pivots and timings must be valid.");
            }

            _homeMotionPosition = _motionRoot.localPosition;
            _homeScale = _model.localScale;
            _homeRotation = _model.localRotation;
            _hasHomePose = true;
            _hand = _animator.GetBoneTransform(_usesLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        }

        /// <summary>
        /// 被弾の閃光に使うパレットを UnitHitReactionView へ渡します。
        /// [KO] 피격 섬광에 사용할 팔레트를 UnitHitReactionView에 전달합니다.
        /// [EN] Passes the hit-flash palette to UnitHitReactionView.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
            => _hitReaction.ApplyPalette(colors);

        /// <summary>
        /// EntryPointが戦闘を接続する前に、このユニットの効果を準備します。
        /// [KO] EntryPoint가 전투를 연결하기 전에 이 유닛의 효과를 준비합니다.
        /// [EN] Prepares this unit's effects before EntryPoint connects the battle.
        /// </summary>
        public void PrepareEffects()
            => _effects.PrepareEffects();

        /// <summary>
        /// 接続中の攻撃の投射物を、手元から現在の対象位置へ進めます。
        /// [KO] 연결 중인 공격의 투사체를 손 위치에서 현재 대상 위치로 진행합니다.
        /// [EN] Advances the bound attack's projectile from the hand toward the current target position.
        /// </summary>
        public void PresentProjectile(int slot, Vector3 targetPosition, float progress)
        {
            if (!_isBound)
                return;

            _effects.PresentProjectile(slot, _hand != null ? _hand.position : ImpactPosition, targetPosition, progress);
        }

        /// <summary>
        /// 到着した投射物の先端を止め、尾は残り寿命だけ再生します。
        /// [KO] 도착한 투사체 선두를 멈추고 꼬리는 남은 수명만 재생합니다.
        /// [EN] Stops arrived projectile heads while allowing their trails to finish.
        /// </summary>
        public void HideProjectiles()
            => _effects.HideProjectiles();

        /// <summary>
        /// 現在のRunから届いた命中だけを対象位置に再生します。
        /// [KO] 현재 Run에서 전달한 명중만 대상 위치에 재생합니다.
        /// [EN] Plays only impacts from the current run at the target position.
        /// </summary>
        public void ShowImpact(Guid runId, int slot, Vector3 position)
        {
            if (_isBound && runId == _runId)
                _effects.ShowImpact(slot, position);
        }

        /// <summary>
        /// 現在のRunの生存中の攻撃者について、剣先の効果を一度再生します。
        /// [KO] 현재 Run에서 살아 있는 공격자에 대해 검 끝 효과를 한 번 재생합니다.
        /// [EN] Plays a blade effect for a living attacker bound to the current run.
        /// </summary>
        public void ShowStrike(Guid runId)
        {
            if (!_isBound || IsDead || runId != _runId)
                return;

            var position = _hand != null ? _hand.position : ImpactPosition;
            _effects.ShowStrike(position + _model.forward * StrikeForwardOffsetWorldUnits, _model.forward);
        }

        /// <summary>
        /// 前の演出を解除して新しい戦闘へ接続します。
        /// [KO] 이전 연출을 해제하고 새 전투에 연결합니다.
        /// [EN] Clears previous effects and binds a new battle.
        /// </summary>
        public void Bind(Guid runId)
        {
            Release();
            _runId = runId;
            _isBound = true;
            IsDead = false;
            Play(IdleState);
        }

        /// <summary>
        /// 攻撃モーションを始め、近接攻撃ならモデルを対象へ近づけます。
        /// [KO] 공격 모션을 시작하고 근접 공격이면 모델을 대상에게 접근시킵니다.
        /// [EN] Starts attack motion and moves the model toward the target for melee attacks.
        /// </summary>
        public void ShowAttack(Guid runId, Vector3 targetPosition, bool isMelee, bool isSpecial,
            float approachFraction = 0.5f)
        {
            if (!_isBound || IsDead || runId != _runId)
            {
                return;
            }

            StopMotion();
            _attackRemaining = _attackSeconds;
            _animator.SetFloat(_attackSpeed, _attackClipSeconds / _attackSeconds);
            Play(AttackState);
            var direction = targetPosition - _motionRoot.position;
            direction.y = 0;
            if (direction.sqrMagnitude > Define.MinimumDirectionSqrMagnitude)
            {
                _model.rotation = Quaternion.LookRotation(direction);
            }

            var generation = _generation;
            // 接近と姿勢の Tween を、他の演出と同じゲーム内時間で進める。
            var sequence = DOTween.Sequence().SetUpdate(UpdateType.Normal, false).SetRecyclable(false);
            _motion = sequence;
            AppendAttackMotion(sequence, direction, isMelee, isSpecial, approachFraction);

            // この戦闘と世代の終了だけを受け、旧 Tween が死亡や新しい攻撃を上書きしないようにする。
            sequence.OnComplete(() =>
            {
                if (this == null || !_isBound || IsDead || generation != _generation || runId != _runId)
                {
                    return;
                }

                _motionRoot.localPosition = _homeMotionPosition;
                _model.localScale = _homeScale;
                _model.localRotation = _homeRotation;
                _attackRemaining = 0;
                _motion = null;
                Play(IdleState);
            });
        }

        /// <summary>
        /// 近接なら間合いまで往復し、遠距離なら姿勢を拡縮する動きを攻撃Sequenceへ追加します。
        /// [KO] 근접이면 사거리까지 왕복하고 원거리면 자세를 확대·축소하는 움직임을 공격 Sequence에 추가합니다.
        /// [EN] Appends a melee approach-and-return or ranged scale motion to the attack sequence.
        /// </summary>
        private void AppendAttackMotion(Sequence sequence, Vector3 direction, bool isMelee, bool isSpecial, float approachFraction)
        {
            if (isMelee)
            {
                // 前列の距離 3.6 なら、武器の間合い 1.35 を残す移動 2.25 を双方で半分ずつ使う。
                // 後列へは片方だけが近づくため全距離を使う。論理スロットは動かさない。
                var distance = Mathf.Max(0, direction.magnitude - MeleeReachWorldUnits) * approachFraction;
                var destination = _motionRoot.position + direction.normalized * distance;
                var localDestination = _motionRoot.parent.InverseTransformPoint(destination);
                sequence.Append(_motionRoot.DOLocalMove(localDestination, ReleaseDelay * ApproachTravelFraction)
                    .SetEase(Ease.InOutSine));
                sequence.AppendInterval(ReleaseDelay * ApproachHoldFraction);
                sequence.Append(_motionRoot.DOLocalMove(_homeMotionPosition, _attackSeconds - ReleaseDelay)
                    .SetEase(Ease.OutSine));
            }
            else
            {
                sequence.Append(_model.DOScale(
                    _homeScale * (isSpecial ? SpecialAttackScaleMultiplier : NormalAttackScaleMultiplier),
                    ReleaseDelay));
                sequence.Append(_model.DOScale(_homeScale, _attackSeconds - ReleaseDelay));
            }

        }

        /// <summary>
        /// 攻撃は保護し、命中時に閃光と攻撃元の反対への小さな押し戻しを開始します。
        /// [KO] 공격을 보호하고 명중 때 섬광과 공격 원점 반대 방향의 작은 밀림을 시작합니다.
        /// [EN] Protects attacks and starts a flash and a small push away from the attacker at impact.
        /// </summary>
        public void ShowHit(Guid runId, Vector3 sourcePosition)
        {
            if (!_isBound || IsDead || runId != _runId)
                return;

            if (!IsAttacking)
                Play(HitState);
            _hitReaction.Play(sourcePosition);
        }

        /// <summary>
        /// この一人の攻撃保護と被弾表示だけを毎フレーム進めます。
        /// [KO] 이 유닛의 공격 보호와 피격 표시만 매 프레임 진행합니다.
        /// [EN] Advances only this unit's attack protection and hit presentation each frame.
        /// </summary>
        private void Update()
        {
            // 攻撃モーションの残り時間と被弾反応を、ゲーム内の経過秒で進める。
            var presentationDeltaSeconds = Time.deltaTime;
            _attackRemaining = Mathf.Max(0, _attackRemaining - presentationDeltaSeconds);
            _hitReaction.Advance(presentationDeltaSeconds);
        }

        /// <summary>
        /// 現在の位置で死亡モーションを開始し、反動位置を固定して魂の効果を再生します。
        /// [KO] 현재 위치에서 사망 모션을 시작하고 반동 위치를 고정해 영혼 효과를 재생합니다.
        /// [EN] Starts death motion at the current position, freezes recoil and plays the soul effect.
        /// </summary>
        public void ShowDeath(Guid runId)
        {
            if (!_isBound || IsDead || runId != _runId)
            {
                return;
            }

            StopMotion(false);
            _hitReaction.Freeze();
            IsDead = true;
            Play(DeathState);
            _effects.ShowDeath(_model.position + Vector3.up * (DeathHeightPerUnitScale * transform.lossyScale.y));
        }

        /// <summary>
        /// 最後の攻撃後、生存者だけを待機へ戻します。
        /// [KO] 마지막 공격 후 생존자만 대기로 되돌립니다.
        /// [EN] Returns survivors to idle after final attacks.
        /// </summary>
        public void FinishBattle()
        {
            if (_isBound && !IsDead)
            {
                StopMotion();
                Play(IdleState);
            }
        }

        /// <summary>
        /// 移動と効果を止め、モデルの位置・姿勢・被弾表示を初期状態に戻します。
        /// [KO] 이동과 효과를 멈추고 모델 위치·자세·피격 표시를 초기 상태로 되돌립니다.
        /// [EN] Stops motion and effects and restores the model position, pose and hit appearance.
        /// </summary>
        public void Release()
        {
            _isBound = false;
            StopMotion();
            if (_effects != null)
                _effects.Clear();
            if (_hitReaction != null)
                _hitReaction.Clear();
        }

        /// <summary>
        /// 自分の Tween を無効にし、死亡以外は元の姿勢を復元します。
        /// [KO] 자체 Tween을 무효화하고 사망 이외에는 원래 자세를 복원합니다.
        /// [EN] Invalidates owned tweens and restores home transforms except for death.
        /// </summary>
        private void StopMotion(bool restorePose = true)
        {
            _generation++;
            _motion?.Kill();
            _motion = null;
            _attackRemaining = 0;
            if (!_hasHomePose || !restorePose)
            {
                return;
            }

            if (_motionRoot != null)

                _motionRoot.localPosition = _homeMotionPosition;
            if (_model != null)
            {
                _model.localScale = _homeScale;
                _model.localRotation = _homeRotation;
            }
        }

        /// <summary>
        /// 自身の Controller の状態を選びます。
        /// [KO] 자체 Controller 상태를 선택합니다.
        /// [EN] Selects a state in the owned controller.
        /// </summary>
        private void Play(string state)
        {
            CurrentAnimation = state;
            _animator.Play(state, 0, 0);
        }

        /// <summary>
        /// 非表示時にも所有演出を解除します。
        /// [KO] 비활성화 시에도 소유 연출을 해제합니다.
        /// [EN] Releases owned effects when disabled.
        /// </summary>
        private void OnDisable()
            => Release();
    }
}
