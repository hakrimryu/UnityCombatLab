using System;
using System.Collections.Generic;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Presentation.UI;

namespace UnityCombatLab.Presentation.ReadModels
{
    /// <summary>
    /// 英雄の説明・選択状態・配置・難易度を表示値へ変換し、Viewによるゲーム規則の解釈を不要にします。
    /// [KO] 영웅 설명·선택 상태·배치·난이도를 표시 값으로 변환하여 View가 게임 규칙을 해석할 필요를 없앱니다.
    /// [EN] Converts hero descriptions, selection, slots and difficulty to display values so the view needs no gameplay interpretation.
    /// </summary>
    public sealed class FormationDisplay
    {
        /// <summary>
        /// 騎士・弓使い・魔法使いの順のカード説明。
        /// [KO] 기사·궁수·마법사 순서의 카드 설명입니다.
        /// [EN] Hero descriptions in knight, ranger and mage order.
        /// </summary>
        public IReadOnlyList<string> Heroes { get; }

        /// <summary>
        /// 騎士・弓使い・魔法使いの順に、カードの色とバッジへ反映する選択状態。
        /// [KO] 기사·궁수·마법사 순서로 카드 색상과 배지에 반영할 선택 상태입니다.
        /// [EN] Selection states applied to card colors and badges in knight, ranger and mage order.
        /// </summary>
        public IReadOnlyList<bool> HeroSelections { get; }

        /// <summary>
        /// 前列・後列上・後列下の順の配置名と英雄名。
        /// [KO] 전열·위쪽 후열·아래쪽 후열 순서의 배치 이름과 영웅 이름입니다.
        /// [EN] Slot and hero names in front, upper-back and lower-back order.
        /// </summary>
        public IReadOnlyList<string> Slots { get; }

        /// <summary>
        /// 標準・やさしいの順の選択印と説明。
        /// [KO] 표준·쉬움 순서의 선택 표시와 설명입니다.
        /// [EN] Selection marks and descriptions in standard and easy order.
        /// </summary>
        public IReadOnlyList<string> Difficulties { get; }

        /// <summary>
        /// Presenterの現在の選択から、変更されない一画面分の表示値を作ります。
        /// [KO] Presenter의 현재 선택으로 변경되지 않는 한 화면의 표시 값을 만듭니다.
        /// [EN] Creates immutable display values for one screen from the presenter's current selection.
        /// </summary>
        public FormationDisplay(RunConfiguration configuration, Define.UnitRole? selected, IReadOnlyList<string> descriptions)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (descriptions == null || descriptions.Count != Define.SlotsPerTeam)
                throw new ArgumentException("Three hero descriptions are required.", nameof(descriptions));

            var heroes = new string[Define.SlotsPerTeam];
            var heroSelections = new bool[Define.SlotsPerTeam];
            var slots = new string[Define.SlotsPerTeam];

            for (var index = 0; index < heroes.Length; index++)
            {
                heroes[index] = descriptions[index];
                heroSelections[index] = selected == (Define.UnitRole)index;
                var slot = (Define.BattleSlot)index;
                slots[index] = $"{GameText.SlotName(slot)}\n{GameText.RoleName(configuration.GetRole(slot))}";
            }

            Heroes = Array.AsReadOnly(heroes);
            HeroSelections = Array.AsReadOnly(heroSelections);
            Slots = Array.AsReadOnly(slots);
            Difficulties = Array.AsReadOnly(new[]
            {
                GameText.DifficultyOption(Define.Difficulty.Standard, configuration.Difficulty),
                GameText.DifficultyOption(Define.Difficulty.Easy, configuration.Difficulty)
            });
        }
    }
}
