using System;
using DG.Tweening;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// ユニットの攻撃・接近・投射物・被弾・死亡の演出を進めます。
    /// [KO] 유닛의 공격·접근·투사체·피격·사망 연출을 진행합니다.
    /// [EN] Runs a unit's attack, approach, projectile, hit and death presentation.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
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
        /// 飛行の中央で投射物を持ち上げるワールド高さ。
        /// [KO] 비행 중간에서 투사체를 들어 올리는 월드 높이입니다.
        /// [EN] World-space projectile height at the middle of its arc.
        /// </summary>
        private const float ProjectileArcHeightWorldUnits = 0.25f;

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
        /// ほぼ同じ位置で不定な向きを作らないための距離二乗。
        /// [KO] 거의 같은 위치에서 불확정 방향을 만들지 않을 제곱 거리 기준입니다.
        /// [EN] Squared-distance threshold avoiding an undefined direction at nearly identical positions.
        /// </summary>
        private const float MinimumDirectionSqrMagnitude = 0.001f;

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
        /// 開始時に三個作り広域攻撃にも使い回す投射体。
        /// [KO] 시작 시 세 개를 만들어 광역 공격에도 재사용할 투사체입니다.
        /// [EN] Projectile instantiated three times at startup for single and area attacks.
        /// </summary>
        [SerializeField] private BattleEffectView _projectilePrefab;

        /// <summary>
        /// 弓は左手、杖と銃は右手から発射。
        /// [KO] 활은 왼손, 지팡이·총은 오른손에서 발사합니다.
        /// [EN] Bows release from the left hand; staffs and crossbows use the right.
        /// </summary>
        [SerializeField] private bool _usesLeftHand;

        /// <summary>
        /// この攻撃者の命中を相手の位置に示す効果。近接・矢・魔法で使い分けます。
        /// [KO] 이 공격자의 명중을 상대 위치에 표시할 효과이며 근접·화살·마법별로 구분합니다.
        /// [EN] Effect showing this attacker's impact at its target, selected for melee, arrows or magic.
        /// </summary>
        [SerializeField] private BattleEffectView _impactPrefab;

        /// <summary>
        /// 近接の命中時に剣先で再生する効果。遠距離には設定しません。
        /// [KO] 근접 명중 때 검 끝에서 재생할 효과이며 원거리에는 설정하지 않습니다.
        /// [EN] Effect played at the blade on melee impact, left unassigned for ranged units.
        /// </summary>
        [SerializeField] private BattleEffectView _strikePrefab;

        /// <summary>
        /// 自身の死亡位置から一度だけ再生する魂の効果。
        /// [KO] 자체 사망 위치에서 한 번만 재생할 영혼 효과입니다.
        /// [EN] Soul effect played once at this unit's death position.
        /// </summary>
        [SerializeField] private BattleEffectView _deathPrefab;

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
        /// View の寿命で生成し、最大三対象へ同時に飛ばす表示。
        /// [KO] View 수명에 생성해 최대 세 대상에 동시에 날릴 표시입니다.
        /// [EN] View-owned visuals supporting up to three simultaneous targets.
        /// </summary>
        private readonly BattleEffectView[] _projectiles = new BattleEffectView[Define.SlotsPerTeam];

        /// <summary>
        /// 各発射で一度だけ記録する手元の座標。
        /// [KO] 발사마다 한 번 기록할 손의 위치입니다.
        /// [EN] Hand position recorded once for each projectile release.
        /// </summary>
        private readonly Vector3[] _projectileStarts = new Vector3[Define.SlotsPerTeam];

        /// <summary>
        /// 矢・魔法弾の発射位置を読む手の骨。
        /// [KO] 화살·마법탄 발사 위치를 읽는 손 뼈입니다.
        /// [EN] Hand bone supplying arrow and bolt release positions.
        /// </summary>
        private Transform _hand;

        /// <summary>
        /// 最大三対象の命中で再利用し、View の破棄と共に消える効果。
        /// [KO] 최대 세 대상 명중에 재사용하고 View 파괴와 함께 사라질 효과입니다.
        /// [EN] Reusable impact effects for up to three targets, destroyed with their owning view.
        /// </summary>
        private readonly BattleEffectView[] _impacts = new BattleEffectView[Define.SlotsPerTeam];

        /// <summary>
        /// このユニットが所有する剣先の一時表示。
        /// [KO] 이 유닛이 소유할 검 끝의 일시 표시입니다.
        /// [EN] Temporary blade effect owned by this unit.
        /// </summary>
        private BattleEffectView _strike;

        /// <summary>
        /// このユニットが所有し、死亡後も原点へ戻さず再生する魂。
        /// [KO] 이 유닛이 소유하고 사망 후 원점으로 되돌리지 않고 재생할 영혼입니다.
        /// [EN] Unit-owned soul effect played at death without returning to the home position.
        /// </summary>
        private BattleEffectView _death;


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
                _motionRoot.parent != transform || _hitReaction.transform.parent != _motionRoot ||
                !_model.IsChildOf(_hitReaction.transform) || _attackSeconds <= 0 || _releaseNormalized <= 0 ||
                _releaseNormalized >= 1 || _attackClipSeconds <= 0 || _flightSeconds < 0)
            {
                throw new InvalidOperationException(
                    "Unit presentation references, authored pivots and timings must be valid.");
            }

            _homeMotionPosition = _motionRoot.localPosition;
            _homeScale = _model.localScale;
            _homeRotation = _model.localRotation;
            _hand = _animator.GetBoneTransform(_usesLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        }

        /// <summary>
        /// 被弾の閃光に使うパレットを UnitHitReactionView へ渡します。
        /// [KO] 피격 섬광에 사용할 팔레트를 UnitHitReactionView에 전달합니다.
        /// [EN] Passes the hit-flash palette to UnitHitReactionView.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors) => _hitReaction.ApplyPalette(colors);

        /// <summary>
        /// EntryPoint が接続前に呼び、ユニット所有の効果を一度準備します。再呼出しでは既存効果を再利用します。
        /// [KO] EntryPoint가 연결 전에 호출하여 유닛 소유 효과를 한 번 준비하며 재호출 시 기존 효과를 재사용합니다.
        /// [EN] Called by EntryPoint before binding to prepare unit-owned effects once, reusing them on subsequent calls.
        /// </summary>
        public void PrepareEffects()
        {
            if (_projectilePrefab != null && _projectiles[0] == null)
            {
                for (var i = 0; i < _projectiles.Length; i++)
                {
                    _projectiles[i] = CreateEffect(_projectilePrefab, "VFX_Projectile" + i);
                }
            }

            if (_impactPrefab != null && _impacts[0] == null)
            {
                for (var i = 0; i < _impacts.Length; i++)
                {
                    _impacts[i] = CreateEffect(_impactPrefab, "VFX_Impact" + i);
                }
            }

            if (_strikePrefab != null && _strike == null)
            {
                _strike = CreateEffect(_strikePrefab, "VFX_SwordStrike");
            }

            if (_deathPrefab != null && _death == null)
            {
                _death = CreateEffect(_deathPrefab, "VFX_Death");
            }
        }

        /// <summary>
        /// ユニットの子としてエフェクトを作り、再生待ちの状態にします。
        /// [KO] 유닛 자식으로 효과를 생성해 재생 대기 상태로 만듭니다.
        /// [EN] Creates an effect as a unit child and leaves it ready for playback.
        /// </summary>
        private BattleEffectView CreateEffect(BattleEffectView prefab, string effectName)
        {
            var effect = Instantiate(prefab, transform);
            effect.name = effectName;
            effect.Clear();
            return effect;
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
            Play("Idle");
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
            Play("Attack");
            var direction = targetPosition - _motionRoot.position;
            direction.y = 0;
            if (direction.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                _model.rotation = Quaternion.LookRotation(direction);
            }

            var generation = _generation;
            // 接近と姿勢の Tween を、他の演出と同じゲーム内時間で進める。
            var sequence = DOTween.Sequence().SetUpdate(UpdateType.Normal, false);
            _motion = sequence;
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
                Play("Idle");
            });
        }

        /// <summary>
        /// 発射から到着まで、同じ対象の現在位置へ矢・魔法弾を進めます。
        /// [KO] 발사부터 도착까지 같은 대상의 현재 위치로 화살·마법탄을 이동합니다.
        /// [EN] Advances an arrow or bolt toward the current position of its target.
        /// </summary>
        public void PresentProjectile(int slot, Vector3 targetPosition, float progress)
        {
            if (!_isBound || _projectiles[slot] == null)
            {
                return;
            }

            var projectile = _projectiles[slot];
            var start = _projectileStarts[slot];
            if (!projectile.IsEmitting)
            {
                start = _hand != null ? _hand.position : ImpactPosition;
                _projectileStarts[slot] = start;
                // 飛行方向を +Z に向けて再生する。発射位置を変える前に前回の尾を消す。
                projectile.Play(start, targetPosition - start);
            }

            projectile.transform.position = Vector3.Lerp(start, targetPosition, progress) +
                                            Vector3.up *
                                            (Mathf.Sin(progress * Mathf.PI) * ProjectileArcHeightWorldUnits);
            var direction = targetPosition - start;
            if (direction.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                projectile.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        /// <summary>
        /// 到着した先端を止め、尾の粒子は残り寿命だけ再生します。戦闘解除なら全て消します。
        /// [KO] 도착한 선두를 멈추고 꼬리 입자는 남은 수명만 재생하며 전투 해제라면 모두 지웁니다.
        /// [EN] Stops arrived heads while tails finish their remaining lifetime, clearing everything on battle release.
        /// </summary>
        public void HideProjectiles(bool clear = false)
        {
            foreach (var projectile in _projectiles)
            {
                if (projectile != null)
                {
                    if (clear)
                    {
                        projectile.Clear();
                    }
                    else
                    {
                        projectile.StopFlight();
                    }
                }
            }
        }

        /// <summary>
        /// Presenter が命中時に呼び、この攻撃者の効果を対象の胴体位置に再生します。
        /// [KO] Presenter가 명중 때 호출해 이 공격자의 효과를 대상 몸통 위치에 재생합니다.
        /// [EN] Called by the presenter at impact to play this attacker's effect at the target's torso.
        /// </summary>
        public void ShowImpact(Guid runId, int slot, Vector3 position)
        {
            // slot は一攻撃の被害列の 0〜2。編成のスロット番号ではなく、広域攻撃の三表示を分ける。
            if (_isBound && runId == _runId && _impacts[slot] != null)
            {
                _impacts[slot].Play(position, Vector3.forward);
            }
        }

        /// <summary>
        /// Presenter が一攻撃につき一度、斬撃の命中時に剣先効果を開始します。
        /// [KO] Presenter가 공격당 한 번 베기 명중 때 검 끝 효과를 시작합니다.
        /// [EN] Called once per attack by the presenter to start a blade effect at melee impact.
        /// </summary>
        public void ShowStrike(Guid runId)
        {
            if (_isBound && !IsDead && runId == _runId && _strike != null)
            {
                var position = _hand != null ? _hand.position : ImpactPosition;
                _strike.Play(position + _model.forward * StrikeForwardOffsetWorldUnits, _model.forward);
            }
        }

        /// <summary>
        /// 攻撃は保護し、命中時に閃光と攻撃元の反対への小さな押し戻しを開始します。
        /// [KO] 공격을 보호하고 명중 때 섬광과 공격 원점 반대 방향의 작은 밀림을 시작합니다.
        /// [EN] Protects attacks and starts a flash and a small push away from the attacker at impact.
        /// </summary>
        public void ShowHit(Guid runId, Vector3 sourcePosition)
        {
            if (!_isBound || IsDead || runId != _runId) return;
            if (!IsAttacking) Play("Hit");
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
            Play("Death");
            if (_death != null)
            {
                _death.Play(_model.position + Vector3.up * (DeathHeightPerUnitScale * transform.lossyScale.y),
                    Vector3.forward);
            }
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
                Play("Idle");
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
            HideProjectiles(true);
            if (_hitReaction != null) _hitReaction.Clear();
            foreach (var impact in _impacts)
            {
                if (impact != null)
                {
                    impact.Clear();
                }
            }

            if (_strike != null) _strike.Clear();
            if (_death != null) _death.Clear();
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
            if (_model == null || !restorePose)
            {
                return;
            }

            _motionRoot.localPosition = _homeMotionPosition;
            _model.localScale = _homeScale;
            _model.localRotation = _homeRotation;
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
        private void OnDisable() => Release();
    }
}
