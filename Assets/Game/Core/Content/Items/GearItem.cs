using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class GearItem
    {
        public GearItem(ItemSlot slot, ItemRarity rarity, int itemLevel, string? color)
        {
            Slot = slot;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Color = color;
            Affixes = Array.Empty<GearAffix>();
        }

        public GearItem(GearBase gearBase, ItemRarity rarity, int itemLevel, IReadOnlyList<GearAffix> affixes, SetDef? set = null)
        {
            Set = set;
            Base = gearBase;
            Slot = gearBase.Slot;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Color = gearBase.Color;
            Affixes = affixes;
        }

        public GearBase? Base { get; }
        public ItemSlot Slot { get; }
        public ItemRarity Rarity { get; }
        public int ItemLevel { get; }
        public string? Color { get; }
        public IReadOnlyList<GearAffix> Affixes { get; }
        public SetDef? Set { get; }
        public bool Wearable => Base != null;

        public void AddModifiers(List<Modifier> modifiers)
        {
            if (Base == null)
                return;
            modifiers.AddRange(Base.Implicit);
            foreach (var affix in Affixes)
                affix.AddModifiers(modifiers);
            if (Set != null)
                modifiers.AddRange(Set.PieceLines(Slot));
        }
    }
}
