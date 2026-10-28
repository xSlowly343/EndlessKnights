using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class JewelItem
    {
        public JewelItem(ItemRarity rarity, int itemLevel, IReadOnlyList<GearAffix> affixes)
        {
            Rarity = rarity;
            ItemLevel = itemLevel;
            Affixes = affixes;
        }

        public ItemRarity Rarity { get; }
        public int ItemLevel { get; }
        public IReadOnlyList<GearAffix> Affixes { get; }

        public void AddModifiers(List<Modifier> modifiers, double scale)
        {
            foreach (var affix in Affixes)
            {
                foreach (var stat in affix.Def.Stats)
                    modifiers.Add(new Modifier(stat.Stat, stat.Kind, affix.Value * scale, stat.Tags));
            }
        }
    }
}
