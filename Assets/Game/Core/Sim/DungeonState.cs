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
        public bool Inside => Active != null;
    }
}
