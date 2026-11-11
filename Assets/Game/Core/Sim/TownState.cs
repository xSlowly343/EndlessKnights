using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class TownOrder
    {
        public string Request { get; set; } = string.Empty;
        public int Amount { get; set; }
        public long Day { get; set; }
    }

    public sealed class TownState
    {
        public long Day { get; set; } = -1;
        public List<TownOrder> Orders { get; } = new List<TownOrder>();
        public Dictionary<string, long> Reputation { get; } = new Dictionary<string, long>(System.StringComparer.Ordinal);
        public long Done { get; set; }
        public long RedrawDay { get; set; } = -1;
        public List<string> Gifts { get; } = new List<string>();
    }
}
