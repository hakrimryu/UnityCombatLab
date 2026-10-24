using System;
using DG.Tweening;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// 攻撃・接近・投射体・被弾を同時進行で描き、戦闘の損失量は決めません。
    /// [KO] 공격·접근·투사체·피격을 동시에 그리며 전투 손실량은 결정하지 않습니다.
    /// [EN] Presents overlapping attacks, approaches, projectiles and hits without deciding combat damage.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>
        /// 閃光の全体秒数と、最大輝度を保つ秒数。
        /// [KO] 섬광 전체 시간과 최대 밝기 유지 시간(초)입니다.
        /// [EN] Total flash seconds and the time spent at full brightness.
        /// </summary>
        private const float HitFlashSeconds = 0.1f;

        /// <summary>
        /// 命中直後に白い姿を読めるよう保つ秒数。
        /// [KO] 명중 직후 흰 실루엣을 읽을 수 있게 유지할 시간(초)입니다.
        /// [EN] Seconds retaining a readable bright silhouette immediately after impact.
        /// </summary>
        private const float HitFlashHoldSeconds = 0.035f;

        /// <summary>
        /// 小さく押されて戻る全体秒数。
        /// [KO] 살짝 밀렸다가 돌아올 전체 시간(초)입니다.
        /// [EN] Total seconds for the small push and return.
        /// </summary>
        private const float RecoilSeconds = 0.18f;

        /// <summary>
        /// 押される動きを終えるまでの秒数。残り時間で元へ戻します。
        /// [KO] 밀리는 동작을 끝낼 시간(초)이며 나머지 시간에 원위치로 돌아옵니다.
        /// [EN] Seconds spent moving outward, with the remaining time used to return.
        /// </summary>
        private const float RecoilOutSeconds = 0.045f;

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
        /// 次の攻撃周期より短い攻撃再生秒数。
        /// [KO] 다음 공격 주기보다 짧은 공격 재생 시간(초)입니다.
        /// [EN] Attack duration in seconds, shorter than the attack interval.
        /// </summary>
        [SerializeField] private float _attackSeconds = 0.64f;

        /// <summary>
        /// 姿勢確認で選んだ斬撃・発射の正規化位置。
        /// [KO] 자세를 확인해 정한 베기·발사의 정규화 위치입니다.
        /// [EN] Normalized strike or release position selected from inspected poses.
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
        /// 旧移動完了を拒否する世代。
        /// [KO] 이전 이동 완료를 거부하는 세대입니다.
        /// [EN] Generation rejecting stale movement completions.
        /// </summary>
        private int _generation;

        /// <summary>
        /// 現在の接続先の Run 識別子。
        /// [KO] 현재 연결된 Run 식별자입니다.
        /// [EN] Run identifier for the current binding.
        /// </summary>
        private Guid _runId;

        /// <summary>
        /// 解除後の演出要求を拒否する状態。
        /// [KO] 해제 후 연출 요청을 거부하는 상태입니다.
        /// [EN] State rejecting effects after release.
        /// </summary>
        private bool _isBound;

        /// <summary>
        /// 解除時に戻すモデルの元の姿勢。
        /// [KO] 해제 때 복원할 모델 원래 자세입니다.
        /// [EN] Model home transforms restored on release.
        /// </summary>
        private Vector3 _homePosition;

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
        /// Hit に切り替えず攻撃を保護する残り秒数。
        /// [KO] Hit로 전환하지 않고 공격을 보호할 남은 시간(초)입니다.
        /// [EN] Remaining protected attack seconds.
        /// </summary>
        private float _attackRemaining;

        /// <summary>
        /// 命中の色を残す秒数。
        /// [KO] 명중 색을 유지할 시간(초)입니다.
        /// [EN] Remaining hit-tint seconds.
        /// </summary>
        private float _hitRemaining;

        /// <summary>
        /// 攻撃移動の親として生成し、被弾の変位だけを加える表示用の軸。
        /// [KO] 공격 이동의 부모로 생성해 피격 변위만 더하는 표시용 축입니다.
        /// [EN] Visual pivot created above attack movement to add only hit displacement.
        /// </summary>
        private Transform _reactionRoot;

        /// <summary>
        /// 次の Update が進める押し戻しの残り秒数。
        /// [KO] 다음 Update가 진행할 밀림의 남은 시간(초)입니다.
        /// [EN] Recoil seconds remaining for the next update.
        /// </summary>
        private float _recoilRemaining;

        /// <summary>
        /// 連続被弾でも位置を飛ばさないよう保存した開始変位。
        /// [KO] 연속 피격에도 위치가 튀지 않도록 저장한 시작 변위입니다.
        /// [EN] Starting offset retained to keep repeated hits continuous.
        /// </summary>
        private Vector3 _recoilStart;

        /// <summary>
        /// 攻撃元と反対へ世界距離0.14だけ動く、親座標での目標変位。
        /// [KO] 공격 원점 반대로 월드 거리 0.14만큼 움직일 부모 좌표의 목표 변위입니다.
        /// [EN] Parent-space offset corresponding to a world-space push of 0.14 away from the attacker.
        /// </summary>
        private Vector3 _recoilTarget;

        /// <summary>
        /// 色を読むため借用する共通パレット。
        /// [KO] 색을 읽으려고 빌리는 공통 팔레트입니다.
        /// [EN] Borrowed shared effect palette.
        /// </summary>
        private GameColorPalette _colors;

        /// <summary>
        /// 原本材質を変えず既存 Renderer 設定を復元するキャッシュ。
        /// [KO] 원본 재질을 바꾸지 않고 기존 Renderer 설정을 복원할 캐시입니다.
        /// [EN] Cache restoring renderer properties without changing original materials.
        /// </summary>
        private Renderer[] _renderers;

        /// <summary>
        /// 被弾色を解除する時に Renderer ごとに復元する設定。
        /// [KO] 피격 색을 해제할 때 Renderer별로 복원할 설정입니다.
        /// [EN] Per-renderer settings restored after hit tint.
        /// </summary>
        private MaterialPropertyBlock[] _originalProperties;

        /// <summary>
        /// 被弾色から元の色へ戻すため開始時に読む色。
        /// [KO] 피격 색에서 원래 색으로 돌아가려고 시작 때 읽은 색입니다.
        /// [EN] Starting colors used to fade hit tint back to the originals.
        /// </summary>
        private Color[] _originalColors;

        /// <summary>
        /// 被弾色を書き込む時に使い回す設定容器。
        /// [KO] 피격 색을 기록할 때 재사용할 설정 컨테이너입니다.
        /// [EN] Reusable property block used while applying hit tint.
        /// </summary>
        private MaterialPropertyBlock _flashProperties;

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
        /// 自身の URP 材質と Controller のプロパティ番号。
        /// [KO] 자체 URP 재질과 Controller의 프로퍼티 번호입니다.
        /// [EN] Property IDs for the owned URP material and controller.
        /// </summary>
        private static readonly int _baseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 個体ごとの発光色を渡す URP プロパティ。
        /// [KO] 개체별 발광색을 전달할 URP 프로퍼티입니다.
        /// [EN] URP property receiving the per-instance emission color.
        /// </summary>
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// 通常の黒い発光マップを命中時だけ白へ切り替える番号。
        /// [KO] 평소 검정 발광 맵을 명중 때만 흰색으로 바꿀 번호입니다.
        /// [EN] Property switching the normally black emission map to white only on impact.
        /// </summary>
        private static readonly int _emissionMap = Shader.PropertyToID("_EmissionMap");

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
        /// 他ユニットを止めずこの一人が攻撃中か。
        /// [KO] 다른 유닛을 멈추지 않고 이 유닛만 공격 중인지 나타냅니다.
        /// [EN] Whether this unit is attacking independently.
        /// </summary>
        public bool IsAttacking => _attackRemaining > 0f;

        /// <summary>
        /// Presenter が予約に使う攻撃・発射・到着秒数。
        /// [KO] Presenter가 예약에 쓸 공격·발사·도착 시간(초)입니다.
        /// [EN] Attack, release and arrival seconds used by the presenter.
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
        public Vector3 ImpactPosition => _model.position + Vector3.up * (1.25f * transform.lossyScale.y);

        /// <summary>
        /// 元の姿勢・手・材質設定を一度記録します。
        /// [KO] 원래 자세·손·재질 설정을 한 번 기록합니다.
        /// [EN] Caches home transforms, hand and renderer properties once.
        /// </summary>
        private void Awake()
        {
            if (_animator == null || _model == null || _attackSeconds <= 0 || _releaseNormalized <= 0 ||
                _releaseNormalized >= 1 || _attackClipSeconds <= 0 || _flightSeconds < 0)
                throw new InvalidOperationException("Unit presentation references and timing must be valid.");
            _homePosition = _model.localPosition;
            _homeScale = _model.localScale;
            _homeRotation = _model.localRotation;
            // 攻撃 Tween は従来のモデルを動かし、被弾は新しい親だけを動かすため競合しない。
            _reactionRoot = new GameObject("Unit_HitReaction").transform;
            _reactionRoot.SetParent(_model.parent, false);
            _model.SetParent(_reactionRoot, false);
            _hand = _animator.GetBoneTransform(_usesLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            _renderers = _model.GetComponentsInChildren<Renderer>();
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
        /// EntryPoint が色を渡し、借用する効果を開始時に一度生成します。
        /// [KO] EntryPoint가 색을 전달하고 사용할 효과를 시작 시 한 번 생성합니다.
        /// [EN] EntryPoint supplies colors and creates owned effects once at startup.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
        {
            _colors = colors != null ? colors : throw new ArgumentNullException(nameof(colors));
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
        /// シーンのユニットの子へ生成し、開始直後の自動再生を残さず待機させます。
        /// [KO] 씬 유닛의 자식으로 생성하고 시작 직후 자동 재생을 남기지 않고 대기시킵니다.
        /// [EN] Creates an effect under the scene unit and parks it without leaving startup playback.
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
        /// 一人の攻撃を始め、対象へモデルだけを近づけます。他の攻撃を待ちません。
        /// [KO] 한 명의 공격을 시작해 모델만 대상에게 접근시키며 다른 공격을 기다리지 않습니다.
        /// [EN] Starts one attack and approaches using only the model, without waiting for other attackers.
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
            var direction = targetPosition - _model.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                _model.rotation = Quaternion.LookRotation(direction);
            }

            var generation = _generation;
            var sequence = DOTween.Sequence();
            _motion = sequence;
            if (isMelee)
            {
                // 前列の距離 3.6 なら、武器の間合い 1.35 を残す移動 2.25 を双方で半分ずつ使う。
                // 後列へは片方だけが近づくため全距離を使う。論理スロットは動かさない。
                var distance = Mathf.Max(0, direction.magnitude - 1.35f) * approachFraction;
                var destination = _model.position + direction.normalized * distance;
                var localDestination = _model.parent.InverseTransformPoint(destination);
                sequence.Append(_model.DOLocalMove(localDestination, ReleaseDelay * 0.82f).SetEase(Ease.InOutSine));
                sequence.AppendInterval(ReleaseDelay * 0.18f);
                sequence.Append(_model.DOLocalMove(_homePosition, _attackSeconds - ReleaseDelay).SetEase(Ease.OutSine));
            }
            else
            {
                sequence.Append(_model.DOScale(_homeScale * (isSpecial ? 1.06f : 1.025f), ReleaseDelay));
                sequence.Append(_model.DOScale(_homeScale, _attackSeconds - ReleaseDelay));
            }

            // この戦闘と世代の終了だけを受け、旧 Tween が死亡や新しい攻撃を上書きしないようにする。
            sequence.OnComplete(() =>
            {
                if (this == null || !_isBound || IsDead || generation != _generation || runId != _runId)
                {
                    return;
                }

                _model.localPosition = _homePosition;
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
                // 購入デモと同じ +Z を飛行前方にする。前回の尾は発射位置を変える前に消す。
                projectile.Play(start, targetPosition - start);
            }

            projectile.transform.position = Vector3.Lerp(start, targetPosition, progress) +
                                            Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.25f);
            var direction = targetPosition - start;
            if (direction.sqrMagnitude > 0.001f)
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
                _strike.Play(position + _model.forward * 0.3f, _model.forward);
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
            {
                return;
            }

            if (!IsAttacking)
            {
                Play("Hit");
            }

            _hitRemaining = HitFlashSeconds;
            var direction = _model.position - sourcePosition;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) direction = -_model.forward;
            // 世界距離0.14を親の縮尺で換算し、ユニットの大きさによらず同じ小幅にする。
            _recoilStart = _reactionRoot.localPosition;
            _recoilTarget = _reactionRoot.parent.InverseTransformVector(direction.normalized * 0.14f);
            _recoilRemaining = RecoilSeconds;
            RenderHitFlash(1f);
        }

        /// <summary>
        /// この一人の攻撃保護と被弾表示だけを毎フレーム進めます。
        /// [KO] 이 유닛의 공격 보호와 피격 표시만 매 프레임 진행합니다.
        /// [EN] Advances only this unit's attack protection and hit presentation each frame.
        /// </summary>
        private void Update()
        {
            _attackRemaining = Mathf.Max(0, _attackRemaining - Time.deltaTime);
            AdvanceHitReaction(Time.deltaTime);
        }

        /// <summary>
        /// 表示用の親を押して戻し、短い保持後に閃光を消します。戦闘スロットは動かしません。
        /// [KO] 표시용 부모를 밀었다가 되돌리고 짧은 유지 후 섬광을 지우며 전투 슬롯은 움직이지 않습니다.
        /// [EN] Pushes and returns the visual parent and fades the flash after a brief hold without moving battle slots.
        /// </summary>
        private void AdvanceHitReaction(float deltaSeconds)
        {
            if (_recoilRemaining > 0)
            {
                _recoilRemaining = Mathf.Max(0, _recoilRemaining - deltaSeconds);
                var elapsed = RecoilSeconds - _recoilRemaining;
                _reactionRoot.localPosition = elapsed < RecoilOutSeconds
                    ? Vector3.Lerp(_recoilStart, _recoilTarget, elapsed / RecoilOutSeconds)
                    : Vector3.Lerp(_recoilTarget, Vector3.zero,
                        (elapsed - RecoilOutSeconds) / (RecoilSeconds - RecoilOutSeconds));
            }

            if (_hitRemaining <= 0 || _colors == null)
            {
                return;
            }

            _hitRemaining = Mathf.Max(0, _hitRemaining - deltaSeconds);
            RenderHitFlash(Mathf.Clamp01(_hitRemaining / (HitFlashSeconds - HitFlashHoldSeconds)));
        }

        /// <summary>
        /// 共通パレットの明色を個体の PropertyBlock に渡し、共有材質は変更しません。
        /// [KO] 공통 팔레트의 밝은 색을 개체 PropertyBlock에 전달하고 공유 재질은 바꾸지 않습니다.
        /// [EN] Applies the shared palette's bright color through per-instance property blocks without changing shared materials.
        /// </summary>
        private void RenderHitFlash(float strength)
        {
            if (_colors == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (strength <= 0)
                {
                    _renderers[i].SetPropertyBlock(_originalProperties[i]);
                    continue;
                }

                _renderers[i].GetPropertyBlock(_flashProperties);
                _flashProperties.SetColor(_baseColor,
                    Color.Lerp(_originalColors[i], _colors.GetColor(Define.PaletteColor.OnAccent), strength * 0.65f));
                _flashProperties.SetTexture(_emissionMap, Texture2D.whiteTexture);
                _flashProperties.SetColor(_emissionColor,
                    _colors.GetColor(Define.PaletteColor.OnAccent) * (3f * strength));
                _renderers[i].SetPropertyBlock(_flashProperties);
            }
        }

        /// <summary>
        /// 命中位置で死亡を始め、復帰移動で死体を元の位置へ飛ばしません。
        /// [KO] 명중 위치에서 사망을 시작하며 복귀 이동으로 시체를 원래 위치로 날려 보내지 않습니다.
        /// [EN] Starts death at impact position without snapping the corpse home.
        /// </summary>
        public void ShowDeath(Guid runId)
        {
            if (!_isBound || IsDead || runId != _runId)
            {
                return;
            }

            StopMotion(false);
            // 死亡位置は維持し、帰還中の反応で死体が滑らないようにする。
            _recoilRemaining = 0;
            IsDead = true;
            Play("Death");
            if (_death != null)
            {
                _death.Play(_model.position + Vector3.up * (0.5f * transform.lossyScale.y), Vector3.forward);
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
        /// 所有する移動・投射体・色を解除し、次の戦闘へ古い表示を残しません。
        /// [KO] 소유 이동·투사체·색을 해제해 다음 전투에 이전 표시를 남기지 않습니다.
        /// [EN] Releases owned motion, projectiles and tint before another battle.
        /// </summary>
        public void Release()
        {
            _isBound = false;
            StopMotion();
            HideProjectiles(true);
            _hitRemaining = 0;
            _recoilRemaining = 0;
            if (_reactionRoot != null) _reactionRoot.localPosition = Vector3.zero;
            foreach (var impact in _impacts)
            {
                if (impact != null)
                {
                    impact.Clear();
                }
            }

            if (_strike != null) _strike.Clear();
            if (_death != null) _death.Clear();

            if (_renderers == null)
            {
                return;
            }

            for (var i = 0; i < _renderers.Length; i++)
                _renderers[i].SetPropertyBlock(_originalProperties[i]);
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

            _model.localPosition = _homePosition;
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
