using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public readonly struct PendingDrop
    {
        private PendingDrop(DropSource source, long stage, ItemSlot slot, ItemRarity rarity, WeaponType? type, string? color, GemDef? gem, bool jewel = false)
        {
            IsJewel = jewel;
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
        public bool IsJewel { get; }

        public static PendingDrop Weapon(DropSource source, long stage, ItemRarity rarity, WeaponType type) =>
            new PendingDrop(source, stage, ItemSlot.Weapon, rarity, type, null, null);

        public static PendingDrop Gear(DropSource source, long stage, ItemSlot slot, ItemRarity rarity, string? color) =>
            new PendingDrop(source, stage, slot, rarity, null, color, null);

        public static PendingDrop Jewel(DropSource source, long stage, ItemRarity rarity) =>
            new PendingDrop(source, stage, ItemSlot.Weapon, rarity, null, null, null, jewel: true);

        public static PendingDrop OfGem(GemDef gem) =>
            new PendingDrop(DropSource.Record, 0, ItemSlot.Weapon, ItemRarity.Simple, null, null, gem);
    }
}
