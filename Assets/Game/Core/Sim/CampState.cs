using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class CampState
    {
        public int Tier { get; set; }
        public HashSet<string> Guilds { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public Dictionary<string, long> Bag { get; } = new Dictionary<string, long>(System.StringComparer.Ordinal);
    }
}
