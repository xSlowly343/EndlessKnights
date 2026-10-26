using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public readonly struct PendingDrop
    {
        private PendingDrop(DropSource source, long stage, ItemSlot slot, ItemRarity rarity, WeaponType? type, string? color, GemDef? gem)
        {
            Source = source;
            Stage = stage;
            Slot = slot;
            Rarity = rarity;
            Type = type;
            Color = color;
            Gem = gem;
        }

        public DropSource Source { get; }
        public long Stage { get; }
        public ItemSlot Slot { get; }
        public ItemRarity Rarity { get; }
        public WeaponType? Type { get; }
        public string? Color { get; }
        public GemDef? Gem { get; }

        public static PendingDrop Weapon(DropSource source, long stage, ItemRarity rarity, WeaponType type) =>
            new PendingDrop(source, stage, ItemSlot.Weapon, rarity, type, null, null);

        public static PendingDrop Gear(DropSource source, long stage, ItemSlot slot, ItemRarity rarity, string? color) =>
            new PendingDrop(source, stage, slot, rarity, null, color, null);

        public static PendingDrop OfGem(GemDef gem) =>
            new PendingDrop(DropSource.Record, 0, ItemSlot.Weapon, ItemRarity.Simple, null, null, gem);
    }
}
