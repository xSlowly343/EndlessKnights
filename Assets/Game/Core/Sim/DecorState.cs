using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class DecorState
    {
        public HashSet<string> Owned { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public List<string> Placed { get; } = new List<string>();
        public string Theme { get; set; } = string.Empty;
        public long GuestsWatered { get; set; }
        public long GuestHammers { get; set; }
        public long Gifts { get; set; }
    }
}
