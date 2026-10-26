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
        }

        public ItemSlot Slot { get; }
        public ItemRarity Rarity { get; }
        public int ItemLevel { get; }
        public string? Color { get; }
    }
}
