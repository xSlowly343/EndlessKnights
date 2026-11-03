using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public sealed class RerollOffer
    {
        public RerollOffer(WeaponItem? weapon, GearItem? gear, IReadOnlyList<GearAffix> before, IReadOnlyList<GearAffix> after, int? locked, double paid)
        {
            Weapon = weapon;
            Gear = gear;
            Before = before;
            After = after;
            Locked = locked;
            Paid = paid;
        }

        public WeaponItem? Weapon { get; }
        public GearItem? Gear { get; }
        public IReadOnlyList<GearAffix> Before { get; }
        public IReadOnlyList<GearAffix> After { get; }
        public int? Locked { get; }
        public double Paid { get; }
    }
}
