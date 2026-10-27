using System;
using NUnit.Framework;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.UI;

namespace UnityCombatLab.Tests.EditMode
{
    /// <summary>
    /// Unity の画面を生成せず、Presenter が強化候補から作る表示値を確認します。
    /// [KO] Unity 화면을 생성하지 않고 Presenter가 강화 후보에서 만드는 표시값을 확인합니다.
    /// [EN] Checks display values prepared by the presenter without creating Unity views.
    /// </summary>
    public sealed class RunBattlePresenterTests
    {
        /// <summary>
        /// 六種類の名前・効果単位と、同じ種類だけを合算した選択前後の値を確認します。
        /// [KO] 여섯 종류의 이름·효과 단위와 같은 종류만 합산한 선택 전후 값을 확인합니다.
        /// [EN] Checks all six names and effect units, with before-and-after totals restricted to the offered kind.
        /// </summary>
        [TestCase(Define.RewardKind.Power, "力の紋章", "攻撃力 +16.5%")]
        [TestCase(Define.RewardKind.Vitality, "生命の紋章", "最大HP +16.5%")]
        [TestCase(Define.RewardKind.Haste, "迅速の紋章", "攻撃速度 +16.5%")]
        [TestCase(Define.RewardKind.Barrier, "守護の紋章", "開始時の盾：最大HPの16.5%")]
        [TestCase(Define.RewardKind.Renewal, "再生の紋章", "撃破ごとに最大HPの16.5%を回復")]
        [TestCase(Define.RewardKind.Ambush, "先制の紋章", "開始6秒間 攻撃力 +16.5%")]
        public void DisplayUsesOfferedKindAndCommittedTotals(Define.RewardKind kind, string title, string details)
        {
            var otherKind = kind == Define.RewardKind.Power ? Define.RewardKind.Vitality : Define.RewardKind.Power;
            var current = new RunRewards(new[]
            {
                new RewardChoice(1, kind, 1500),
                new RewardChoice(2, otherKind, 900)
            });
            var display = RunBattlePresenter.CreateRewardDisplay(3, new RewardData(kind, 1500), current, 11000);

            Assert.That(display.Wave, Is.EqualTo(3));
            Assert.That(display.Kind, Is.EqualTo(kind));
            Assert.That(display.Title, Is.EqualTo(title));
            Assert.That(display.Value, Is.EqualTo("+16.5%"));
            Assert.That(display.Details, Is.EqualTo(details));
            Assert.That(display.Totals, Is.EqualTo("15% → 31.5%\n重複：1 / 3 → 2 / 3"));
            Assert.That(display.StacksAfter, Is.EqualTo(2));
            Assert.That(current.History.Count, Is.EqualTo(2));
            Assert.That(current.GetEffect(kind), Is.EqualTo(1500));
        }

        /// <summary>
        /// 過去二回の確定効果を再計算せず、三個目の宝石と実際の選択後の合計を表示します。
        /// [KO] 과거 두 번의 확정 효과를 재계산하지 않고 세 번째 보석과 실제 선택 후 합계를 표시합니다.
        /// [EN] Shows the third gem and the actual post-selection total without recalculating earlier committed effects.
        /// </summary>
        [Test]
        public void ThirdStackPreviewMatchesTheCommittedChoice()
        {
            var current = new RunRewards(new[]
            {
                new RewardChoice(1, Define.RewardKind.Power, 1500),
                new RewardChoice(2, Define.RewardKind.Power, 1650)
            });
            var reward = new RewardData(Define.RewardKind.Power, 1500);
            var display = RunBattlePresenter.CreateRewardDisplay(3, reward, current, 12000);
            var selected = current.Add(new RewardChoice(3, reward.Kind, reward.GetIncrease(12000)));

            Assert.That(display.Value, Is.EqualTo("+18%"));
            Assert.That(display.Totals, Is.EqualTo("31.5% → 49.5%\n重複：2 / 3 → 3 / 3"));
            Assert.That(display.StacksAfter, Is.EqualTo(3));
            Assert.That(selected.GetEffect(reward.Kind), Is.EqualTo(4950));
            Assert.That(selected.GetStacks(reward.Kind), Is.EqualTo(display.StacksAfter));
            Assert.That(current.GetEffect(reward.Kind), Is.EqualTo(3150));
            Assert.That(current.History.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// 基本15bpの1.1倍を17bpへ丸め、初回の表示にもデータの重複上限を使います。
        /// [KO] 기본 15bp의 1.1배를 17bp로 반올림하고 첫 표시에도 데이터의 중첩 상한을 사용합니다.
        /// [EN] Rounds fifteen basis points scaled by 1.1 to seventeen and uses the data's stack limit for the first offer.
        /// </summary>
        [Test]
        public void FirstStackUsesRoundedIncreaseAndConfiguredLimit()
        {
            var display = RunBattlePresenter.CreateRewardDisplay(1,
                new RewardData(Define.RewardKind.Power, 15, 1), RunRewards.Empty, 11000);

            Assert.That(display.Value, Is.EqualTo("+0.17%"));
            Assert.That(display.Details, Is.EqualTo("攻撃力 +0.17%"));
            Assert.That(display.Totals, Is.EqualTo("0% → 0.17%\n重複：0 / 1 → 1 / 1"));
            Assert.That(display.StacksAfter, Is.EqualTo(1));
            Assert.That(RunRewards.Empty.History, Is.Empty);
        }

        /// <summary>
        /// 表示に必要な入力の欠落と、Domain が認めない強化倍率を拒否します。
        /// [KO] 표시에 필요한 입력 누락과 Domain이 허용하지 않는 강화 배율을 거부합니다.
        /// [EN] Rejects missing display inputs and reward strength outside the domain's accepted range.
        /// </summary>
        [Test]
        public void MissingInputsAndInvalidStrengthAreRejected()
        {
            var reward = new RewardData(Define.RewardKind.Power, 1500);
            Assert.Throws<ArgumentNullException>(() => RunBattlePresenter.CreateRewardDisplay(1, null, RunRewards.Empty, 10000));
            Assert.Throws<ArgumentNullException>(() => RunBattlePresenter.CreateRewardDisplay(1, reward, null, 10000));
            Assert.Throws<ArgumentOutOfRangeException>(() => RunBattlePresenter.CreateRewardDisplay(1, reward, RunRewards.Empty, 0));
        }
    }
}
