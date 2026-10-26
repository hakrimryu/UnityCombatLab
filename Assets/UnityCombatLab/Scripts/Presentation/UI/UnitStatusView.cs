using System;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 一人分の名前・HP バー・防御膜・被害数字を、Presenter から渡された値で表示します。
    /// [KO] Presenter가 전달한 값으로 한 명의 이름·HP 바·방어막·피해 숫자를 표시합니다.
    /// [EN] Displays one unit’s name, health bar, shield and damage numbers using values supplied by the presenter.
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
        /// ユニットの役割名を表示する文字。
        /// [KO] 유닛의 역할 이름을 표시할 글자입니다.
        /// [EN] Text displaying the unit's role name.
        /// </summary>
        [SerializeField] private TMP_Text _name;

        /// <summary>
        /// 現在 HP と最大 HP を表示する文字。
        /// [KO] 현재 HP와 최대 HP를 표시할 글자입니다.
        /// [EN] Text displaying current and maximum health.
        /// </summary>
        [SerializeField] private TMP_Text _health;

        /// <summary>
        /// 残っている防御膜を表示する文字。
        /// [KO] 남아 있는 방어막을 표시할 글자입니다.
        /// [EN] Text displaying the remaining shield value.
        /// </summary>
        [SerializeField] private TMP_Text _shield;

        /// <summary>
        /// HP バーの塗り部分。Initialize が陣営色を適用します。
        /// [KO] HP 바의 채움 부분입니다. Initialize가 팀 색상을 적용합니다.
        /// [EN] Health-bar fill image whose team color is set by Initialize.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _fill;

        /// <summary>
        /// HP 比率に応じて塗り幅を変える表示用 Slider。
        /// [KO] HP 비율에 따라 채움 너비를 바꿀 표시용 Slider입니다.
        /// [EN] Display slider whose fill width follows the health ratio.
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
        /// 配置順ではなく、Presenterが渡す実際の職業画像を表示します。
        /// [KO] 배치 순서 대신 Presenter가 전달하는 실제 직업 이미지를 표시합니다.
        /// [EN] Shows the actual role image supplied by the presenter rather than an image fixed to a slot.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Image _roleIcon;

        /// <summary>
        /// HP 0 を色だけでなく「戦闘不能」と伝える表示先。
        /// [KO] HP 0을 색뿐 아니라 '전투 불능' 문구로 알리는 표시 대상입니다.
        /// [EN] Label explicitly identifying defeat at zero HP rather than relying on color alone.
        /// </summary>
        [SerializeField] private TMP_Text _defeated;

        /// <summary>
        /// 被害数字専用プールと各表示の終了処理を所有するコンポーネント。
        /// [KO] 피해 숫자 전용 풀과 각 표시의 종료 처리를 소유한 컴포넌트입니다.
        /// [EN] Component owning the damage-number pool and each display's expiry.
        /// </summary>
        [SerializeField] private DamageNumberView _damageNumbers;

        /// <summary>
        /// この共通パネル内の固定色。親HUDはパネル内部の部品を直接参照しません。
        /// [KO] 공통 패널 내부 고정 색상이며 부모 HUD는 패널 내부 부품을 직접 참조하지 않습니다.
        /// [EN] Static colors within this shared panel, keeping its internal graphics out of the parent HUD's bindings.
        /// </summary>
        [SerializeField] private PaletteGraphicBinding[] _paletteBindings;

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
        /// 最新の HP 通知を受けた時点のゲージ比率。補間の開始値に使います。
        /// [KO] 최신 HP 알림을 받은 시점의 게이지 비율이며 보간 시작값으로 사용합니다.
        /// [EN] Gauge ratio when the latest health notification arrived, used as the interpolation start.
        /// </summary>
        private float _healthStart;

        /// <summary>
        /// ゲージの移動先となる、通知された HP の比率。
        /// [KO] 게이지가 이동할 목표인 알림으로 받은 HP 비율입니다.
        /// [EN] Notified health ratio used as the gauge's destination.
        /// </summary>
        private float _healthTarget;

        /// <summary>
        /// 最新通知時点の残像比率。連続被害では既に残っている範囲を維持します。
        /// [KO] 최신 알림 시점 잔상 비율입니다. 연속 피해에는 이미 남아 있는 구간을 유지합니다.
        /// [EN] Trail ratio at the latest notification, preserving an existing trail through consecutive damage.
        /// </summary>
        private float _trailingStart;

        /// <summary>
        /// 前回の最大 HP。最大値の変更時にゲージを再初期化するために使います。
        /// [KO] 이전 최대 HP이며 최대값 변경 시 게이지를 다시 초기화하는 데 사용합니다.
        /// [EN] Previous maximum health, used to reinitialize the gauge when the maximum changes.
        /// </summary>
        private int _maxHealth;

        /// <summary>
        /// 未到着の損失も含めた予告先。数字と本体の命中通知とは別に受け取ります。
        /// [KO] 미도착 손실까지 포함한 예고 목표이며 숫자·본체의 명중 알림과 별도로 받습니다.
        /// [EN] Preview target including pending hits, received separately from arrived health.
        /// </summary>
        private float _previewTarget = 1f;


        /// <summary>
        /// 現在表示している HP。
        /// [KO] 현재 표시 중인 HP입니다.
        /// [EN] Currently displayed health.
        /// </summary>
        public int DisplayedHealth { get; private set; }

        /// <summary>
        /// 現在表示している防御膜。
        /// [KO] 현재 표시 중인 방어막입니다.
        /// [EN] Currently displayed shield value.
        /// </summary>
        public int DisplayedShield { get; private set; }

        /// <summary>
        /// 変更通知の検証に使う HP 更新回数。
        /// [KO] 변경 알림 검증에 쓰는 HP 갱신 횟수.
        /// [EN] Health update count for notification validation.
        /// </summary>
        public int HealthRenderCount { get; private set; }

        /// <summary>
        /// 名前・職業画像・陣営色を設定し、被害数字とゲージの補間を初期化します。
        /// [KO] 이름·직업 이미지·팀 색상을 설정하고 피해 숫자와 게이지 보간을 초기화합니다.
        /// [EN] Sets the name, role image and team colors and resets damage numbers and gauge interpolation.
        /// </summary>
        public void Initialize(string displayName, Define.BattleTeam team, Sprite icon)
        {
            if (_name == null || _health == null || _shield == null || _fill == null || _healthBar == null ||
                _damageNumbers == null || _shieldIcon == null ||
                _badge == null || _defeated == null || _colors == null ||
                _damagePreview == null || _trailingHealth == null || _roleIcon == null || icon == null)
            {
                throw new InvalidOperationException("Unit status references must be assigned.");
            }

            ClearTransient();
            _hasHealth = false;
            _previewTarget = 1f;
            _name.text = displayName;
            _roleIcon.sprite = icon;
            _teamColor = _colors.GetTeamColor(team);
            _fill.color = _teamColor;
            _badge.color = _teamColor;
            _damagePreview.color = _colors.GetColor(Define.PaletteColor.DamagePreview);
            _trailingHealth.color = _colors.GetColor(Define.PaletteColor.TrailingHealth);
        }

        /// <summary>
        /// BattleHudView が渡すパレットを保持し、HUD 所有の共通数字材質を DamageNumberView へ渡します。
        /// [KO] BattleHudView가 전달한 팔레트를 보관하고 HUD 소유 공통 숫자 재질을 DamageNumberView에 전달합니다.
        /// [EN] Stores the palette supplied by BattleHudView and forwards the HUD-owned shared number material to DamageNumberView.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors, Material damageMaterial)
        {
            _colors = colors != null ? colors : throw new ArgumentNullException(nameof(colors));
            if (_paletteBindings == null || _paletteBindings.Length == 0)
                throw new InvalidOperationException("Unit status requires local palette bindings.");

            foreach (var binding in _paletteBindings)
            {
                if (binding == null)
                    throw new InvalidOperationException("Unit status has a missing palette binding.");

                binding.Apply(colors);
            }

            if (_damageNumbers == null)
                throw new InvalidOperationException("Damage number view must be assigned.");

            _damageNumbers.ApplyMaterial(damageMaterial);
        }

        /// <summary>
        /// 通知された HP と戦闘不能表示を更新し、ゲージを新しい HP へ動かします。
        /// [KO] 알림으로 받은 HP와 전투 불능 표시를 갱신하고 게이지를 새 HP로 움직입니다.
        /// [EN] Updates notified health and defeat indicators and moves the gauge toward the new health value.
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
            // HP 75/100 はゲージ比率 0.75。
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
        /// HP ゲージと数字へ適用する戦闘速度。ゼロで途中の状態を保ちます。
        /// [KO] HP 게이지·숫자에 적용하는 전투 속도이며 0이면 중간 상태를 유지합니다.
        /// [EN] Combat speed for gauges and numbers, preserving their current state at zero.
        /// </summary>
        private float _playbackSpeed = 1f;

        /// <summary>
        /// このパネルの戦闘時計で HP ゲージを進めます。
        /// [KO] 이 패널의 전투 시간으로 HP 게이지를 진행합니다.
        /// [EN] Advances health gauges using this panel's combat clock.
        /// </summary>
        private void Update()
        {
            if (_isHealthAnimating)
            {
                Advance(Time.unscaledDeltaTime * _playbackSpeed);
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
        /// 確定順で表示した回復を数字プールへ渡します。
        /// [KO] 확정 순서로 표시한 회복을 숫자 풀에 전달합니다.
        /// [EN] Sends healing presented in committed order to the number pool.
        /// </summary>
        public void ShowHealing(int amount)
            => _damageNumbers.ShowHealing(amount);

        /// <summary>
        /// ゲージと数字の戦闘速度を揃え、ゼロで途中の状態を保ちます。
        /// [KO] 게이지·숫자의 전투 속도를 맞추고 0이면 중간 상태를 유지합니다.
        /// [EN] Aligns gauge and number speed, preserving their current state at zero.
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            _damageNumbers.SetPlaybackSpeed(speed);
        }

        /// <summary>
        /// 被害数字を消し、HP ゲージの補間を止めて現在 HP に揃えます。
        /// [KO] 피해 숫자를 지우고 HP 게이지 보간을 멈춰 현재 HP에 맞춥니다.
        /// [EN] Clears damage numbers and stops gauge interpolation at current health.
        /// </summary>
        public void ClearTransient()
        {
            if (_damageNumbers != null) _damageNumbers.Clear();
            _previewTarget = _healthTarget;
            SnapHealth();
        }

        /// <summary>
        /// 非表示時に被害数字と HP ゲージのアニメーションを止めます。
        /// [KO] 비활성화 시 피해 숫자와 HP 게이지 애니메이션을 멈춥니다.
        /// [EN] Stops damage-number and health-gauge animations when disabled.
        /// </summary>
        private void OnDisable()
        {
            ClearTransient();
        }
    }
}
