using System.Collections.Generic;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public sealed class WeaponState
    {
        public WeaponBase? Right { get; set; }
        public WeaponBase? Left { get; set; }
        public List<WeaponBase> Bag { get; } = new List<WeaponBase>();
        public bool StarterGiven { get; set; }

        public WeaponBase? In(WeaponHand hand) => hand == WeaponHand.Right ? Right : Left;

        public void Set(WeaponHand hand, WeaponBase? weapon)
        {
            if (hand == WeaponHand.Right)
                Right = weapon;
            else
                Left = weapon;
        }

        public bool Holds(string typeId) =>
            (Right != null && string.Equals(Right.Type.Id, typeId, System.StringComparison.Ordinal)) ||
            (Left != null && string.Equals(Left.Type.Id, typeId, System.StringComparison.Ordinal));
    }
}
