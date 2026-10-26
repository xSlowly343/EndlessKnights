using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public sealed class ItemDrop
    {
        public ItemDrop(DropSource source, long stage, WeaponItem? weapon, GearItem? gear, bool salvaged, double dust)
        {
            Source = source;
            Stage = stage;
            Weapon = weapon;
            Gear = gear;
            Salvaged = salvaged;
            Dust = dust;
        }

        public DropSource Source { get; }
        public long Stage { get; }
        public WeaponItem? Weapon { get; }
        public GearItem? Gear { get; }
        public double Dust { get; }
        public bool Salvaged { get; }
        public ItemSlot Slot => Gear?.Slot ?? ItemSlot.Weapon;
        public ItemRarity Rarity => Weapon?.Rarity ?? Gear!.Rarity;
    }
}
