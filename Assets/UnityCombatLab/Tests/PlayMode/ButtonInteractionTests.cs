using System.Collections;
using DG.Tweening;
using NUnit.Framework;
using UnityCombatLab.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UnityCombatLab.Tests.PlayMode
{
    /// <summary>
    /// 実際の共通ボタン Variant で入力、クリック通知、演出の終了を確認します。
    /// [KO] 실제 공통 버튼 Variant에서 입력, 클릭 알림, 연출 종료를 확인합니다.
    /// [EN] Checks input, click delivery, and feedback cleanup on the actual shared button variant.
    /// </summary>
    public sealed class ButtonInteractionTests
    {
        /// <summary>
        /// このテストだけが生成して破棄するボタンと、必要な場合の EventSystem。
        /// [KO] 이 테스트만 생성하고 파괴할 버튼과 필요한 경우의 EventSystem입니다.
        /// [EN] Button and optional EventSystem created and destroyed only by this fixture.
        /// </summary>
        private GameObject _instance;
        private GameObject _host;
        private GameObject _eventSystemObject;
        private UnityEngine.UI.Button _button;
        private ActionButtonView _view;
        private RectTransform _visual;
        private Vector3 _homeScale;

        /// <summary>
        /// 保存済みの青い Variant を読み、実際の Base と派生 View を初期化します。
        /// [KO] 저장된 파란색 Variant를 읽어 실제 Base와 파생 View를 초기화합니다.
        /// [EN] Loads the saved blue variant and initializes its actual base and derived view.
        /// </summary>
        [UnitySetUp]
        public IEnumerator CreateButton()
        {
            if (EventSystem.current == null)
                _eventSystemObject = new GameObject("ButtonTestEventSystem", typeof(EventSystem));

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/UnityCombatLab/Prefabs/UI/Common/Buttons/UI_ActionButtonBlueView.prefab");
            Assert.That(prefab, Is.Not.Null);
            _host = new GameObject("ButtonTestHost", typeof(RectTransform));
            _host.SetActive(false);
            _instance = Object.Instantiate(prefab, _host.transform, false);
            _button = _instance.GetComponent<UnityEngine.UI.Button>();
            _view = _instance.GetComponent<ActionButtonView>();
            _visual = (RectTransform)_instance.transform.Find("UI_VisualRoot");
            // Awake より前に非等倍を設定し、等倍へ戻すだけの実装を検出する。
            _visual.localScale = new Vector3(1.25f, 0.8f, 1f);
            _homeScale = _visual.localScale;
            _host.SetActive(true);
            _view.SetClickHandler(() => { });
            yield return null;
        }

        /// <summary>
        /// 失敗してもテスト自身のオブジェクトを次のフレームまでに片付けます。
        /// [KO] 실패해도 테스트 자신의 오브젝트를 다음 프레임까지 정리합니다.
        /// [EN] Cleans up fixture-owned objects by the next frame even after a failure.
        /// </summary>
        [UnityTearDown]
        public IEnumerator DestroyButton()
        {
            Object.Destroy(_host);
            Object.Destroy(_eventSystemObject);
            yield return null;
        }

        /// <summary>
        /// 連続入力でも元のサイズへ戻り、root の位置とクリック領域が変わらないことを確認します。
        /// [KO] 연속 입력 후 원래 크기로 돌아오며 root 위치와 클릭 영역이 변하지 않는지 확인합니다.
        /// [EN] Checks recovery after rapid input without changing the root position or hit area.
        /// </summary>
        [UnityTest]
        public IEnumerator RapidInputKeepsHitAreaAndRestoresOriginalScale()
        {
            var root = (RectTransform)_instance.transform;
            var homePosition = root.anchoredPosition3D;
            var homeRootScale = root.localScale;
            var homeRect = root.rect;
            var homeVisualPosition = _visual.anchoredPosition3D;
            var mouse = Pointer(-1);
            for (var index = 0; index < 40; index++)
            {
                Send(mouse, ExecuteEvents.pointerEnterHandler);
                Send(mouse, ExecuteEvents.pointerDownHandler);
                if (index % 4 == 0)
                    yield return null;

                Send(mouse, ExecuteEvents.pointerUpHandler);
                Send(mouse, ExecuteEvents.pointerExitHandler);
            }

            yield return Settle();
            AssertScale(1f);
            Assert.That(_visual.anchoredPosition3D, Is.EqualTo(homeVisualPosition));
            Assert.That(root.anchoredPosition3D, Is.EqualTo(homePosition));
            Assert.That(root.localScale, Is.EqualTo(homeRootScale));
            Assert.That(root.rect, Is.EqualTo(homeRect));
        }

        /// <summary>
        /// 解放時の小さな行き過ぎをフレームごとに測り、最後はホバー倍率へ安定することを確認します。
        /// [KO] 해제 시 작은 오버슈트를 프레임별로 측정하고 마지막에 호버 배율로 안정되는지 확인합니다.
        /// [EN] Samples the small release overshoot per frame and checks settlement at the hover scale.
        /// </summary>
        [UnityTest]
        public IEnumerator HoverPressAndReleaseUseOneSmallOvershoot()
        {
            var mouse = Pointer(-1);
            Send(mouse, ExecuteEvents.pointerEnterHandler);
            yield return Settle();
            AssertScale(1.04f);
            Send(mouse, ExecuteEvents.pointerDownHandler);
            yield return Settle();
            AssertScale(0.96f);
            Send(mouse, ExecuteEvents.pointerUpHandler);
            var maximum = 0f;
            var deadline = Time.realtimeSinceStartup + 0.35f;
            while (Time.realtimeSinceStartup < deadline)
            {
                maximum = Mathf.Max(maximum, _visual.localScale.x / _homeScale.x);
                yield return null;
            }

            Assert.That(maximum, Is.GreaterThan(1.0401f).And.LessThan(1.065f));
            AssertScale(1.04f);
            Send(mouse, ExecuteEvents.pointerExitHandler);
            yield return Settle();
            AssertScale(1f);
        }

        /// <summary>
        /// 押したまま外へ出ると戻り、再進入では押下へ戻り、外で解放しても残らないことを確認します。
        /// [KO] 누른 채 이탈하면 복귀하고 재진입 시 누름으로 돌아가며 밖에서 놓아도 남지 않는지 확인합니다.
        /// [EN] Checks exit while held, reentry while held, and release outside without stuck press state.
        /// </summary>
        [UnityTest]
        public IEnumerator HeldPointerExitReentryAndOutsideReleaseRestoreCorrectly()
        {
            var mouse = Pointer(-1);
            Send(mouse, ExecuteEvents.pointerEnterHandler);
            Send(mouse, ExecuteEvents.pointerDownHandler);
            yield return Settle();
            Send(mouse, ExecuteEvents.pointerExitHandler);
            yield return Settle();
            AssertScale(1f);
            Send(mouse, ExecuteEvents.pointerEnterHandler);
            yield return Settle();
            AssertScale(0.96f);
            Send(mouse, ExecuteEvents.pointerExitHandler);
            Send(mouse, ExecuteEvents.pointerUpHandler);
            yield return Settle();
            AssertScale(1f);
        }

        /// <summary>
        /// interactable、Button.enabled、親 CanvasGroup の禁止を次の描画前に反映することを確認します。
        /// [KO] interactable, Button.enabled, 부모 CanvasGroup의 차단을 다음 렌더 전에 반영하는지 확인합니다.
        /// [EN] Checks reset when interactable, Button.enabled, or a parent CanvasGroup blocks interaction.
        /// </summary>
        [UnityTest]
        public IEnumerator InteractionLocksResetFeedbackAndSuppressHover()
        {
            var mouse = Pointer(-1);
            var group = _host.AddComponent<CanvasGroup>();
            for (var mode = 0; mode < 3; mode++)
            {
                Send(mouse, ExecuteEvents.pointerEnterHandler);
                Send(mouse, ExecuteEvents.pointerDownHandler);
                yield return Settle();
                if (mode == 0)
                    _button.interactable = false;
                else if (mode == 1)
                    _button.enabled = false;
                else
                    group.interactable = false;

                yield return null;
                yield return null;
                AssertScale(1f);
                Send(mouse, ExecuteEvents.pointerEnterHandler);
                Send(mouse, ExecuteEvents.pointerDownHandler);
                yield return Settle();
                AssertScale(1f);
                _button.interactable = true;
                _button.enabled = true;
                group.interactable = true;
            }
        }

        /// <summary>
        /// 新 Input System の Touch はホバーせず、他の指の解放では押下を解除しないことを確認します。
        /// [KO] 새 Input System Touch가 호버하지 않고 다른 손가락 해제로 누름을 해제하지 않는지 확인합니다.
        /// [EN] Checks touch without hover and prevents another finger's release from ending the tracked press.
        /// </summary>
        [UnityTest]
        public IEnumerator TouchHasNoHoverAndTracksThePressedFinger()
        {
            var touch = new ExtendedPointerEventData(EventSystem.current)
            {
                pointerId = 21,
                pointerType = UIPointerType.Touch,
                button = PointerEventData.InputButton.Left
            };
            Send(touch, ExecuteEvents.pointerEnterHandler);
            yield return Settle();
            AssertScale(1f);
            Send(touch, ExecuteEvents.pointerDownHandler);
            yield return Settle();
            AssertScale(0.96f);
            Send(Pointer(22), ExecuteEvents.pointerUpHandler);
            yield return Settle();
            AssertScale(0.96f);
            Send(touch, ExecuteEvents.pointerUpHandler);
            yield return Settle();
            AssertScale(1f);
        }

        /// <summary>
        /// 解放イベントが来ないフォーカス喪失と休止の通知でも押下が残らないことを確認します。
        /// [KO] 해제 이벤트가 오지 않는 포커스 상실과 일시 중단 알림에도 누름이 남지 않는지 확인합니다.
        /// [EN] Checks cleanup on focus-loss and pause notifications when no release event arrives.
        /// </summary>
        [UnityTest]
        public IEnumerator FocusAndPauseNotificationsClearPressWithoutRelease()
        {
            Send(Pointer(-1), ExecuteEvents.pointerDownHandler);
            yield return Settle();
            _instance.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            AssertScale(1f);
            Send(Pointer(-1), ExecuteEvents.pointerDownHandler);
            yield return Settle();
            _instance.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);
            AssertScale(1f);
            yield return Settle();
            AssertScale(1f);
        }

        /// <summary>
        /// 画面非表示と破棄は自分の Tween を止め、同じ表示を対象にした別の Tween を止めないことを確認します。
        /// [KO] 화면 숨김과 파괴가 자신의 Tween만 멈추고 같은 표시를 대상으로 한 다른 Tween을 멈추지 않는지 확인합니다.
        /// [EN] Checks hide and destruction cleanup without killing another tween targeting the same visual.
        /// </summary>
        [UnityTest]
        public IEnumerator DisableAndDestroyCleanUpOnlyOwnedTween()
        {
            var otherValue = 0f;
            var otherTween = DOTween.To(() => otherValue, value => otherValue = value, 1f, 5f)
                .SetTarget(_visual).SetUpdate(true).SetRecyclable(false);
            try
            {
                Send(Pointer(-1), ExecuteEvents.pointerDownHandler);
                yield return null;
                _instance.SetActive(false);
                AssertScale(1f);
                Assert.That(otherTween.IsActive(), Is.True);
                _instance.SetActive(true);
                yield return Settle();
                AssertScale(1f);
                Send(Pointer(-1), ExecuteEvents.pointerDownHandler);
                var feedback = _instance.GetComponent<ButtonInteractionFeedback>();
                Object.Destroy(feedback);
                yield return null;
                AssertScale(1f);
                Assert.That(otherTween.IsActive(), Is.True);
                yield return Settle();
                AssertScale(1f);
            }
            finally
            {
                otherTween.Kill(false);
            }
        }

        /// <summary>
        /// 何度設定しても最新 callback だけがクリックとキーボード決定で即時に一回呼ばれることを確認します。
        /// [KO] 여러 번 설정해도 최신 callback만 클릭과 키보드 결정에서 즉시 한 번 호출되는지 확인합니다.
        /// [EN] Checks immediate single delivery to the latest callback after rebinding for clicks and keyboard submit.
        /// </summary>
        [UnityTest]
        public IEnumerator RebindingKeepsSingleImmediateClickAndKeyboardDelivery()
        {
            var oldCalls = 0;
            var calls = 0;
            _view.SetClickHandler(() => oldCalls++);
            for (var index = 0; index < 5; index++)
                _view.SetClickHandler(() => calls++);

            Assert.That(_instance.GetComponents<CommonButtonViewBase>().Length, Is.EqualTo(1));
            Assert.That(_button.onClick.GetPersistentEventCount(), Is.Zero);
            Send(Pointer(-1), ExecuteEvents.pointerClickHandler);
            Assert.That(calls, Is.EqualTo(1));
            ExecuteEvents.Execute(_instance, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(oldCalls, Is.Zero);
            _button.interactable = false;
            Send(Pointer(-1), ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(_instance, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(calls, Is.EqualTo(2));
            yield return Settle();
            Assert.That(calls, Is.EqualTo(2));
        }

        /// <summary>
        /// テストで使う左マウスまたは指のイベントを作ります。
        /// [KO] 테스트에 사용할 왼쪽 마우스 또는 손가락 이벤트를 만듭니다.
        /// [EN] Creates the left mouse or finger event used by the tests.
        /// </summary>
        private static PointerEventData Pointer(int pointerId)
            => new PointerEventData(EventSystem.current)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left
            };

        /// <summary>
        /// Native Button とフィードバックへ EventSystem と同じ handler を送ります。
        /// [KO] native Button과 피드백에 EventSystem과 같은 handler를 보냅니다.
        /// [EN] Sends the same event handlers used by EventSystem to the native Button and feedback.
        /// </summary>
        private void Send<T>(PointerEventData pointer, ExecuteEvents.EventFunction<T> handler)
            where T : IEventSystemHandler
            => ExecuteEvents.Execute(_instance, pointer, handler);

        /// <summary>
        /// 実時間の復帰 Tween が終わるまで待ちます。
        /// [KO] 실시간 복귀 Tween이 끝날 때까지 기다립니다.
        /// [EN] Waits for the real-time return tween to settle.
        /// </summary>
        private static IEnumerator Settle()
            => new WaitForSecondsRealtime(0.25f);

        /// <summary>
        /// 元の非等倍も保つ倍率を確認します。
        /// [KO] 원래 비등배도 유지하는 배율을 확인합니다.
        /// [EN] Checks the multiplier relative to the original scale, including non-unit axes.
        /// </summary>
        private void AssertScale(float multiplier)
        {
            Assert.That(_visual.localScale.x, Is.EqualTo(_homeScale.x * multiplier).Within(0.0001f));
            Assert.That(_visual.localScale.y, Is.EqualTo(_homeScale.y * multiplier).Within(0.0001f));
            Assert.That(_visual.localScale.z, Is.EqualTo(_homeScale.z));
        }
    }
}
