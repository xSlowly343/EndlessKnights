using System;
using System.Collections.Generic;

namespace Game.Core.Ui
{
    public static class PanelSections
    {
        public static readonly IReadOnlyList<PanelSection> All = (PanelSection[])Enum.GetValues(typeof(PanelSection));

        public static readonly IReadOnlyList<PanelSection> DefaultBar = new[]
        {
            PanelSection.Hero,
            PanelSection.Book,
            PanelSection.Tree,
            PanelSection.Items,
            PanelSection.Party,
        };

        private static readonly string[] NoSubTabs = Array.Empty<string>();
        private static readonly string[] HeroSubTabs = { "Обзор", "Статы", "Ввод" };
        private static readonly string[] BookSubTabs = { "Скиллы", "Цвета поддержек" };
        private static readonly string[] TreeSubTabs = { "Дерево", "Подкласс", "Самоцветы" };
        private static readonly string[] ItemsSubTabs = { "Сумка", "Артефакты", "Разбор" };
        private static readonly string[] PartySubTabs = { "Отряд", "Друзья", "Клан", "Поиск" };

        public static string Title(PanelSection section) => section switch
        {
            PanelSection.Hero => "Герой",
            PanelSection.Book => "Книга",
            PanelSection.Tree => "Дерево",
            PanelSection.Items => "Предметы",
            PanelSection.Party => "Отряд",
            PanelSection.Portal => "Портал",
            PanelSection.Stats => "Статистика",
            PanelSection.Camp => "Лагерь",
            PanelSection.Shop => "Магазин",
            PanelSection.Settings => "Настройки",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
        };

        public static IReadOnlyList<string> SubTabs(PanelSection section) => section switch
        {
            PanelSection.Hero => HeroSubTabs,
            PanelSection.Book => BookSubTabs,
            PanelSection.Tree => TreeSubTabs,
            PanelSection.Items => ItemsSubTabs,
            PanelSection.Party => PartySubTabs,
            _ => NoSubTabs,
        };

        public static PanelHeight DefaultHeight(PanelSection section) =>
            section == PanelSection.Portal ? PanelHeight.Half : PanelHeight.Full;

        public static bool CanBeWide(PanelSection section) => section == PanelSection.Tree;
    }
}
