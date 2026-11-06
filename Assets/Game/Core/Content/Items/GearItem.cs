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
            Forge = new ForgeMark();
        }

        public GearItem(GearBase gearBase, ItemRarity rarity, int itemLevel, IReadOnlyList<GearAffix> affixes, SetDef? set = null, GearImplicit? rolledImplicit = null, ForgeMark? forge = null)
        {
            Forge = forge ?? new ForgeMark();
            RolledImplicit = rolledImplicit;
            Set = set;
            Base = gearBase;
            Slot = gearBase.Slot;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Color = gearBase.Color;
            Affixes = affixes;
        }

        public GearItem(UniqueDef unique, int itemLevel, ForgeMark? forge = null)
        {
            Forge = forge ?? new ForgeMark();
            Unique = unique;
            Base = unique.Base;
            Slot = unique.Slot;
            Rarity = unique.Rarity;
            ItemLevel = itemLevel;
            Color = unique.Base.Color;
            Affixes = Array.Empty<GearAffix>();
        }

        public GearBase? Base { get; }
        public UniqueDef? Unique { get; }
        public ItemSlot Slot { get; }
        public ItemRarity Rarity { get; }
        public int ItemLevel { get; }
        public string? Color { get; }
        public IReadOnlyList<GearAffix> Affixes { get; }
        public SetDef? Set { get; }
        public GearImplicit? RolledImplicit { get; }
        public ForgeMark Forge { get; }
        public bool Wearable => Base != null;

        public GearItem WithAffixes(IReadOnlyList<GearAffix> affixes) =>
            Base == null || Unique != null ? this : new GearItem(Base, Rarity, ItemLevel, affixes, Set, RolledImplicit, Forge);

        public void AddModifiers(List<Modifier> modifiers)
        {
            if (Base == null)
                return;
            AddScaled(modifiers, Base.Implicit);
            if (RolledImplicit != null)
                AddScaled(modifiers, RolledImplicit.Lines);
            var start = modifiers.Count;
            foreach (var affix in Affixes)
                affix.AddModifiers(modifiers);
            Scale(modifiers, start, Forge.AffixScale);
            if (Set != null)
                modifiers.AddRange(Set.PieceLines(Slot));
            if (Unique != null)
                modifiers.AddRange(Unique.Lines);
        }

        public static void Scale(List<Modifier> modifiers, int start, double scale)
        {
            if (scale == 1)
                return;
            for (var i = start; i < modifiers.Count; i++)
                modifiers[i] = modifiers[i].WithValue(modifiers[i].Value * scale);
        }

        private void AddScaled(List<Modifier> modifiers, IReadOnlyList<Modifier> lines)
        {
            var scale = Forge.Scale;
            foreach (var line in lines)
                modifiers.Add(scale == 1 ? line : line.WithValue(line.Value * scale));
        }
    }
}
