using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class PeacefulWork
    {
        public double Keys { get; set; }
        public double Clicks { get; set; }
        public double MouseMeters { get; set; }
        public double IdleSeconds { get; set; }
    }

    public sealed class PeacefulState
    {
        public PeacefulMode Mode { get; set; }
        public string Activity { get; set; } = string.Empty;
        public HashSet<string> Presets { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public string ScheduledBy { get; set; } = string.Empty;
        public Dictionary<string, PeacefulWork> Work { get; } = new Dictionary<string, PeacefulWork>(System.StringComparer.Ordinal);
        public bool TeaPause { get; set; } = true;
        public double BattleSeconds { get; set; }
        public double QuietSeconds { get; set; }
        public bool TeaOffered { get; set; }
        public long UnsentActions { get; set; }
        public double ActionCarry { get; set; }
    }
}
