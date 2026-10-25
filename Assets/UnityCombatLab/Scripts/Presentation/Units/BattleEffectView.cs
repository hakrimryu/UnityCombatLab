using System;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// UnitView が指定した位置でエフェクトを再生し、再利用時に残った粒子を消します。
    /// [KO] UnitView가 지정한 위치에 효과를 재생하고 재사용 시 남은 입자를 지웁니다.
    /// [EN] Plays effects at positions supplied by UnitView and clears remaining particles before reuse.
    /// </summary>
    public sealed class BattleEffectView : MonoBehaviour
    {
        /// <summary>
        /// 再生と子の粒子の生存確認に使うルート ParticleSystem。
        /// [KO] 재생과 자식 입자의 생존 확인에 사용할 루트 ParticleSystem입니다.
        /// [EN] Root ParticleSystem used for playback and checking whether child particles are alive.
        /// </summary>
        private ParticleSystem _root;

        /// <summary>
        /// 到着時に先端とワールド空間の尾を区別して停止するための一覧。
        /// [KO] 도착 때 선두와 월드 공간의 꼬리를 구분해 멈출 목록입니다.
        /// [EN] Cached systems used to stop the head separately from world-space tails on arrival.
        /// </summary>
        private ParticleSystem[] _systems;

        /// <summary>
        /// 先端が飛行中かを示します。命中後に残る尾は含みません。
        /// [KO] 선두가 비행 중인지 나타내며 명중 후 남은 꼬리는 포함하지 않습니다.
        /// [EN] Whether the projectile head is in flight, excluding trails remaining after impact.
        /// </summary>
        public bool IsEmitting { get; private set; }

        /// <summary>
        /// 自身と子の ParticleSystem を取得します。
        /// [KO] 자신과 자식의 ParticleSystem을 가져옵니다.
        /// [EN] Collects this object's particle systems.
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
        /// 命中した先端を消し、ワールド空間の尾は残り寿命だけ再生します。
        /// [KO] 명중한 선두를 지우고 월드 공간의 꼬리는 남은 수명 동안 재생합니다.
        /// [EN] Clears the impacted projectile head and lets world-space trails finish their lifetime.
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
        /// 非表示時に、尾を含む全ての粒子を消します。
        /// [KO] 비활성화 시 꼬리를 포함한 모든 입자를 지웁니다.
        /// [EN] Clears all particles, including trails, when disabled.
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
