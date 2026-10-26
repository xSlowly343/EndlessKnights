using System;
using System.Collections.Generic;

namespace Game.Core.Content.Items
{
    public static class ItemRarityNames
    {
        public static readonly IReadOnlyDictionary<string, ItemRarity> ByName = Build();

        public static string Of(ItemRarity rarity) => rarity.ToString().ToLowerInvariant();

        private static Dictionary<string, ItemRarity> Build()
        {
            var names = new Dictionary<string, ItemRarity>(StringComparer.Ordinal);
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
                names[Of(rarity)] = rarity;
            return names;
        }
    }
}
