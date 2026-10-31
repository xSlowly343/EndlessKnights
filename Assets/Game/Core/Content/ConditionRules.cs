namespace Game.Core.Content
{
    public sealed class ConditionRules
    {
        public ConditionRules(double bossTimerEndSeconds, int firstHits, int killStreakMax, double killStreakResetSeconds, double cullMax, double afterIdlePauseSeconds, double afterIdleSeconds, double fastKillSeconds, double inputKindSeconds = 10)
        {
            InputKindSeconds = inputKindSeconds;
            FastKillSeconds = fastKillSeconds;
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
        public double FastKillSeconds { get; }
        public double InputKindSeconds { get; }
        public double LongStreakSeconds { get; private set; } = double.MaxValue;
        public double LongStreakPauseSeconds { get; private set; } = 3;
        public double BossOpeningSeconds { get; private set; } = 3;
        public double BossLowHpShare { get; private set; } = 0.3;
        public double AfterFastKillSeconds { get; private set; } = 3;
        public double AfterGestureSeconds { get; private set; } = 3;
        public double MouseMovingSeconds { get; private set; } = 0.5;

        public static ConditionRules Read(ContentNode node)
        {
            var streak = node.Field("killStreak");
            var afterIdle = node.Field("afterIdle");
            var longStreak = node.Field("longStreak");
            return new ConditionRules(
                node.Field("bossTimerEndSeconds").AsNumber(0, 600),
                node.Field("firstHits").AsInt(0, 10000),
                streak.Field("max").AsInt(0, 100000),
                streak.Field("resetSeconds").AsNumber(0.1, 3600),
                node.Field("cullMax").AsNumber(0, 1),
                afterIdle.Field("pauseSeconds").AsNumber(0, 86400),
                afterIdle.Field("seconds").AsNumber(0, 3600),
                node.Field("fastKillSeconds").AsNumber(0, 600),
                node.Field("inputKindSeconds").AsNumber(0.1, 600))
            {
                LongStreakSeconds = longStreak.Field("seconds").AsNumber(1, 86400),
                LongStreakPauseSeconds = longStreak.Field("pauseSeconds").AsNumber(0.1, 3600),
                BossOpeningSeconds = node.HasField("bossOpeningSeconds") ? node.Field("bossOpeningSeconds").AsNumber(0, 600) : 3,
                BossLowHpShare = node.HasField("bossLowHp") ? node.Field("bossLowHp").AsNumber(0, 1) : 0.3,
                AfterFastKillSeconds = node.HasField("afterFastKillSeconds") ? node.Field("afterFastKillSeconds").AsNumber(0, 600) : 3,
                AfterGestureSeconds = node.HasField("afterGestureSeconds") ? node.Field("afterGestureSeconds").AsNumber(0, 600) : 3,
                MouseMovingSeconds = node.HasField("mouseMovingSeconds") ? node.Field("mouseMovingSeconds").AsNumber(0.1, 60) : 0.5
            };
        }
    }
}
