using Game.Core.Content.Items;

namespace Game.Core.Sim
{
    public sealed class GearState
    {
        public const int Slots = 7;

        public GearItem?[] Worn { get; } = new GearItem?[Slots];

        public GearItem? In(GearSlot slot) => Worn[(int)slot];

        public static GearSlot SlotFor(ItemSlot slot, bool rightRing = false) => slot switch
        {
            ItemSlot.Helmet => GearSlot.Helmet,
            ItemSlot.Chest => GearSlot.Chest,
            ItemSlot.Gloves => GearSlot.Gloves,
            ItemSlot.Boots => GearSlot.Boots,
            ItemSlot.Amulet => GearSlot.Amulet,
            _ => rightRing ? GearSlot.RingRight : GearSlot.RingLeft
        };

        public static bool Fits(GearSlot slot, ItemSlot itemSlot) =>
            itemSlot != ItemSlot.Weapon && SlotFor(itemSlot, slot == GearSlot.RingRight) == slot;
    }
}
