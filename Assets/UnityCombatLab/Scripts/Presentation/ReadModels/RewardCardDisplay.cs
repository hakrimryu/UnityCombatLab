using System;
using UnityCombatLab.Common;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// Presenter が作った一枚分の文章・宝石数と、選択時に返す識別子を保持します。
    /// [KO] Presenter가 만든 카드 한 장의 문구·보석 수와 선택 시 반환할 식별자를 보관합니다.
    /// [EN] Holds one card's text and gem count prepared by the presenter, plus the identity returned on selection.
    /// </summary>
    public sealed class RewardCardDisplay
    {
        /// <summary>
        /// この候補を提示したクリア済みウェーブ。
        /// [KO] 이 후보를 제시한 클리어 웨이브입니다.
        /// [EN] Cleared wave that offered this choice.
        /// </summary>
        public int Wave { get; }

        /// <summary>
        /// アイコン・色の選択と、クリック通知に使う強化種類。
        /// [KO] 아이콘·색상 선택과 클릭 알림에 사용하는 강화 종류입니다.
        /// [EN] Reward kind used for artwork selection and click notification.
        /// </summary>
        public Define.RewardKind Kind { get; }

        /// <summary>
        /// カードの見出しへ表示する名前。
        /// [KO] 카드 머리글에 표시할 이름입니다.
        /// [EN] Name displayed in the card heading.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 数字専用ラベルへ表示する今回の増加率。
        /// [KO] 숫자 전용 라벨에 표시할 이번 증가율입니다.
        /// [EN] This offer's increase formatted for the numeric label.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 効果の対象・単位・期間を含む説明文。
        /// [KO] 효과 대상·단위·기간을 포함한 설명입니다.
        /// [EN] Effect description including its target, unit and duration.
        /// </summary>
        public string Details { get; }

        /// <summary>
        /// 選択前後の累積率と重複数を示す文章。
        /// [KO] 선택 전후의 누적 비율·중첩 수를 나타내는 문구입니다.
        /// [EN] Text showing cumulative percentages and stacks before and after selection.
        /// </summary>
        public string Totals { get; }

        /// <summary>
        /// 選択後の重複数として点灯する宝石の数。
        /// [KO] 선택 후 중첩 수로 표시할 보석 개수입니다.
        /// [EN] Number of gems lit to show the stack count after selection.
        /// </summary>
        public int StacksAfter { get; }

        /// <summary>
        /// 計算済みの表示値を保存し、選択後もこのカードの内容を変えません。
        /// [KO] 계산한 표시값을 저장하고 선택 후에도 이 카드 내용을 바꾸지 않습니다.
        /// [EN] Stores prepared display values so this card's content remains unchanged after selection.
        /// </summary>
        public RewardCardDisplay(int wave, Define.RewardKind kind, string title, string value, string details, string totals, int stacksAfter)
        {
            if (wave < 1 || wave >= Define.WavesPerRun)
                throw new ArgumentOutOfRangeException(nameof(wave));

            if ((uint)kind > (uint)Define.RewardKind.Ambush)
                throw new ArgumentOutOfRangeException(nameof(kind));

            if (stacksAfter < 1 || stacksAfter > 3)
                throw new ArgumentOutOfRangeException(nameof(stacksAfter));

            Wave = wave;
            Kind = kind;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Details = details ?? throw new ArgumentNullException(nameof(details));
            Totals = totals ?? throw new ArgumentNullException(nameof(totals));
            StacksAfter = stacksAfter;
        }
    }
}
