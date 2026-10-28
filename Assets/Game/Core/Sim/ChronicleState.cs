using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class ChronicleState
    {
        public string? Active { get; set; }
        public int ActivePrestige { get; set; }
        public bool SkillsLocked { get; set; }
        public List<string> Completed { get; } = new List<string>();
    }
}
