using System;
using NUnit.Framework;
using UnityCombatLab.Application.Run;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.UI;
using UnityCombatLab.Presentation.ReadModels;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// Frontend入力の規則と解除後の安全性を確認します。
    /// [KO] Frontend 입력 규칙과 해제 후 안전성을 확인합니다.
    /// [EN] Verifies Frontend input rules and safety after disposal.
    /// </summary>
    public sealed class FrontendPresenterTests
    {
        /// <summary>
        /// 画面入力から英雄交換と難易度を保存し、出発連打を一度に抑えます。
        /// [KO] 화면 입력으로 영웅 교환과 난이도를 저장하고 연속 출발을 한 번으로 제한하는지 확인합니다.
        /// [EN] Checks hero swaps, difficulty persistence and one departure for repeated input.
        /// </summary>
        [Test]
        public void SelectionAndDepartureFollowPresenterRules()
        {
            var controller = new RunController();
            var view = new FakeView();
            using var presenter = new FrontendPresenter(controller, view, new[] { "Knight", "Ranger", "Mage" });
            presenter.Show(Define.FrontendPage.Formation, false, null, 0.7f, true);
            view.SelectHero(Define.UnitRole.Mage);
            view.SelectSlot(Define.BattleSlot.Front);
            view.SelectDifficulty(Define.Difficulty.Easy);
            Assert.That(controller.LastConfiguration.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Mage));
            Assert.That(view.Formation.Slots[0], Does.Contain(GameText.RoleName(Define.UnitRole.Mage)));
            var departures = 0;
            presenter.DepartureRequested += configuration => { departures++; Assert.That(configuration.Difficulty, Is.EqualTo(Define.Difficulty.Easy)); };
            view.Depart();
            view.Depart();
            Assert.That(departures, Is.EqualTo(1));
            Assert.That(view.IsInteractable, Is.False);
        }
        /// <summary>
        /// 解除後の遅いShowや入力がViewと状態を変更しません。
        /// [KO] 해제 후 늦은 Show나 입력이 View와 상태를 변경하지 않는지 확인합니다.
        /// [EN] Checks late Show and input cannot mutate the view or state after disposal.
        /// </summary>
        [Test]
        public void LateShowCannotReviveDisposedPresenter()
        {
            var view = new FakeView();
            var controller = new RunController();
            var presenter = new FrontendPresenter(controller, view, new[] { "K", "R", "M" });
            presenter.Show(Define.FrontendPage.Formation, true, null, 0.7f, true);
            var changes = view.ShowCount;
            presenter.Dispose();
            presenter.Show(Define.FrontendPage.Formation, false, null, 0.2f, true);
            view.SelectHero(Define.UnitRole.Mage);
            view.SelectSlot(Define.BattleSlot.Front);
            Assert.That(view.ShowCount, Is.EqualTo(changes));
            Assert.That(view.IsInteractable, Is.False);
            Assert.That(controller.LastConfiguration.GetRole(Define.BattleSlot.Front), Is.EqualTo(Define.UnitRole.Knight));
        }
        /// <summary>
        /// 戦闘失敗の再試行を拒否し、開始失敗だけを同じ設定で再出発します。
        /// [KO] 전투 실패 재시도를 거부하고 시작 실패만 같은 설정으로 다시 출발하는지 확인합니다.
        /// [EN] Rejects battle-failure retry while allowing startup-failure retry with remembered configuration.
        /// </summary>
        [Test]
        public void RetryRequiresRecoverableErrorPage()
        {
            var view = new FakeView();
            using var presenter = new FrontendPresenter(new RunController(), view, new[] { "K", "R", "M" });
            var count = 0;
            presenter.DepartureRequested += _ => count++;
            presenter.Show(Define.FrontendPage.Error, false, "failure", 0.7f, false);
            view.Retry();
            Assert.That(count, Is.Zero);
            presenter.Show(Define.FrontendPage.Error, false, "failure", 0.7f, true);
            view.Retry();
            view.Retry();
            Assert.That(count, Is.EqualTo(1));
        }
        /// <summary>
        /// 画面を生成せず、入力と表示結果だけを記録するテスト用View。
        /// [KO] 화면을 만들지 않고 입력과 표시 결과만 기록하는 테스트 View입니다.
        /// [EN] Test view recording inputs and display outputs without creating a screen.
        /// </summary>
        private sealed class FakeView : IFrontendView
        {
            public event Action<Define.FrontendPage> PageRequested;
            public event Action<Define.UnitRole> HeroSelected;
            public event Action<Define.BattleSlot> SlotSelected;
            public event Action<Define.Difficulty> DifficultySelected;
            public event Action DepartureRequested;
            public event Action RetryRequested;
            public event Action<float> VolumeRequested;
            public FormationDisplay Formation { get; private set; }
            public bool IsInteractable { get; private set; }
            public int ShowCount { get; private set; }
            /// <summary>
            /// 指定英雄の選択をPresenterへ通知します。
            /// [KO] 지정 영웅 선택을 Presenter에 알립니다.
            /// [EN] Sends hero selection to the presenter.
            /// </summary>
            public void SelectHero(Define.UnitRole value)
                => HeroSelected?.Invoke(value);
            /// <summary>
            /// 指定配置先の入力をPresenterへ通知します。
            /// [KO] 지정 배치 입력을 Presenter에 알립니다.
            /// [EN] Sends a destination-slot input to the presenter.
            /// </summary>
            public void SelectSlot(Define.BattleSlot value)
                => SlotSelected?.Invoke(value);
            /// <summary>
            /// 難易度の選択をPresenterへ通知します。
            /// [KO] 난이도 선택을 Presenter에 알립니다.
            /// [EN] Sends difficulty selection to the presenter.
            /// </summary>
            public void SelectDifficulty(Define.Difficulty value)
                => DifficultySelected?.Invoke(value);
            /// <summary>
            /// 出発ボタンの入力を再現します。
            /// [KO] 출발 버튼 입력을 재현합니다.
            /// [EN] Simulates departure-button input.
            /// </summary>
            public void Depart()
                => DepartureRequested?.Invoke();
            /// <summary>
            /// 再試行ボタンの入力を再現します。
            /// [KO] 재시도 버튼 입력을 재현합니다.
            /// [EN] Simulates retry-button input.
            /// </summary>
            public void Retry()
                => RetryRequested?.Invoke();
            /// <summary>
            /// 画面切替ボタンの入力を再現します。
            /// [KO] 화면 전환 버튼 입력을 재현합니다.
            /// [EN] Simulates page-navigation input.
            /// </summary>
            public void Navigate(Define.FrontendPage value)
                => PageRequested?.Invoke(value);
            /// <summary>
            /// 音量スライダーの入力を再現します。
            /// [KO] 음량 슬라이더 입력을 재현합니다.
            /// [EN] Simulates volume-slider input.
            /// </summary>
            public void ChangeVolume(float value)
                => VolumeRequested?.Invoke(value);
            /// <summary>
            /// 画面更新を数え、解除後の呼出しを検出します。
            /// [KO] 화면 갱신을 세어 해제 후 호출을 감지합니다.
            /// [EN] Counts page updates to detect calls after disposal.
            /// </summary>
            public void ShowPage(Define.FrontendPage page, RunResultDisplay result, string error)
                => ShowCount++;
            /// <summary>
            /// 最後に渡された編成表示を検査用に保存します。
            /// [KO] 마지막 편성 표시를 검사용으로 저장합니다.
            /// [EN] Retains the last formation display for assertions.
            /// </summary>
            public void ShowFormation(FormationDisplay display)
                => Formation = display;
            /// <summary>
            /// Presenterが指定した入力可否を保存します。
            /// [KO] Presenter에서 지정한 입력 가능 상태를 저장합니다.
            /// [EN] Records the input availability chosen by the presenter.
            /// </summary>
            public void SetInteractable(bool value)
                => IsInteractable = value;
            /// <summary>
            /// 再試行表示の要求を受け取ります。入力の許可はPresenter側で検査します。
            /// [KO] 재시도 표시 요청을 받으며 입력 허용은 Presenter 쪽에서 검사합니다.
            /// [EN] Receives retry-display requests; presenter tests check whether retry input is permitted.
            /// </summary>
            public void SetErrorRetry(bool value) { }
            /// <summary>
            /// 音量表示の要求を受け取り、入力を再通知しません。
            /// [KO] 음량 표시 요청을 받고 입력을 다시 알리지 않습니다.
            /// [EN] Receives volume display without sending another input event.
            /// </summary>
            public void SetVolume(float value) { }
        }
    }
}
