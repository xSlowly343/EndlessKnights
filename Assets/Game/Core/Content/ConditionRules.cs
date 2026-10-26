namespace Game.Core.Content
{
    public sealed class ConditionRules
    {
        public ConditionRules(double bossTimerEndSeconds, int firstHits, int killStreakMax, double killStreakResetSeconds, double cullMax, double afterIdlePauseSeconds, double afterIdleSeconds)
        {
            BossTimerEndSeconds = bossTimerEndSeconds;
            FirstHits = firstHits;
            KillStreakMax = killStreakMax;
            KillStreakResetSeconds = killStreakResetSeconds;
            CullMax = cullMax;
            AfterIdlePauseSeconds = afterIdlePauseSeconds;
            AfterIdleSeconds = afterIdleSeconds;
        }

        public double BossTimerEndSeconds { get; }
        public int FirstHits { get; }
        public int KillStreakMax { get; }
        public double KillStreakResetSeconds { get; }
        public double CullMax { get; }
        public double AfterIdlePauseSeconds { get; }
        public double AfterIdleSeconds { get; }

        public static ConditionRules Read(ContentNode node)
        {
            var streak = node.Field("killStreak");
            var afterIdle = node.Field("afterIdle");
            return new ConditionRules(
                node.Field("bossTimerEndSeconds").AsNumber(0, 600),
                node.Field("firstHits").AsInt(0, 10000),
                streak.Field("max").AsInt(0, 100000),
                streak.Field("resetSeconds").AsNumber(0.1, 3600),
                node.Field("cullMax").AsNumber(0, 1),
                afterIdle.Field("pauseSeconds").AsNumber(0, 86400),
                afterIdle.Field("seconds").AsNumber(0, 3600));
        }
    }
}
