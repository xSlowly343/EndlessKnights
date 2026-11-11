using System.Collections.Generic;
using Game.Core.Sim.Rift;

namespace Game.Core.Sim
{
    public sealed class DungeonState
    {
        public List<DungeonKey> Keys { get; } = new List<DungeonKey>();
        public DungeonKey? Active { get; set; }
        public long StartStage { get; set; }
        public StageProgress? Home { get; set; }
        public long Completed { get; set; }
        public long Failed { get; set; }
        public int BestTier { get; set; }
        public long Salvaged { get; set; }
        public long Merged { get; set; }
        public int BossesWithoutKey { get; set; }
        public bool Auto { get; set; }
        public int AutoMinTier { get; set; } = 1;
        public bool AutoMerge { get; set; }
        public List<string> AutoSkip { get; } = new List<string>();
        public bool Party { get; set; }
        public bool Borrowed { get; set; }
        public bool Inside => Active != null;
    }
}
