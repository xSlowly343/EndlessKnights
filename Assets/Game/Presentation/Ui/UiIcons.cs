using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Ui;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public static class UiIcons
    {
        public const string More = "more";
        public const string Gold = "gold";

        private const string Root = "Ui/Icons/";
        private const string Tabs = "tabs";
        private const string Currencies = "currencies";
        private const string Slots = "slots";
        private const string Filters = "filters";
        private const string ArmorSlot = "armor";
        private const string ArmorIcon = "chest";

        private static readonly Dictionary<string, Sprite?> Loaded = new Dictionary<string, Sprite?>();

        public static Sprite? Tab(PanelSection section) => Get(Tabs, PanelSections.Key(section));

        public static Sprite? Tab(string id) => Get(Tabs, id);

        public static Sprite? Currency(string id) => Get(Currencies, id);

        public static Sprite? Slot(string slot) => Get(Slots, slot == ArmorSlot ? ArmorIcon : slot);

        public static Sprite? Filter(GemBagFilter filter) => Get(Filters, filter.ToString().ToLowerInvariant());

        public static Sprite? Get(string group, string id)
        {
            var path = Root + group + "/" + id;
            if (!Loaded.TryGetValue(path, out var sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                Loaded[path] = sprite;
            }
            return sprite;
        }
    }
}
