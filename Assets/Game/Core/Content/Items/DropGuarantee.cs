namespace Game.Core.Content.Items
{
    public sealed class DropGuarantee
    {
        public DropGuarantee(long stage, ItemSlot? slot, ItemRarity rarity, GuaranteeColor color)
        {
            Stage = stage;
            Slot = slot;
            Rarity = rarity;
            Color = color;
        }

        public long Stage { get; }
        public ItemSlot? Slot { get; }
        public ItemRarity Rarity { get; }
        public GuaranteeColor Color { get; }
        public bool ToEmptySlot => Slot == null;
    }
}
