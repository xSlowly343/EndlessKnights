namespace Game.Core.Party
{
    public readonly struct PartyPierceStats
    {
        public PartyPierceStats(double pierceShare, int enemySkip, double splashShare, int splashTargets, double bossPierce, double stageCarry)
        {
            PierceShare = pierceShare;
            EnemySkip = enemySkip;
            SplashShare = splashShare;
            SplashTargets = splashTargets;
            BossPierce = bossPierce;
            StageCarry = stageCarry;
        }

        public double PierceShare { get; }
        public int EnemySkip { get; }
        public double SplashShare { get; }
        public int SplashTargets { get; }
        public double BossPierce { get; }
        public double StageCarry { get; }
    }
}
