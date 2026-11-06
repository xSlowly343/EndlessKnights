using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class ConsonanceDef
    {
        public ConsonanceDef(string armor, string jewelry, IReadOnlyList<Modifier> lines)
        {
            Armor = armor;
            Jewelry = jewelry;
            Lines = lines;
        }

        public string Armor { get; }
        public string Jewelry { get; }
        public IReadOnlyList<Modifier> Lines { get; }
        public IReadOnlyList<Modifier> Fixed { get; internal set; } = System.Array.Empty<Modifier>();
        public IReadOnlyList<Modifier> PerMuseumCollection { get; internal set; } = System.Array.Empty<Modifier>();
        public IReadOnlyList<PerkValue> Perks { get; internal set; } = System.Array.Empty<PerkValue>();
        public double PerkBoost { get; internal set; }
        public bool NightOffline { get; internal set; }
        public int FateOptions { get; internal set; }
    }
}
