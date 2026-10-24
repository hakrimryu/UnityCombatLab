using System;
using DG.Tweening;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
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
        /// HP バーの塗り部分。Initialize が陣営色を適用します。
        /// [KO] HP 바의 채움 부분입니다. Initialize가 팀 색상을 적용합니다.
        /// [EN] Health-bar fill image whose team color is set by Initialize.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _fill;

        /// <summary>
        /// 購入プレハブの表示専用 Slider。SetHealth が HP 比率を渡し、枠を変形せず塗り幅だけを変えます。
        /// [KO] 구매 프리팹의 표시 전용 Slider입니다. SetHealth가 HP 비율을 전달해 테두리 변형 없이 채움 너비만 바꿉니다.
        /// [EN] Display-only slider from the purchased prefab; SetHealth supplies the HP ratio to resize its fill without distorting the border.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Slider _healthBar;

        /// <summary>
        /// HP 損失と盾吸収を区別する短い表示。
        /// [KO] HP 손실과 방어막 흡수를 구분하는 짧은 표시.
        /// [EN] Brief label separating health loss and shield absorption.
        /// </summary>
        [SerializeField] private TMP_Text _damage;

        /// <summary>
        /// HP 損失と同時に発生しても別の行で読める、防御膜吸収の数字。
        /// [KO] HP 손실과 동시에 발생해도 별도 줄로 읽을 수 있는 방어막 흡수 수치입니다.
        /// [EN] Shield absorption number on a separate line, readable alongside simultaneous HP loss.
        /// </summary>
        [SerializeField] private TMP_Text _shieldDamage;

        /// <summary>
        /// 防御膜吸収の数字に添える購入スプライト。数字と一緒に表示・解除します。
        /// [KO] 방어막 흡수 수치 옆의 구매 스프라이트입니다. 수치와 함께 표시·해제합니다.
        /// [EN] Purchased icon beside the absorption number, shown and cleared with that number.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _shieldDamageIcon;

        /// <summary>
        /// 現在の防御膜の数字に添えるアイコン。戦闘不能時は隠します。
        /// [KO] 현재 방어막 수치 옆의 아이콘입니다. 전투 불능이면 숨깁니다.
        /// [EN] Icon beside the current shield value, hidden when defeated.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _shieldIcon;

        /// <summary>
        /// 名前の左に置く役割アイコンの背景。陣営色を使い、死亡時は非活性色にします。
        /// [KO] 이름 왼쪽 역할 아이콘 배경입니다. 팀 색상을 쓰며 사망 시 비활성 색으로 바꿉니다.
        /// [EN] Role-icon background beside the name; uses the team color and the muted color on death.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _badge;

        /// <summary>
        /// HP 0 を色だけでなく「戦闘不能」と伝える表示先。
        /// [KO] HP 0을 색뿐 아니라 '전투 불능' 문구로 알리는 표시 대상입니다.
        /// [EN] Label explicitly identifying defeat at zero HP rather than relying on color alone.
        /// </summary>
        [SerializeField] private TMP_Text _defeated;

        /// <summary>
        /// 再バインド時にも生存色へ戻せるよう Initialize で記録する陣営色。
        /// [KO] 재바인딩 시 생존 색으로 복원하도록 Initialize에서 저장하는 팀 색입니다.
        /// [EN] Team color recorded by Initialize so rebinding can restore the living appearance.
        /// </summary>
        private Color _teamColor;

        /// <summary>
        /// BattleHudView が通知開始前に渡す共通設定。HP と防御膜の状態色もここから読みます。
        /// [KO] BattleHudView가 알림 연결 전에 전달하는 공통 설정입니다. HP와 방어막 상태 색도 여기에서 읽습니다.
        /// [EN] Shared settings supplied by BattleHudView before subscriptions, including health and shield state colors.
        /// </summary>
        private GameColorPalette _colors;

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
        public void Initialize(string displayName, Define.BattleTeam team)
        {
            if (_name == null || _health == null || _shield == null || _fill == null || _healthBar == null ||
                _damage == null ||
                _shieldDamage == null || _shieldDamageIcon == null || _shieldIcon == null ||
                _badge == null || _defeated == null || _colors == null)
            {
                throw new InvalidOperationException("Unit status references must be assigned.");
            }

            ClearTransient();
            _name.text = displayName;
            _teamColor = _colors.GetTeamColor(team);
            _fill.color = _teamColor;
            _badge.color = _teamColor;
        }

        /// <summary>
        /// BattleHudView が開始時に色設定と HUD 所有の共有材質を渡します。購入材質は変更しません。
        /// [KO] BattleHudView가 시작 시 색상 설정과 HUD 소유 공유 재질을 전달합니다. 구매 재질은 수정하지 않습니다.
        /// [EN] BattleHudView supplies shared colors and its owned damage material at startup, leaving purchased materials untouched.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors, Material damageMaterial)
        {
            _colors = colors != null ? colors : throw new ArgumentNullException(nameof(colors));
            if (_damage == null || _shieldDamage == null || damageMaterial == null)
            {
                throw new InvalidOperationException("Damage labels and their HUD material must be assigned.");
            }

            _damage.fontSharedMaterial = damageMaterial;
            _shieldDamage.fontSharedMaterial = damageMaterial;
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
            _health.text = health + " / " + maxHealth;
            _defeated.gameObject.SetActive(health == 0);
            _shield.gameObject.SetActive(health > 0);
            _shieldIcon.gameObject.SetActive(health > 0);
            _badge.color = health == 0 ? _colors.GetColor(Define.PaletteColor.MutedText) : _teamColor;
            _name.color = _colors.GetColor(health == 0 ? Define.PaletteColor.MutedText : Define.PaletteColor.Text);
            // Slider の範囲は 0〜1。例: HP 75/100 は 0.75 で塗り幅を 75% にする。
            // float に変換しない整数除算では 75/100 が 0 になり、残 HP があってもバーが空になる。
            // 表示専用なので値変更イベントは送らず、ReadModel への逆向きの入力を作らない。
            _healthBar.SetValueWithoutNotify((float)health / maxHealth);
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
            _shield.color = _colors.GetColor(shield > 0 ? Define.PaletteColor.Shield : Define.PaletteColor.MutedText);
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
            _damage.text = healthDamage > 0 ? "-" + healthDamage : "";
            _shieldDamage.text = shieldDamage > 0 ? "-" + shieldDamage : "";
            _shieldDamageIcon.gameObject.SetActive(shieldDamage > 0);

            _damageTween = DOVirtual.DelayedCall(DamageLabelSeconds, () =>
            {
                if (this != null && generation == _generation)
                {
                    _damage.text = "";
                    _shieldDamage.text = "";
                    _shieldDamageIcon.gameObject.SetActive(false);
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

            if (_shieldDamage != null)
            {
                _shieldDamage.text = "";
            }

            if (_shieldDamageIcon != null)
            {
                _shieldDamageIcon.gameObject.SetActive(false);
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
