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
        public double ShareWindowSeconds { get; private set; } = 3600;
        public int DayStartHour { get; private set; } = 6;
        public int NightStartHour { get; private set; } = 18;
        public int ServerUtcOffsetHours { get; private set; }
        public double LeastInputSeconds { get; private set; } = 60;
        public double BelowRecordShare { get; private set; } = 0.8;

        public bool IsDay(int hour) => DayStartHour <= NightStartHour
            ? hour >= DayStartHour && hour < NightStartHour
            : hour >= DayStartHour || hour < NightStartHour;

        public int ServerHour(long utcUnixMs)
        {
            var hours = System.Math.Floor(utcUnixMs / 3_600_000.0) + ServerUtcOffsetHours;
            return (int)(((hours % 24) + 24) % 24);
        }

        public double ServerDayHours(long utcUnixMs)
        {
            var hours = utcUnixMs / 3_600_000.0 + ServerUtcOffsetHours;
            return ((hours % 24) + 24) % 24;
        }

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
                MouseMovingSeconds = node.HasField("mouseMovingSeconds") ? node.Field("mouseMovingSeconds").AsNumber(0.1, 60) : 0.5,
                ShareWindowSeconds = node.HasField("shareWindowMinutes") ? node.Field("shareWindowMinutes").AsNumber(1, 1440) * 60 : 3600,
                DayStartHour = node.HasField("dayNight") ? node.Field("dayNight").Field("dayStartHour").AsInt(0, 23) : 6,
                NightStartHour = node.HasField("dayNight") ? node.Field("dayNight").Field("nightStartHour").AsInt(0, 23) : 18,
                ServerUtcOffsetHours = node.HasField("dayNight") ? node.Field("dayNight").Field("serverUtcOffsetHours").AsInt(-12, 14) : 0,
                LeastInputSeconds = node.HasField("leastInputSeconds") ? node.Field("leastInputSeconds").AsNumber(1, 3600) : 60,
                BelowRecordShare = node.HasField("belowRecordShare") ? node.Field("belowRecordShare").AsNumber(0, 1) : 0.8
            };
        }
    }
}
