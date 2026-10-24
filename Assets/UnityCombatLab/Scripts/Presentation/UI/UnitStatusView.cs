using System;
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
        /// 現在の表示位置から確定 HP まで等速で動かす秒数。
        /// [KO] 현재 표시 위치에서 확정 HP까지 일정 속도로 움직이는 시간(초)입니다.
        /// [EN] Seconds for linear movement from the current fill to confirmed health.
        /// </summary>
        private const float HealthLerpSeconds = 0.24f;

        /// <summary>
        /// 被害通知から直前 HP の追従を始めるまでの秒数。
        /// [KO] 피해 알림부터 직전 HP의 추적을 시작할 때까지의 시간(초)입니다.
        /// [EN] Seconds after a damage notification before the old-health trail starts moving.
        /// </summary>
        private const float TrailingDelaySeconds = 0.4f;

        /// <summary>
        /// 残した直前 HP を確定 HP まで縮める秒数。
        /// [KO] 남겨 둔 직전 HP를 확정 HP까지 줄이는 시간(초)입니다.
        /// [EN] Seconds for the retained old-health trail to reach confirmed health.
        /// </summary>
        private const float TrailingLerpSeconds = 0.35f;

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
        /// 確定 HP から現在の塗り位置まで、これから減る範囲を重ねる画像。
        /// [KO] 확정 HP에서 현재 채움 위치까지 곧 줄어들 구간을 덮어 표시하는 이미지입니다.
        /// [EN] Overlay highlighting the upcoming visual loss between confirmed health and the current fill.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _damagePreview;

        /// <summary>
        /// 本体の背面に直前 HP を残し、遅れて縮む画像。
        /// [KO] 본 체력바 뒤에 직전 HP를 남기고 늦게 줄어드는 이미지입니다.
        /// [EN] Image behind the main fill that retains old health before shrinking later.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _trailingHealth;

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
        /// 被害数字の三枠と終了処理を所有する表示コンポーネント。
        /// [KO] 피해 숫자 세 슬롯과 종료 처리를 소유한 표시 컴포넌트입니다.
        /// [EN] Display component owning the three damage-number slots and their expiry.
        /// </summary>
        [SerializeField] private DamageNumberView _damageNumbers;

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
        /// 最初の HP 通知を受けたか。Initialize は解除し、初期値を補間せず表示します。
        /// [KO] 첫 HP 알림 수신 여부입니다. Initialize가 해제하여 초기값을 보간 없이 표시합니다.
        /// [EN] Whether initial health arrived; Initialize clears this so initial values appear without interpolation.
        /// </summary>
        private bool _hasHealth;

        /// <summary>
        /// Update がこの View のゲージを動かしている間だけ true。
        /// [KO] Update가 이 View의 게이지를 움직이는 동안만 true입니다.
        /// [EN] True only while Update is animating this view's gauges.
        /// </summary>
        private bool _isHealthAnimating;

        /// <summary>
        /// 最新通知が HP 損失か。回復時には予告と残像を表示しません。
        /// [KO] 최신 알림이 HP 손실인지 여부입니다. 회복 시 예고와 잔상을 표시하지 않습니다.
        /// [EN] Whether the latest notification lost health; healing shows neither preview nor trail.
        /// </summary>
        private bool _isHealthDecreasing;

        /// <summary>
        /// 最新通知からの経過秒。毎回の補間は固定した開始値から計算します。
        /// [KO] 최신 알림부터의 경과 시간(초)입니다. 보간은 고정한 시작값에서 계산합니다.
        /// [EN] Seconds since the latest notification; interpolation always uses a fixed starting value.
        /// </summary>
        private float _healthElapsed;

        /// <summary>
        /// 新通知時点の本体ゲージ比率。連続被害でも表示を巻き戻しません。
        /// [KO] 새 알림 시점 본 게이지 비율입니다. 연속 피해에도 표시를 되돌리지 않습니다.
        /// [EN] Main-fill ratio when a notification arrives, preserving continuity through repeated hits.
        /// </summary>
        private float _healthStart;

        /// <summary>
        /// ReadModel の確定 HP 比率。表示の遅れを戦闘状態へ戻しません。
        /// [KO] ReadModel의 확정 HP 비율입니다. 표시 지연을 전투 상태로 되돌리지 않습니다.
        /// [EN] Confirmed ReadModel health ratio; presentation delay never feeds back into combat state.
        /// </summary>
        private float _healthTarget;

        /// <summary>
        /// 最新通知時点の残像比率。連続被害では既に残っている範囲を維持します。
        /// [KO] 최신 알림 시점 잔상 비율입니다. 연속 피해에는 이미 남아 있는 구간을 유지합니다.
        /// [EN] Trail ratio at the latest notification, preserving an existing trail through consecutive damage.
        /// </summary>
        private float _trailingStart;

        /// <summary>
        /// 前回の最大 HP。別の最大値へ切り替わる時は古い比率を引き継ぎません。
        /// [KO] 이전 최대 HP입니다. 최대값이 바뀌면 이전 비율을 이어받지 않습니다.
        /// [EN] Previous maximum health; a changed maximum resets old ratios.
        /// </summary>
        private int _maxHealth;

        /// <summary>
        /// 未到着の損失も含めた予告先。数字と本体の命中通知とは別に受け取ります。
        /// [KO] 미도착 손실까지 포함한 예고 목표이며 숫자·본체의 명중 알림과 별도로 받습니다.
        /// [EN] Preview target including pending hits, received separately from arrived health.
        /// </summary>
        private float _previewTarget = 1f;


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
                _damageNumbers == null || _shieldIcon == null ||
                _badge == null || _defeated == null || _colors == null ||
                _damagePreview == null || _trailingHealth == null)
            {
                throw new InvalidOperationException("Unit status references must be assigned.");
            }

            ClearTransient();
            _hasHealth = false;
            _previewTarget = 1f;
            _name.text = displayName;
            _teamColor = _colors.GetTeamColor(team);
            _fill.color = _teamColor;
            _badge.color = _teamColor;
            _damagePreview.color = _colors.GetColor(Define.PaletteColor.DamagePreview);
            _trailingHealth.color = _colors.GetColor(Define.PaletteColor.TrailingHealth);
        }

        /// <summary>
        /// BattleHudView が開始時に色設定と HUD 所有の共有材質を渡します。購入材質は変更しません。
        /// [KO] BattleHudView가 시작 시 색상 설정과 HUD 소유 공유 재질을 전달합니다. 구매 재질은 수정하지 않습니다.
        /// [EN] BattleHudView supplies shared colors and its owned damage material at startup, leaving purchased materials untouched.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors, Material damageMaterial)
        {
            _colors = colors != null ? colors : throw new ArgumentNullException(nameof(colors));
            if (_damageNumbers == null)
                throw new InvalidOperationException("Damage number view must be assigned.");
            _damageNumbers.ApplyMaterial(damageMaterial);
        }

        /// <summary>
        /// BattleHudPresenter が命中済み HP を渡します。数字と死亡表示を変え、命中時は追加待機なしで補間します。
        /// [KO] BattleHudPresenter가 명중한 HP를 전달합니다. 숫자·사망 표시를 바꾸고 명중 때는 추가 대기 없이 보간합니다.
        /// [EN] Receives arrived health from BattleHudPresenter, updating numbers and defeat while starting interpolation without another impact delay.
        /// </summary>
        public void SetHealth(int health, int maxHealth)
        {
            if (maxHealth <= 0 || health < 0 || health > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health), "Health must fit a positive maximum.");
            }

            var previousHealth = DisplayedHealth;
            DisplayedHealth = health;
            HealthRenderCount++;
            _health.text = health + " / " + maxHealth;
            _defeated.gameObject.SetActive(health == 0);
            _shield.gameObject.SetActive(health > 0);
            _shieldIcon.gameObject.SetActive(health > 0);
            _badge.color = health == 0 ? _colors.GetColor(Define.PaletteColor.MutedText) : _teamColor;
            _name.color = _colors.GetColor(health == 0 ? Define.PaletteColor.MutedText : Define.PaletteColor.Text);
            // HP 75/100 → 0.75。数字と死亡は確定値、以下の比率は表示専用で戦闘計算へ返さない。
            var target = (float)health / maxHealth;
            if (!_hasHealth || _maxHealth != maxHealth || !isActiveAndEnabled)
            {
                _hasHealth = true;
                _maxHealth = maxHealth;
                _healthTarget = target;
                SnapHealth();
                return;
            }

            if (health == previousHealth)
            {
                return;
            }

            _isHealthDecreasing = health < previousHealth;
            _healthStart = _healthBar.normalizedValue;
            _trailingStart = Mathf.Max(_trailingHealth.rectTransform.anchorMax.x, _healthStart);
            _healthTarget = target;
            _healthElapsed = 0f;
            _isHealthAnimating = true;
            RenderHealth(_healthStart, _isHealthDecreasing ? _trailingStart : target);
        }

        /// <summary>
        /// 表示中のゲージだけを進めます。通知がない間は UI に値を書き込みません。
        /// [KO] 움직이는 게이지만 진행합니다. 진행 중 연출이 없으면 UI 값을 쓰지 않습니다.
        /// [EN] Advances active gauge transitions without writing UI values while idle.
        /// </summary>
        private void Update()
        {
            if (_isHealthAnimating)
            {
                Advance(Time.deltaTime);
            }
        }

        /// <summary>
        /// 経過秒から三つの表示比率を決めます。遅いフレームでも同じ経過時間なら同じ位置になります。
        /// [KO] 경과 시간으로 세 표시 비율을 결정합니다. 느린 프레임도 같은 경과 시간이면 같은 위치가 됩니다.
        /// [EN] Computes gauge ratios from elapsed seconds so equal elapsed time gives equal positions across frame rates.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!_isHealthAnimating)
            {
                return;
            }

            _healthElapsed += deltaSeconds;
            var healthProgress = Mathf.Clamp01(_healthElapsed / HealthLerpSeconds);
            var trailProgress = _isHealthDecreasing
                ? Mathf.Clamp01((_healthElapsed - TrailingDelaySeconds) / TrailingLerpSeconds)
                : 1f;
            // 毎フレームの現在値から Lerp すると到着が遅れ続けるため、通知時の開始値を固定する。
            // HP 100→60 の本体は補間の半分で 80、終端で必ず 60。残像は別の待機時間で追従する。
            RenderHealth(Mathf.Lerp(_healthStart, _healthTarget, healthProgress),
                Mathf.Lerp(_trailingStart, _healthTarget, trailProgress));
            if (healthProgress >= 1f && trailProgress >= 1f)
            {
                SnapHealth();
            }
        }

        /// <summary>
        /// 同じ FillArea 内で本体・背面の残像・前面の損失範囲を描きます。
        /// [KO] 같은 FillArea 안에 본체·뒤쪽 잔상·앞쪽 손실 구간을 그립니다.
        /// [EN] Renders the main fill, rear trail and front loss region inside the same FillArea.
        /// </summary>
        private void RenderHealth(float healthRatio, float trailingRatio)
        {
            _healthBar.SetValueWithoutNotify(healthRatio);
            _trailingHealth.rectTransform.anchorMax = new Vector2(trailingRatio, 1f);
            _trailingHealth.enabled = _isHealthDecreasing && trailingRatio > healthRatio;
            // 例: 確定 60%、本体 80% なら横 60〜80% だけを予告色にし、残りの HP は隠さない。
            var preview = Mathf.Min(_healthTarget, _previewTarget);
            _damagePreview.rectTransform.anchorMin = new Vector2(preview, 0f);
            _damagePreview.rectTransform.anchorMax = new Vector2(Mathf.Max(preview, healthRatio), 1f);
            _damagePreview.enabled = healthRatio > preview;
        }

        /// <summary>
        /// 命中前の確定損失範囲だけを示し、HP 数字と本体ゲージは動かしません。
        /// [KO] 명중 전 확정 손실 구간만 보여주고 HP 숫자·본 게이지는 움직이지 않습니다.
        /// [EN] Shows committed pending loss without changing health numbers or the main gauge.
        /// </summary>
        public void SetPreviewHealth(int health, int maxHealth)
        {
            _previewTarget = Mathf.Clamp01((float)health / maxHealth);
            RenderHealth(_healthBar.normalizedValue, _trailingHealth.rectTransform.anchorMax.x);
        }

        /// <summary>
        /// 初期化・非表示・Presenter 解放で呼び、補間を止めて確定値へ揃えます。
        /// [KO] 초기화·비활성화·Presenter 해제 시 호출해 보간을 멈추고 확정값으로 맞춥니다.
        /// [EN] Stops interpolation and aligns gauges with confirmed health during initialization, hiding or presenter disposal.
        /// </summary>
        private void SnapHealth()
        {
            _isHealthAnimating = false;
            _isHealthDecreasing = false;
            if (_healthBar != null && _trailingHealth != null && _damagePreview != null)
            {
                RenderHealth(_healthTarget, _healthTarget);
            }
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
        /// BattlePresenter が命中時に呼びます。HP 損失と防御膜吸収を別々に表示し、短時間後に消します。
        /// [KO] 명중 때 BattlePresenter가 호출합니다. HP 손실과 방어막 흡수를 나눠 표시하고 잠시 후 지웁니다.
        /// [EN] Called by BattlePresenter at impact to briefly display health loss separately from shield absorption.
        /// </summary>
        public void ShowDamage(int healthDamage, int shieldDamage) => _damageNumbers.Show(healthDamage, shieldDamage);

        /// <summary>
        /// 所有する短命表示だけを解除します。
        /// [KO] 소유한 짧은 표시만 해제합니다.
        /// [EN] Clears only the transient presentation owned here.
        /// </summary>
        public void ClearTransient()
        {
            if (_damageNumbers != null) _damageNumbers.Clear();
            _previewTarget = _healthTarget;
            SnapHealth();
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
