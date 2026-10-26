using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 一人分の被害数字専用プール。空き枠を貸し出し、上昇とフェードの完了で返却します。
    /// [KO] 한 유닛의 피해 숫자 전용 풀입니다. 빈 슬롯을 대여하고 상승·페이드 완료 시 반환합니다.
    /// [EN] Owns one unit's damage-number pool, renting free slots and returning them after their rise and fade.
    /// </summary>
    public sealed class DamageNumberView : MonoBehaviour
    {
        /// <summary>
        /// ゲーム内時間で数える数字の表示秒数。各スロットが独立して進みます。
        /// [KO] 게임 시간 기준 숫자 표시 시간이며 각 슬롯이 독립적으로 진행합니다.
        /// [EN] Number display duration in game time, advanced independently for each slot.
        /// </summary>
        [SerializeField, Min(0.01f)] private float _displaySeconds = 0.75f;

        /// <summary>
        /// 数字の基準点から下へずらす UI 単位。1920×1080 基準で 18px 下から始めます。
        /// [KO] 숫자 기준점에서 아래로 옮길 UI 단위이며 1920×1080 기준 18px 아래에서 시작합니다.
        /// [EN] UI units below the number anchor; starts 18px lower at the 1920×1080 reference resolution.
        /// </summary>
        [SerializeField, Min(0f)] private float _startBelowAnchor = 18f;

        /// <summary>
        /// 開始位置から上昇する UI 単位。初期値 64px は基準点より 46px 上で終わります。
        /// [KO] 시작 위치에서 상승할 UI 단위이며 초깃값 64px는 기준점보다 46px 위에서 끝납니다.
        /// [EN] UI units to rise from the start; the default 64px rise finishes 46px above the anchor.
        /// </summary>
        [SerializeField, Min(0f)] private float _riseDistance = 64f;

        /// <summary>
        /// プレハブの三枠から開始し、不足時だけ追加した枠も保持して再利用します。
        /// [KO] 프리팹의 세 슬롯으로 시작하고 부족할 때만 추가한 슬롯도 보관해 재사용합니다.
        /// [EN] Starts with three prefab slots and retains extra slots created only on exhaustion for reuse.
        /// </summary>
        [SerializeField] private List<DamageSlot> _slots = new();

        /// <summary>
        /// 戦闘だけに適用する速度。UI ボタンの Tween には影響しません。
        /// [KO] 전투에만 적용하는 속도이며 UI 버튼 Tween에는 영향을 주지 않습니다.
        /// [EN] Speed applied only to combat, independent of UI button tweens.
        /// </summary>
        private float _playbackSpeed = 1f;

        /// <summary>
        /// 一回の被害の数字二つ・盾アイコン・Tween と、古い callback を拒否する番号を保持します。
        /// [KO] 한 피해의 숫자 두 개·방패 아이콘·Tween과 이전 콜백을 거부할 번호를 보관합니다.
        /// [EN] Holds one hit's two labels, shield icon, tween and stale-callback version.
        /// </summary>
        [Serializable]
        private sealed class DamageSlot
        {
            /// <summary>
            /// HP 数字・盾吸収数字・盾アイコンをまとめて移動・フェード・前面化する枠。
            /// [KO] HP 숫자·방어막 흡수 숫자·방패 아이콘을 함께 이동·페이드·전면 배치할 슬롯입니다.
            /// [EN] Group moving, fading and ordering its health label, shield-absorption label and shield icon.
            /// </summary>
            [SerializeField] private CanvasGroup _group;

            /// <summary>
            /// OnAccent パレット色で HP 損失を表示する数字。
            /// [KO] OnAccent 팔레트 색상으로 HP 손실을 표시할 숫자입니다.
            /// [EN] Health-loss label using the OnAccent palette color.
            /// </summary>
            [SerializeField] private TMP_Text _healthDamage;

            /// <summary>
            /// HP 損失とは別の行に表示する盾吸収数字。
            /// [KO] HP 손실과 다른 줄에 표시할 방어막 흡수 숫자입니다.
            /// [EN] Shield-absorption label on a separate line from health loss.
            /// </summary>
            [SerializeField] private TMP_Text _shieldDamage;

            /// <summary>
            /// 盾吸収がある時だけ表示するアイコン。
            /// [KO] 방어막 흡수가 있을 때만 표시할 아이콘입니다.
            /// [EN] Icon displayed only when shield absorption exists.
            /// </summary>
            [SerializeField] private UnityEngine.UI.Image _shieldIcon;

            /// <summary>
            /// この枠の移動とフェードを同時に進め、返却時に止める Tween。
            /// [KO] 이 슬롯의 이동·페이드를 함께 진행하고 반환 시 멈출 Tween입니다.
            /// [EN] Tween advancing this slot's movement and fade together, stopped on return.
            /// </summary>
            private Tween _animation;

            /// <summary>
            /// 返却ごとに増やし、以前の完了 callback が新しい表示を消すのを防ぎます。
            /// [KO] 반환마다 증가하여 이전 완료 콜백이 새 표시를 지우지 못하게 합니다.
            /// [EN] Increases on return so an earlier completion callback cannot erase a new display.
            /// </summary>
            private int _version;

            /// <summary>
            /// このスロットに数字を表示している間は true。完了または Clear で false に戻ります。
            /// [KO] 이 슬롯에 숫자를 표시하는 동안 true이며 완료 또는 Clear 시 false로 돌아갑니다.
            /// [EN] True while this slot displays a hit, reset on completion or Clear.
            /// </summary>
            public bool IsInUse { get; private set; }

            /// <summary>
            /// 数字とアイコンが同じ枠の直接の子として接続されているか確認します。
            /// [KO] 숫자와 아이콘이 같은 슬롯의 직계 자식으로 연결됐는지 확인합니다.
            /// [EN] Checks both labels and the icon are connected as direct children of the same slot.
            /// </summary>
            public void Validate(Transform owner)
            {
                if (_group == null || _healthDamage == null || _shieldDamage == null || _shieldIcon == null ||
                    _group.transform.parent != owner || _group.transform is not RectTransform ||
                    _healthDamage.transform.parent != _group.transform ||
                    _shieldDamage.transform.parent != _group.transform ||
                    _shieldIcon.transform.parent != _group.transform || _group.transform.childCount != 3)
                    throw new InvalidOperationException(
                        "Each damage slot requires a group with two labels and a shield icon.");
            }

            /// <summary>
            /// 全枠使用中の時だけ既存の表示構成を複製し、空き枠として返します。
            /// [KO] 모든 슬롯이 사용 중일 때만 기존 표시 구성을 복제해 빈 슬롯으로 반환합니다.
            /// [EN] Copies an existing display only on exhaustion and returns it as a free slot.
            /// </summary>
            public DamageSlot CreateCopy(int index)
            {
                var copy = Instantiate(_group, _group.transform.parent);
                copy.name = "UI_DamageSlot" + index;
                // 複製元と同じ子の順序で数字とアイコンを取得する。
                var slot = new DamageSlot
                {
                    _group = copy,
                    _healthDamage = copy.transform.GetChild(_healthDamage.transform.GetSiblingIndex())
                        .GetComponent<TMP_Text>(),
                    _shieldDamage = copy.transform.GetChild(_shieldDamage.transform.GetSiblingIndex())
                        .GetComponent<TMP_Text>(),
                    _shieldIcon = copy.transform.GetChild(_shieldIcon.transform.GetSiblingIndex())
                        .GetComponent<UnityEngine.UI.Image>()
                };
                // 複製元が上昇中でも、コピーした位置・透明度・文字を初期化してから貸し出す。
                slot.Clear();
                return slot;
            }

            /// <summary>
            /// HUD 所有の共通材質を数字へ設定し、初期枠のパレット色を数字とアイコンへ適用します。
            /// [KO] HUD 소유 공통 재질을 숫자에 설정하고 초기 슬롯의 팔레트 색상을 숫자와 아이콘에 적용합니다.
            /// [EN] Assigns the HUD-owned shared material to labels and applies the initial slot's palette colors to labels and icon.
            /// </summary>
            public void ApplyStyle(Material material, DamageSlot source)
            {
                _healthDamage.fontSharedMaterial = material;
                _shieldDamage.fontSharedMaterial = material;
                _healthDamage.color = source._healthDamage.color;
                _shieldDamage.color = source._shieldDamage.color;
                _shieldIcon.color = source._shieldIcon.color;
            }

            /// <summary>
            /// 空き枠を初期化して最前面へ出し、完了時に同じ使用番号の表示だけを返却します。
            /// [KO] 빈 슬롯을 초기화해 맨 앞으로 배치하고 완료 시 같은 사용 번호의 표시만 반환합니다.
            /// [EN] Resets a free slot, brings it forward and returns only the matching rental on completion.
            /// </summary>
            public void Show(DamageNumberView owner, int healthDamage, int shieldDamage, int healing = 0)
            {
                Clear();
                IsInUse = true;
                var version = _version;
                var root = (RectTransform)_group.transform;
                root.anchoredPosition = Vector2.down * owner._startBelowAnchor;
                _group.alpha = 1f;
                _healthDamage.text = healing > 0 ? $"<color=#5DD9B5>+{healing}</color>" : healthDamage > 0 ? "-" + healthDamage : "";
                _shieldDamage.text = shieldDamage > 0 ? "-" + shieldDamage : "";
                _shieldIcon.gameObject.SetActive(shieldDamage > 0);
                root.gameObject.SetActive(true);
                // Overlay Canvas は後の兄弟を手前に描く。枠だけを動かし、数字・輪郭・アイコンの内部順は保つ。
                root.SetAsLastSibling();
                // スロットの位置と透明度を同時に変え、完了時に返却する。
                _animation = DOTween.Sequence()
                    .Append(DOTween.To(() => root.anchoredPosition,
                            position => root.anchoredPosition = position,
                            Vector2.up * (owner._riseDistance - owner._startBelowAnchor), owner._displaySeconds)
                        .SetEase(Ease.OutQuad))
                    .Join(DOTween.To(() => _group.alpha, alpha => _group.alpha = alpha, 0f, owner._displaySeconds)
                        .SetEase(Ease.InQuad))
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        // Clear は Kill より先に番号を変える。返却・再貸出後の古い完了通知は何も変更しない。
                        if (owner == null || !IsInUse || version != _version) return;
                        _animation = null;
                        Clear();
                    });
                _animation.timeScale = owner._playbackSpeed;
            }

            /// <summary>
            /// この数字の所有 Tween だけへ速度を設定します。
            /// [KO] 이 숫자가 소유한 Tween에만 속도를 설정합니다.
            /// [EN] Sets speed only on the tween owned by this number.
            /// </summary>
            public void SetPlaybackSpeed(float speed)
            {
                if (_animation != null && _animation.IsActive())
                    _animation.timeScale = speed;
            }

            /// <summary>
            /// 古い callback を無効にして全表示状態を戻し、非表示の空き枠として保持します。
            /// [KO] 이전 콜백을 무효화하고 표시 상태를 모두 초기화해 비활성 빈 슬롯으로 보관합니다.
            /// [EN] Invalidates old callbacks and resets all display state, retaining the slot hidden and available.
            /// </summary>
            public void Clear()
            {
                _version++;
                _animation?.Kill();
                _animation = null;
                IsInUse = false;
                if (_healthDamage != null) _healthDamage.text = "";
                if (_shieldDamage != null) _shieldDamage.text = "";
                if (_shieldIcon != null) _shieldIcon.gameObject.SetActive(false);
                if (_group == null) return;
                _group.alpha = 0f;
                var root = (RectTransform)_group.transform;
                root.anchoredPosition = Vector2.zero;
                root.localScale = Vector3.one;
                root.localRotation = Quaternion.identity;
                root.gameObject.SetActive(false);
                // 親の OnDisable 中は兄弟順を変えられない。次の Show が表示直前に最前面へ置く。
            }
        }

        /// <summary>
        /// 接続を確認してプレハブの全枠を空き状態にします。
        /// [KO] 연결을 확인하고 프리팹의 모든 슬롯을 빈 상태로 만듭니다.
        /// [EN] Validates references and clears all prefab slots for rental.
        /// </summary>
        private void Awake()
        {
            Validate();
            Clear();
        }

        /// <summary>
        /// 初期三枠以上と演出時間・距離を、材質設定前にも確認します。
        /// [KO] 초기 세 슬롯 이상과 연출 시간·거리를 재질 설정 전에도 확인합니다.
        /// [EN] Checks at least three initial slots and valid animation timing and distances before material assignment.
        /// </summary>
        private void Validate()
        {
            if (_slots == null || _slots.Count < Define.SlotsPerTeam ||
                !float.IsFinite(_displaySeconds) || _displaySeconds <= 0f ||
                !float.IsFinite(_startBelowAnchor) || _startBelowAnchor < 0f ||
                !float.IsFinite(_riseDistance) || _riseDistance < 0f)
                throw new InvalidOperationException(
                    "Damage numbers require three authored slots and valid animation settings.");
            foreach (var slot in _slots)
            {
                if (slot == null) throw new InvalidOperationException("Damage slots cannot be null.");
                slot.Validate(transform);
            }
        }

        /// <summary>
        /// BattleHudView から借りた共通材質を全枠へ適用します。追加枠もこの参照を複製します。
        /// [KO] BattleHudView에서 빌린 공통 재질을 모든 슬롯에 적용하며 추가 슬롯도 이 참조를 복사합니다.
        /// [EN] Applies the shared material borrowed from BattleHudView to all slots, including the reference copied by new slots.
        /// </summary>
        public void ApplyMaterial(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            Validate();
            // パレット適用済みの初期スロットから、追加スロットにも色を揃える。
            foreach (var slot in _slots) slot.ApplyStyle(material, _slots[0]);
        }

        /// <summary>
        /// UnitStatusView が命中時に呼び、空き枠を優先し、不足時だけ一枠追加して被害を表示します。
        /// [KO] UnitStatusView가 명중 시 호출하며 빈 슬롯을 우선 사용하고 부족할 때만 하나 추가해 피해를 표시합니다.
        /// [EN] Called by UnitStatusView at impact to show damage in a free slot, adding one only when exhausted.
        /// </summary>
        public void Show(int healthDamage, int shieldDamage)
        {
            if (healthDamage < 0) throw new ArgumentOutOfRangeException(nameof(healthDamage));
            if (shieldDamage < 0) throw new ArgumentOutOfRangeException(nameof(shieldDamage));
            if (healthDamage == 0 && shieldDamage == 0) return;
            if (!isActiveAndEnabled) throw new InvalidOperationException("Damage numbers require an active view.");
            foreach (var slot in _slots)
            {
                if (slot.IsInUse) continue;
                slot.Show(this, healthDamage, shieldDamage);
                return;
            }

            // 同時命中には初期 3 枠を使い、全て使用中なら一枠追加して表示を続ける。
            var extra = _slots[0].CreateCopy(_slots.Count);
            _slots.Add(extra);
            extra.Show(this, healthDamage, shieldDamage);
        }

        /// <summary>
        /// 終了・再接続時に全枠を返却します。追加枠も HUD の寿命中は保持して再利用します。
        /// [KO] 종료·재연결 시 모든 슬롯을 반환하며 추가 슬롯도 HUD 수명 동안 보관해 재사용합니다.
        /// [EN] Returns every slot on shutdown or rebinding, retaining extra slots for reuse during the HUD lifetime.
        /// </summary>
        public void Clear()
        {
            if (_slots == null) return;
            foreach (var slot in _slots) slot?.Clear();
        }

        /// <summary>
        /// 回復済み HP を同じ数字プールへ正符号付きで表示します。
        /// [KO] 회복한 HP를 같은 숫자 풀에 양수 부호로 표시합니다.
        /// [EN] Displays restored health with a positive sign using the same number pool.
        /// </summary>
        public void ShowHealing(int amount)
        {
            if (amount <= 0)
                return;

            foreach (var slot in _slots)
            {
                if (slot.IsInUse)
                    continue;

                slot.Show(this, 0, 0, amount);
                return;
            }

            var extra = _slots[0].CreateCopy(_slots.Count);
            _slots.Add(extra);
            extra.Show(this, 0, 0, amount);
        }

        /// <summary>
        /// 表示中と後から借りる数字に同じ戦闘速度を適用します。
        /// [KO] 표시 중이거나 이후 빌릴 숫자에 같은 전투 속도를 적용합니다.
        /// [EN] Applies the same combat speed to active and subsequently borrowed numbers.
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            foreach (var slot in _slots)
                slot.SetPlaybackSpeed(speed);
        }

        /// <summary>
        /// 非表示・破棄時に全枠の Tween と callback を解除します。追加の子枠も HUD と共に破棄されます。
        /// [KO] 비활성화·파괴 시 모든 슬롯의 Tween·콜백을 해제하며 추가 자식 슬롯도 HUD와 함께 파괴됩니다.
        /// [EN] Releases all tweens and callbacks on disable or destruction; extra children are destroyed with the HUD.
        /// </summary>
        private void OnDisable() => Clear();
    }
}
