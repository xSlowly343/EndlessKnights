using System;
using System.Collections.Generic;
using Game.Core.Text;

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

        public static string Title(PanelSection section)
        {
            if (!Enum.IsDefined(typeof(PanelSection), section))
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
            return Texts.Get("panel.section." + Key(section));
        }

        public static IReadOnlyList<string> SubTabs(PanelSection section)
        {
            var count = SubTabCount(section);
            var tabs = new string[count];
            for (var i = 0; i < count; i++)
                tabs[i] = Texts.Get("panel.tab." + Key(section) + "." + i);
            return tabs;
        }

        private static int SubTabCount(PanelSection section) => section switch
        {
            PanelSection.Hero => 3,
            PanelSection.Book => 3,
            PanelSection.Tree => 3,
            PanelSection.Items => 3,
            PanelSection.Party => 4,
            PanelSection.Settings => 3,
            _ => 0,
        };

        public static string Key(PanelSection section)
        {
            var name = section.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static bool TryParse(string key, out PanelSection section)
        {
            foreach (var candidate in All)
            {
                if (Key(candidate) != key)
                    continue;
                section = candidate;
                return true;
            }
            section = default;
            return false;
        }

        public static PanelHeight DefaultHeight(PanelSection section) =>
            section == PanelSection.Portal ? PanelHeight.Half : PanelHeight.Full;

        public static bool CanBeWide(PanelSection section) => section == PanelSection.Tree;
    }
}
