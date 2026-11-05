using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class ChronicleState
    {
        public string? Active { get; set; }
        public int ActivePrestige { get; set; }
        public bool SkillsLocked { get; set; }
        public List<string> Completed { get; } = new List<string>();
        public bool Weekly { get; set; }
        public long Week { get; set; } = -1;
        public Dictionary<string, long> WeeklyBest { get; } = new Dictionary<string, long>(System.StringComparer.Ordinal);
        public List<string> WeeklyUnsent { get; } = new List<string>();
    }
}
