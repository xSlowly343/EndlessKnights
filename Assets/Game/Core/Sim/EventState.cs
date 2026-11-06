using System.Collections.Generic;
using Game.Core.Sim.Rift;

namespace Game.Core.Sim
{
    public sealed class EventState
    {
        public string WindowId { get; set; } = string.Empty;
        public long UnsentTokens { get; set; }
        public long Tokens { get; set; }
        public long Guests { get; set; }
        public long BossFights { get; set; }
        public long BossWins { get; set; }
        public long DailyQuests { get; set; }
        public long PartyTicks { get; set; }
        public long Spawns { get; set; }
        public bool Guest { get; set; }
        public long BossDay { get; set; } = long.MinValue;
        public int BossFightsToday { get; set; }
        public HashSet<string> GoalsDone { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public HashSet<string> GoalsSent { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public bool FightInside { get; set; }
        public StageProgress? FightHome { get; set; }
        public long FightStage { get; set; }
        public int FightTicksLeft { get; set; }
        public long UnsentScore { get; set; }
        public long BestScore { get; set; }
    }
}
