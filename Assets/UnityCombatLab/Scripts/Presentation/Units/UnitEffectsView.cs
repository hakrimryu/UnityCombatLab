using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// 一人の投射物・命中・剣撃・死亡効果を生成して使い回し、UnitViewの解除時に全て停止します。
    /// [KO] 한 유닛의 투사체·명중·검격·사망 효과를 생성해 재사용하고 UnitView 해제 시 모두 멈춥니다.
    /// [EN] Creates and reuses one unit's projectiles, impacts, blade and death effects, clearing them when UnitView releases.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitEffectsView : MonoBehaviour
    {
        /// <summary>
        /// 飛行の中央で投射物を持ち上げるワールド高さ。
        /// [KO] 비행 중간에서 투사체를 들어 올리는 월드 높이입니다.
        /// [EN] World-space projectile height at the middle of its arc.
        /// </summary>
        private const float ProjectileArcHeightWorldUnits = 0.25f;

        /// <summary>
        /// 開始時に三個作り広域攻撃にも使い回す投射体。
        /// [KO] 시작 시 세 개를 만들어 광역 공격에도 재사용할 투사체입니다.
        /// [EN] Projectile instantiated three times at startup for single and area attacks.
        /// </summary>
        [SerializeField] private BattleEffectView _projectilePrefab;

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
        /// 生成済みと後続生成の効果が共有する戦闘速度。
        /// [KO] 이미 생성했거나 이후 생성할 효과가 공유하는 전투 속도입니다.
        /// [EN] Combat speed shared by existing and subsequently created effects.
        /// </summary>
        private float _playbackSpeed = 1f;

        /// <summary>
        /// EntryPointからUnitView経由で接続前に呼び、効果を一度準備します。再呼出しでは既存効果を再利用します。
        /// [KO] EntryPoint가 UnitView를 거쳐 연결 전에 호출하여 효과를 한 번 준비하며 재호출 시 기존 효과를 재사용합니다.
        /// [EN] Called before binding by EntryPoint through UnitView to prepare effects once and reuse them on subsequent calls.
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
            effect.SetPlaybackSpeed(_playbackSpeed);
            return effect;
        }

        /// <summary>
        /// このユニットが所有する全効果と後続生成へ戦闘速度を適用します。
        /// [KO] 이 유닛이 소유하는 모든 효과와 후속 생성에 전투 속도를 적용합니다.
        /// [EN] Applies combat speed to all effects owned by this unit and future instances.
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            foreach (var effect in _projectiles)
                if (effect != null)
                    effect.SetPlaybackSpeed(speed);

            foreach (var effect in _impacts)
                if (effect != null)
                    effect.SetPlaybackSpeed(speed);

            if (_strike != null)
                _strike.SetPlaybackSpeed(speed);

            if (_death != null)
                _death.SetPlaybackSpeed(speed);
        }

        /// <summary>
        /// 発射から到着まで、同じ対象の現在位置へ矢・魔法弾を進めます。
        /// [KO] 발사부터 도착까지 같은 대상의 현재 위치로 화살·마법탄을 이동합니다.
        /// [EN] Advances an arrow or bolt toward the current position of its target.
        /// </summary>
        public void PresentProjectile(int slot, Vector3 releasePosition, Vector3 targetPosition, float progress)
        {
            if (_projectiles[slot] == null)
            {
                return;
            }

            var projectile = _projectiles[slot];
            var start = _projectileStarts[slot];
            if (!projectile.IsEmitting)
            {
                start = releasePosition;
                _projectileStarts[slot] = start;
                // 飛行方向を +Z に向けて再生する。発射位置を変える前に前回の尾を消す。
                projectile.Play(start, targetPosition - start);
            }

            projectile.transform.position = Vector3.Lerp(start, targetPosition, progress) +
                                            Vector3.up *
                                            (Mathf.Sin(progress * Mathf.PI) * ProjectileArcHeightWorldUnits);
            var direction = targetPosition - start;
            if (direction.sqrMagnitude > Define.MinimumDirectionSqrMagnitude)
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
        /// UnitView が命中時に呼び、この攻撃者の効果を対象の胴体位置に再生します。
        /// [KO] UnitView가 명중 때 호출해 이 공격자의 효과를 대상 몸통 위치에 재생합니다.
        /// [EN] Called by UnitView at impact to play this attacker's effect at the target's torso.
        /// </summary>
        public void ShowImpact(int slot, Vector3 position)
        {
            // 対象の前列0・後列上1・後列下2で効果を分け、広域攻撃でも別の対象の効果を消さない。
            if (_impacts[slot] != null)
            {
                _impacts[slot].Play(position, Vector3.forward);
            }
        }

        /// <summary>
        /// UnitView が一攻撃につき一度、斬撃の命中時に剣先効果を開始します。
        /// [KO] UnitView가 공격당 한 번 베기 명중 때 검 끝 효과를 시작합니다.
        /// [EN] Called once per attack by UnitView to start a blade effect at melee impact.
        /// </summary>
        public void ShowStrike(Vector3 position, Vector3 forward)
        {
            if (_strike != null)
            {
                _strike.Play(position, forward);
            }
        }

        /// <summary>
        /// UnitViewが確定した死亡位置で魂の効果を再生します。
        /// [KO] UnitView가 확정한 사망 위치에 영혼 효과를 재생합니다.
        /// [EN] Plays the soul effect at the death position supplied by UnitView.
        /// </summary>
        public void ShowDeath(Vector3 position)
        {
            if (_death != null)
                _death.Play(position, Vector3.forward);
        }

        /// <summary>
        /// 戦闘解除や非表示時に、投射物の尾を含む全効果を消して次の使用に備えます。
        /// [KO] 전투 해제나 비활성화 시 투사체 꼬리를 포함한 모든 효과를 지우고 다음 사용을 준비합니다.
        /// [EN] Clears all effects, including projectile trails, on battle release or disable so they can be reused.
        /// </summary>
        public void Clear()
        {
            HideProjectiles(true);
            foreach (var impact in _impacts)
                if (impact != null)
                    impact.Clear();
            if (_strike != null)
                _strike.Clear();
            if (_death != null)
                _death.Clear();
        }

        /// <summary>
        /// シーンの終了でも残った粒子を停止します。
        /// [KO] 씬이 종료될 때도 남은 입자를 정지합니다.
        /// [EN] Stops remaining particles when the scene disables the unit.
        /// </summary>
        private void OnDisable()
            => Clear();
    }
}
