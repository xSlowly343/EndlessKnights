using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class HideoutState
    {
        public int Tier { get; set; }
        public int Members { get; set; }
        public long Bosses { get; set; }
        public int Orders { get; set; }
        public long SitSeconds { get; set; }
        public int FireLevel { get; set; }
        public List<string> Here { get; } = new List<string>();
    }
}
