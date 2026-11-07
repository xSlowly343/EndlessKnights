using System.Collections.Generic;

namespace Game.Core.Sim
{
    public enum DigPhase
    {
        Idle,
        Brushing,
        Exposed
    }

    public sealed class DigState
    {
        public List<string> Sites { get; } = new List<string>(Content.DigRules.SiteCapacity);
        public DigPhase Phase { get; set; }
        public double Brushed { get; set; }
        public double Quality { get; set; } = 1;
        public long Dug { get; set; }
        public long Cracks { get; set; }
        public string LastSite { get; set; } = string.Empty;
        public int LastBones { get; set; }
        public HashSet<string> Skeletons { get; } = new HashSet<string>(System.StringComparer.Ordinal);
    }
}
