using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class Potion
    {
        public string Recipe { get; set; } = string.Empty;
        public bool Fine { get; set; }
        public bool Unknown { get; set; }
        public double Lasting { get; set; } = 1;
    }

    public sealed class AlchemyState
    {
        public HashSet<string> Known { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public List<Potion> SharedBrews { get; } = new List<Potion>();
        public Potion? Brewing { get; set; }
        public double Stirs { get; set; }
        public List<Potion> Potions { get; } = new List<Potion>();
        public Potion? Active { get; set; }
        public long ActiveTicksLeft { get; set; }
    }
}
