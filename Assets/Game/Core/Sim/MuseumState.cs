using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class MuseumState
    {
        public HashSet<string> Done { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public List<string> Shelf { get; } = new List<string>();
        public string Title { get; set; } = string.Empty;
        public HashSet<string> Reported { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public double[] Perks { get; } = new double[MuseumRules.PerkCount];

        public double Perk(MuseumPerk perk) => Perks[(int)perk];
    }
}
