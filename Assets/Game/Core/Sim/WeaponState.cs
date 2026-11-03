using System.Collections.Generic;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public sealed class WeaponState
    {
        public WeaponItem? Held { get; set; }
        public List<WeaponItem> Bag { get; } = new List<WeaponItem>();
        public bool StarterGiven { get; set; }

        public bool Opens(string skillId) => Held != null && Held.Opens(skillId);
    }
}
