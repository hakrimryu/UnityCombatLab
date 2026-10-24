using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 一人分の名前・HP バー・防御膜・被害数字を表示します。渡された値を描画し、戦闘の計算は行いません。
    /// [KO] 한 명의 이름·HP 바·방어막·피해 숫자를 표시합니다. 전달받은 값을 그리며 전투 계산은 하지 않습니다.
    /// [EN] Displays one unit’s name, health bar, shield and damage numbers using supplied values, without combat calculations.
    /// </summary>
    public sealed class UnitStatusView : MonoBehaviour
    {
        /// <summary>
        /// 被害表示を保持する秒数。
        /// [KO] 피해 표시 유지 시간(초).
        /// [EN] Damage label lifetime in seconds.
        /// </summary>
        private const float DamageLabelSeconds = 0.55f;

        /// <summary>
        /// 役割名の表示先。
        /// [KO] 역할 이름 표시 대상.
        /// [EN] Role label.
        /// </summary>
        [SerializeField] private TMP_Text _name;

        /// <summary>
        /// 正確な HP と死亡状態の表示先。
        /// [KO] 정확한 HP 및 사망 상태 표시 대상.
        /// [EN] Exact health and death label.
        /// </summary>
        [SerializeField] private TMP_Text _health;

        /// <summary>
        /// 防御膜の独立表示。
        /// [KO] 방어막의 독립 표시.
        /// [EN] Separate shield label.
        /// </summary>
        [SerializeField] private TMP_Text _shield;

        /// <summary>
        /// 割合だけを表示する HP バー。
        /// [KO] 비율만 표시하는 HP 바.
        /// [EN] Health ratio bar.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _fill;

        /// <summary>
        /// HP 損失と盾吸収を区別する短い表示。
        /// [KO] HP 손실과 방어막 흡수를 구분하는 짧은 표시.
        /// [EN] Brief label separating health loss and shield absorption.
        /// </summary>
        [SerializeField] private TMP_Text _damage;

        /// <summary>
        /// 最新の表示だけを消す所有 Tween。
        /// [KO] 최신 표시만 지우는 소유 Tween.
        /// [EN] Owned tween clearing only the latest label.
        /// </summary>
        private Tween _damageTween;

        /// <summary>
        /// 表示を消すたびに増える番号。古い番号の遅延処理が新しい被害数字を消さないために使います。
        /// [KO] 표시를 지울 때마다 증가하는 번호입니다. 이전 번호의 지연 처리가 새 피해 숫자를 지우지 못하게 합니다.
        /// [EN] Counter incremented when clearing a label so a delayed action with an old number cannot erase newer damage.
        /// </summary>
        private int _generation;

        /// <summary>
        /// 検証可能な表示 HP。
        /// [KO] 검증 가능한 표시 HP.
        /// [EN] Displayed health available for validation.
        /// </summary>
        public int DisplayedHealth { get; private set; }

        /// <summary>
        /// 検証可能な表示防御膜。
        /// [KO] 검증 가능한 표시 방어막.
        /// [EN] Displayed shield available for validation.
        /// </summary>
        public int DisplayedShield { get; private set; }

        /// <summary>
        /// 変更通知の検証に使う HP 更新回数。
        /// [KO] 변경 알림 검증에 쓰는 HP 갱신 횟수.
        /// [EN] Health update count for notification validation.
        /// </summary>
        public int HealthRenderCount { get; private set; }

        /// <summary>
        /// 再利用前に短命演出を除去します。
        /// [KO] 재사용 전에 짧은 연출을 제거합니다.
        /// [EN] Removes transient presentation before reuse.
        /// </summary>
        public void Initialize(string displayName, Color color)
        {
            if (_name == null || _health == null || _shield == null || _fill == null || _damage == null)
            {
                throw new InvalidOperationException("Unit status references must be assigned.");
            }

            ClearTransient();
            _name.text = displayName;
            _fill.color = color;
        }

        /// <summary>
        /// BattleHudPresenter が HP 変更時に呼び、数字とバーをすぐ更新します。HP 0 なら戦闘不能と表示します。
        /// [KO] HP 변경 시 BattleHudPresenter가 호출해 숫자와 바를 즉시 갱신합니다. HP가 0이면 전투 불능으로 표시합니다.
        /// [EN] Called by BattleHudPresenter on health changes to update the number and bar immediately, including defeat at zero.
        /// </summary>
        public void SetHealth(int health, int maxHealth)
        {
            DisplayedHealth = health;
            HealthRenderCount++;
            _health.text = health == 0 ? "戦闘不能  0 / " + maxHealth : health + " / " + maxHealth;
            // Image の fillAmount は 0〜1。例: HP 75/100 は 0.75 でバーを 75% 表示する。
            // float に変換しない整数除算では 75/100 が 0 になり、残 HP があってもバーが空になる。
            _fill.fillAmount = (float)health / maxHealth;
        }

        /// <summary>
        /// 盾の値を HP と区別して表示します。
        /// [KO] 방어막 값을 HP와 구분하여 표시합니다.
        /// [EN] Displays shield separately from health.
        /// </summary>
        public void SetShield(int shield)
        {
            DisplayedShield = shield;
            _shield.text = "シールド " + shield;
        }

        /// <summary>
        /// BattlePresenter が被害イベントで呼びます。HP 損失と防御膜吸収を別々に表示し、短時間後に消します。
        /// [KO] 피해 이벤트 때 BattlePresenter가 호출합니다. HP 손실과 방어막 흡수를 나눠 표시하고 잠시 후 지웁니다.
        /// [EN] Called by BattlePresenter on damage events to briefly display health loss separately from shield absorption.
        /// </summary>
        public void ShowDamage(int healthDamage, int shieldDamage)
        {
            ClearTransient();
            // 今回の表示番号を保存する。別の被害表示に切り替わったら番号が変わるため、古いタイマーは何もしない。
            var generation = _generation;
            _damage.text = healthDamage > 0 ? "−" + healthDamage : "";
            if (shieldDamage > 0)
            {
                _damage.text += "  シールド −" + shieldDamage;
            }

            _damageTween = DOVirtual.DelayedCall(DamageLabelSeconds, () =>
            {
                if (this != null && generation == _generation)
                {
                    _damage.text = "";
                    _damageTween = null;
                }
            }, false);
        }

        /// <summary>
        /// 所有する短命表示だけを解除します。
        /// [KO] 소유한 짧은 표시만 해제합니다.
        /// [EN] Clears only the transient presentation owned here.
        /// </summary>
        public void ClearTransient()
        {
            _generation++;
            _damageTween?.Kill();
            _damageTween = null;
            if (_damage != null)
            {
                _damage.text = "";
            }
        }

        /// <summary>
        /// 非表示化で遅延 callback も無効にします。
        /// [KO] 비활성화 시 지연 콜백도 무효화합니다.
        /// [EN] Invalidates delayed callbacks on disable.
        /// </summary>
        private void OnDisable()
        {
            ClearTransient();
        }
    }
}
