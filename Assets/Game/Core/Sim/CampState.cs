using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class CampState
    {
        public const int BlueprintCapacity = 64;

        public int Tier { get; set; }
        public HashSet<string> Guilds { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public Dictionary<string, long> Bag { get; } = new Dictionary<string, long>(System.StringComparer.Ordinal);
        public Dictionary<string, int> Levels { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public Dictionary<string, string> Branches { get; } = new Dictionary<string, string>(System.StringComparer.Ordinal);
        public List<string> Street { get; } = new List<string>();
        public HashSet<string> Modules { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public HashSet<string> Blueprints { get; } = new HashSet<string>(BlueprintCapacity, System.StringComparer.Ordinal);
        public Dictionary<string, int> Installed { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
    }
}
