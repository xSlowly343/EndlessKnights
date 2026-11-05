using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class BestiaryState
    {
        public const int Capacity = 256;

        public Dictionary<string, long> Kills { get; } = new Dictionary<string, long>(Capacity, System.StringComparer.Ordinal);
        public HashSet<string> Opened { get; } = new HashSet<string>(Capacity, System.StringComparer.Ordinal);
    }
}
