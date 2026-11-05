using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class CosmeticState
    {
        public HashSet<string> Owned { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public Dictionary<string, string> Equipped { get; } = new Dictionary<string, string>(System.StringComparer.Ordinal);
    }
}
