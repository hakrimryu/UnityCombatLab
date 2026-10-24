using System;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// UnitView が借りたエフェクトを再生し、古い粒子を次の攻撃へ残さず再利用します。
    /// [KO] UnitView가 가져온 효과를 재생하고 이전 입자를 다음 공격에 남기지 않도록 재사용합니다.
    /// [EN] Plays UnitView-owned effects and reuses them without carrying old particles into another attack.
    /// </summary>
    public sealed class BattleEffectView : MonoBehaviour
    {
        /// <summary>
        /// 原本の階層を保った再生元。子の放出・生存確認もこの ParticleSystem から行います。
        /// [KO] 원본 계층을 유지한 재생 원본이며 자식 방출·생존 확인도 이 ParticleSystem에서 수행합니다.
        /// [EN] Root particle system used to play and check its original child hierarchy.
        /// </summary>
        private ParticleSystem _root;

        /// <summary>
        /// 到着時に先端とワールド空間の尾を区別して停止するための一覧。
        /// [KO] 도착 때 선두와 월드 공간의 꼬리를 구분해 멈출 목록입니다.
        /// [EN] Cached systems used to stop the head separately from world-space tails on arrival.
        /// </summary>
        private ParticleSystem[] _systems;

        /// <summary>
        /// UnitView が同じ飛行を移動中かを判断する状態。尾の残存とは別です。
        /// [KO] UnitView가 같은 비행을 이동 중인지 판단할 상태이며 꼬리 잔존과는 별개입니다.
        /// [EN] State used by UnitView to continue a flight independently of fading tail particles.
        /// </summary>
        public bool IsEmitting { get; private set; }

        /// <summary>
        /// 自身の粒子を一度取得します。購入原本のスクリプトや物理判定には依存しません。
        /// [KO] 자체 입자를 한 번 가져오며 구매 원본 스크립트나 물리 판정에는 의존하지 않습니다.
        /// [EN] Caches owned particles without depending on vendor scripts or collision decisions.
        /// </summary>
        private void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            if (_systems.Length == 0)
            {
                throw new InvalidOperationException("A battle effect requires a particle system.");
            }

            _root = _systems[0];
        }

        /// <summary>
        /// 古い粒子を消して指定した世界位置・前方向で再生します。子の原本回転は保持します。
        /// [KO] 이전 입자를 지우고 지정 월드 위치·전방으로 재생하며 자식의 원본 회전은 유지합니다.
        /// [EN] Clears old particles and plays at the supplied world position and forward direction, preserving child rotation.
        /// </summary>
        public void Play(Vector3 position, Vector3 forward)
        {
            gameObject.SetActive(true);
            _root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            transform.SetPositionAndRotation(position,
                forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity);
            IsEmitting = true;
            _root.Play(true);
        }

        /// <summary>
        /// 命中時に先端を消し、通過した場所の尾だけを自然に消します。被害判定は呼びません。
        /// [KO] 명중 때 선두를 없애고 지나온 위치의 꼬리만 자연스럽게 지우며 피해 판정은 호출하지 않습니다.
        /// [EN] Removes the head at impact and lets world-space tails fade without invoking combat decisions.
        /// </summary>
        public void StopFlight()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            IsEmitting = false;
            foreach (var system in _systems)
            {
                system.Stop(false, system.main.simulationSpace == ParticleSystemSimulationSpace.World
                    ? ParticleSystemStopBehavior.StopEmitting
                    : ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// 戦闘解除時に UnitView が呼び、尾を含む全粒子を即時解除します。
        /// [KO] 전투 해제 때 UnitView가 호출하여 꼬리를 포함한 모든 입자를 즉시 해제합니다.
        /// [EN] Called by UnitView on battle release to immediately clear all particles including tails.
        /// </summary>
        public void Clear()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 放出も子の尾も終了した表示だけを隠し、次の再生まで更新を止めます。
        /// [KO] 방출과 자식 꼬리까지 끝난 표시만 숨기고 다음 재생까지 갱신을 멈춥니다.
        /// [EN] Hides effects after emission and child tails finish, stopping updates until reuse.
        /// </summary>
        private void Update()
        {
            if (!_root.IsAlive(true))
            {
                Clear();
            }
        }

        /// <summary>
        /// 非表示・シーン退場のどちらでも古いワールド空間の粒子を残しません。
        /// [KO] 비활성화·씬 퇴장 모두 이전 월드 공간 입자를 남기지 않습니다.
        /// [EN] Clears old world-space particles on both hiding and scene exit.
        /// </summary>
        private void OnDisable()
        {
            IsEmitting = false;
            if (_root != null)
            {
                _root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
