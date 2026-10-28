using System.Collections.Generic;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public sealed class WeaponState
    {
        public WeaponItem? Right { get; set; }
        public WeaponItem? Left { get; set; }
        public List<WeaponItem> Bag { get; } = new List<WeaponItem>();
        public bool StarterGiven { get; set; }

        public WeaponItem? In(WeaponHand hand) => hand == WeaponHand.Right ? Right : Left;

        public void Set(WeaponHand hand, WeaponItem? weapon)
        {
            if (hand == WeaponHand.Right)
                Right = weapon;
            else
                Left = weapon;
        }

        public bool Holds(string typeId) =>
            (Right != null && Right.Counts(typeId)) || (Left != null && Left.Counts(typeId));

        public bool Opens(string skillId, int heroLevel, IReadOnlyList<int> unlockLevels) =>
            (Right != null && Right.Opens(skillId, heroLevel, unlockLevels)) ||
            (Left != null && Left.Opens(skillId, heroLevel, unlockLevels));
    }
}
