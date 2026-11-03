using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public readonly struct PendingDrop
    {
        private PendingDrop(DropSource source, long stage, ItemSlot slot, ItemRarity rarity, WeaponType? type, string? color, bool key = false)
        {
            IsKey = key;
            Source = source;
            Stage = stage;
            Slot = slot;
            Rarity = rarity;
            Type = type;
            Color = color;
        }

        public DropSource Source { get; }
        public long Stage { get; }
        public ItemSlot Slot { get; }
        public ItemRarity Rarity { get; }
        public WeaponType? Type { get; }
        public string? Color { get; }
        public bool IsKey { get; }

        public static PendingDrop Weapon(DropSource source, long stage, ItemRarity rarity, WeaponType type) =>
            new PendingDrop(source, stage, ItemSlot.Weapon, rarity, type, null);

        public static PendingDrop Gear(DropSource source, long stage, ItemSlot slot, ItemRarity rarity, string? color) =>
            new PendingDrop(source, stage, slot, rarity, null, color);

        public static PendingDrop Key(long stage) =>
            new PendingDrop(DropSource.Record, stage, ItemSlot.Weapon, ItemRarity.Simple, null, null, key: true);
    }
}
