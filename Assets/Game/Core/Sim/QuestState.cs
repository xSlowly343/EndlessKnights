using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class QuestEntry
    {
        public QuestKind Kind { get; set; }
        public long Day { get; set; }
        public double Target { get; set; }
        public double Progress { get; set; }
        public bool Claimed { get; set; }
        public bool Done => Progress >= Target - 1e-9;
    }

    public sealed class QuestState
    {
        public List<QuestEntry> Daily { get; } = new List<QuestEntry>();
        public List<QuestEntry> Weekly { get; } = new List<QuestEntry>();
        public long Day { get; set; } = -1;
        public long Week { get; set; } = -1;
        public int Streak { get; set; }
        public int SwapsLeft { get; set; }
        public int Swaps { get; set; }
        public double[] Last { get; } = new double[QuestRules.KindCount];
        public bool Primed { get; set; }
    }
}
