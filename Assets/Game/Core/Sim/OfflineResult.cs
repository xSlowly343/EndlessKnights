using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class OfflineResult
    {
        public OfflineResult(double seconds, long kills, BigNumber gold, long stageBefore, long stageAfter, int killsOnStage, bool farming,
            bool stoppedAtGuardian, bool stuckAtBoss)
        {
            Seconds = seconds;
            Kills = kills;
            Gold = gold;
            StageBefore = stageBefore;
            StageAfter = stageAfter;
            KillsOnStage = killsOnStage;
            Farming = farming;
            StoppedAtGuardian = stoppedAtGuardian;
            StuckAtBoss = stuckAtBoss;
        }

        public double Seconds { get; }
        public long Kills { get; }
        public BigNumber Gold { get; }
        public long StageBefore { get; }
        public long StageAfter { get; }
        public int KillsOnStage { get; }
        public bool Farming { get; }
        public bool StoppedAtGuardian { get; }
        public bool StuckAtBoss { get; }
        public long StagesCleared => StageAfter - StageBefore;
    }
}
