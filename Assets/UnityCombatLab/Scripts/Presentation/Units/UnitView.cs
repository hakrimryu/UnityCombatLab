using System;
using DG.Tweening;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// キャラクターの Animator と短い移動・拡大演出を動かします。攻撃が当たるかや被害量は決めません。
    /// [KO] 캐릭터의 Animator와 짧은 이동·확대 연출을 실행합니다. 공격 명중 여부나 피해량은 결정하지 않습니다.
    /// [EN] Drives character animation and brief movement or scaling effects without deciding hits or damage.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>
        /// 接近距離の Unity 単位。
        /// [KO] 접근 거리의 Unity 단위.
        /// [EN] Melee distance in Unity units.
        /// </summary>
        private const float MeleeDistanceUnits = 0.65f;

        /// <summary>
        /// 接近演出の秒数。
        /// [KO] 접근 연출 초.
        /// [EN] Approach duration in seconds.
        /// </summary>
        private const float ApproachSeconds = 0.12f;

        /// <summary>
        /// 復帰演出の秒数。
        /// [KO] 복귀 연출 초.
        /// [EN] Return duration in seconds.
        /// </summary>
        private const float ReturnSeconds = 0.18f;

        /// <summary>
        /// 特殊攻撃の強調倍率。
        /// [KO] 특수 공격 강조 배율.
        /// [EN] Special attack emphasis scale.
        /// </summary>
        private const float SpecialScale = 1.12f;

        /// <summary>
        /// 通常攻撃の強調倍率。
        /// [KO] 일반 공격 강조 배율.
        /// [EN] Normal attack emphasis scale.
        /// </summary>
        private const float NormalScale = 1.04f;

        /// <summary>
        /// 強調演出の秒数。
        /// [KO] 강조 연출 초.
        /// [EN] Emphasis duration in seconds.
        /// </summary>
        private const float EmphasisSeconds = 0.1f;

        /// <summary>
        /// 寸法復帰の秒数。
        /// [KO] 크기 복귀 초.
        /// [EN] Scale return duration in seconds.
        /// </summary>
        private const float ScaleReturnSeconds = 0.2f;

        /// <summary>
        /// 自身の wrapper が所有する Animator。
        /// [KO] 자체 wrapper가 소유하는 Animator.
        /// [EN] Animator owned by this wrapper.
        /// </summary>
        [SerializeField] private Animator _animator;

        /// <summary>
        /// 近接演出で動かすモデル。
        /// [KO] 근접 연출로 움직이는 모델.
        /// [EN] Model moved by melee presentation.
        /// </summary>
        [SerializeField] private Transform _model;

        /// <summary>
        /// スロットから動かさない原位置。
        /// [KO] 슬롯에서 이동시키지 않는 원래 위치.
        /// [EN] Original position relative to the fixed slot.
        /// </summary>
        private Vector3 _homePosition;

        /// <summary>
        /// 演出終了時に復元する原寸法。
        /// [KO] 연출 종료 시 복원하는 원래 크기.
        /// [EN] Original scale restored after presentation.
        /// </summary>
        private Vector3 _homeScale;

        /// <summary>
        /// この View が所有する接近演出。
        /// [KO] 이 View가 소유하는 접근 연출.
        /// [EN] Approach animation owned by this view.
        /// </summary>
        private Tween _motion;

        /// <summary>
        /// 演出を止めるたびに増える番号。古い演出の完了処理が新しい動きを元に戻さないように確認します。
        /// [KO] 연출을 멈출 때마다 증가하는 번호입니다. 이전 연출의 완료 처리가 새 움직임을 되돌리지 않도록 확인합니다.
        /// [EN] Counter incremented when stopping an effect so an old completion cannot reset a newer movement.
        /// </summary>
        private int _generation;

        /// <summary>
        /// 現在の表示対象の挑戦識別子。
        /// [KO] 현재 표시 대상의 도전 식별자.
        /// [EN] Run identifier currently being presented.
        /// </summary>
        private Guid _runId;

        /// <summary>
        /// 現在のバインドが生きているか。
        /// [KO] 현재 바인딩이 유효한지 나타냅니다.
        /// [EN] Whether the current binding is live.
        /// </summary>
        private bool _isBound;

        /// <summary>
        /// 死亡後の攻撃・復帰を拒否します。
        /// [KO] 사망 후 공격 및 복귀를 거부합니다.
        /// [EN] Rejects attacks and idle returns after death.
        /// </summary>
        public bool IsDead { get; private set; }

        /// <summary>
        /// 最後に受け取った表現名。
        /// [KO] 마지막으로 전달받은 표현 이름.
        /// [EN] Last requested presentation state.
        /// </summary>
        public string CurrentAnimation { get; private set; }

        /// <summary>
        /// バインド状態を検証へ公開します。
        /// [KO] 검증을 위해 바인딩 상태를 공개합니다.
        /// [EN] Exposes binding state for validation.
        /// </summary>
        public bool IsBound => _isBound;

        /// <summary>
        /// 参照を検証して原位置を保存します。
        /// [KO] 참조를 검증하고 원래 위치를 저장합니다.
        /// [EN] Validates references and stores the original pose.
        /// </summary>
        private void Awake()
        {
            if (_animator == null || _model == null)
            {
                throw new InvalidOperationException("Unit model and Animator must be assigned.");
            }

            _homePosition = _model.localPosition;
            _homeScale = _model.localScale;
        }

        /// <summary>
        /// BattlePresenter が開始時に呼びます。前の演出を止め、今回の RunId を記録して Idle に戻します。
        /// [KO] 시작 시 BattlePresenter가 호출합니다. 이전 연출을 멈추고 이번 RunId를 기록한 뒤 Idle로 되돌립니다.
        /// [EN] Called by BattlePresenter at startup to stop old effects, store the new RunId and return to Idle.
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
        /// 攻撃通知で呼ばれ、近接なら相手方向へ踏み込み、遠隔・全体攻撃なら一時的に拡大して攻撃を強調します。
        /// [KO] 공격 알림으로 호출됩니다. 근접이면 상대 방향으로 전진하고, 원거리·전체 공격이면 잠깐 확대해 강조합니다.
        /// [EN] On an attack notification, lunges toward the target for melee or briefly scales up for ranged and area attacks.
        /// </summary>
        public void ShowAttack(Guid runId, Vector3 targetPosition, bool isMelee, bool isSpecial)
        {
            if (!_isBound || IsDead || runId != _runId)
            {
                return;
            }

            StopMotion();
            Play("Attack");
            var generation = _generation;
            var sequence = DOTween.Sequence();
            _motion = sequence;
            if (isMelee)
            {
                // 相手へのワールド方向をキャラクター基準の方向へ変換し、上下方向を除いて床に沿って動かす。
                // 相手までの距離ではなく固定 0.65 単位だけ踏み込むので、元の配置枠は変わらない。
                var direction = transform.InverseTransformDirection(targetPosition - transform.position);
                direction.y = 0;
                var destination = _homePosition + direction.normalized * MeleeDistanceUnits;
                sequence.Append(_model.DOLocalMove(destination, ApproachSeconds));
                sequence.Append(_model.DOLocalMove(_homePosition, ReturnSeconds));
            }
            else
            {
                // 通常は元の大きさの 1.04 倍（4%増）、特殊攻撃は 1.12 倍（12%増）にしてから戻す。
                sequence.Append(_model.DOScale(_homeScale * (isSpecial ? SpecialScale : NormalScale), EmphasisSeconds));
                sequence.Append(_model.DOScale(_homeScale, ScaleReturnSeconds));
            }

            // この演出が終わった時だけ元の位置・大きさに戻す。
            // 途中で死亡・再バインド・別の演出が起きたなら番号や RunId が違うので、古い完了処理を無視する。
            sequence.OnComplete(() =>
            {
                if (this == null || !_isBound || IsDead || generation != _generation || runId != _runId)
                {
                    return;
                }

                _model.localPosition = _homePosition;
                _model.localScale = _homeScale;
                _motion = null;
            });
        }

        /// <summary>
        /// 生存中の被弾だけを表示します。
        /// [KO] 생존 중인 피격만 표시합니다.
        /// [EN] Shows hits only while alive.
        /// </summary>
        public void ShowHit(Guid runId)
        {
            if (_isBound && !IsDead && runId == _runId)
            {
                Play("Hit");
            }
        }

        /// <summary>
        /// 死亡を最優先し古い接近を終了します。
        /// [KO] 사망을 우선하고 이전 접근을 종료합니다.
        /// [EN] Prioritizes death and stops previous movement.
        /// </summary>
        public void ShowDeath(Guid runId)
        {
            if (!_isBound || IsDead || runId != _runId)
            {
                return;
            }

            StopMotion();
            IsDead = true;
            Play("Death");
        }

        /// <summary>
        /// 死亡姿勢を保ち、生存者の接近だけを終了します。
        /// [KO] 사망 자세를 유지하고 생존자의 접근만 종료합니다.
        /// [EN] Preserves death poses and ends survivors' approaches.
        /// </summary>
        public void FinishBattle()
        {
            StopMotion();
            if (_isBound && !IsDead)
            {
                Play("Idle");
            }
        }

        /// <summary>
        /// BattlePresenter.Dispose または OnDisable が呼びます。以後の演出要求を断り、動きと大きさを元へ戻します。
        /// [KO] BattlePresenter.Dispose 또는 OnDisable에서 호출합니다. 이후 연출 요청을 막고 위치·크기를 원래대로 되돌립니다.
        /// [EN] Called by BattlePresenter.Dispose or OnDisable to reject further effects and restore position and scale.
        /// </summary>
        public void Release()
        {
            _isBound = false;
            StopMotion();
        }

        /// <summary>
        /// 所有 Tween のみを中断しモデルを復元します。
        /// [KO] 소유한 Tween만 중단하고 모델을 복원합니다.
        /// [EN] Stops only the owned tween and restores the model.
        /// </summary>
        private void StopMotion()
        {
            _generation++;
            _motion?.Kill();
            _motion = null;
            if (_model != null)
            {
                _model.localPosition = _homePosition;
                _model.localScale = _homeScale;
            }
        }

        /// <summary>
        /// 状態名は自身の最小 Animator と一致させます。
        /// [KO] 상태 이름을 자체 최소 Animator와 일치시킵니다.
        /// [EN] Uses state names matching the owned minimal Animator.
        /// </summary>
        private void Play(string state)
        {
            CurrentAnimation = state;
            _animator.Play(state, 0, 0);
        }

        /// <summary>
        /// 無効化でも遅延演出を残しません。
        /// [KO] 비활성화 시에도 지연 연출을 남기지 않습니다.
        /// [EN] Leaves no delayed presentation on disable.
        /// </summary>
        private void OnDisable()
        {
            Release();
        }
    }
}
