using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public sealed class PartyMemberResult
    {
        public PartyMemberResult(HeroClass heroClass, long finalStage, long highestStage, long kills, int bossFails, BigNumber goldEarned, double shareOfKills, long missedKills = 0)
        {
            HeroClass = heroClass;
            FinalStage = finalStage;
            HighestStage = highestStage;
            Kills = kills;
            BossFails = bossFails;
            GoldEarned = goldEarned;
            ShareOfKills = shareOfKills;
            MissedKills = missedKills;
        }

        public HeroClass HeroClass { get; }
        public long FinalStage { get; }
        public long HighestStage { get; }
        public long Kills { get; }
        public int BossFails { get; }
        public BigNumber GoldEarned { get; }
        public double ShareOfKills { get; }
        public long MissedKills { get; }
    }
}
