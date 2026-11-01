namespace Game.Core.Content
{
    public sealed class ClassRules
    {
        public ClassRules(double keyStreakGapSeconds,
            double clockworkAbove = 0, double clockworkSlow = 0, double clockworkFloor = 1)
        {
            ClockworkAbove = clockworkAbove;
            ClockworkSlow = clockworkSlow;
            ClockworkFloor = clockworkFloor;
            KeyStreakGapSeconds = keyStreakGapSeconds;
        }

        public double KeyStreakGapSeconds { get; }
        public double ClockworkAbove { get; }
        public double ClockworkSlow { get; }
        public double ClockworkFloor { get; }
        public HunterMarkRules HunterMark { get; private set; } = HunterMarkRules.None;
        public EvilEyeRules EvilEye { get; private set; } = EvilEyeRules.None;
        public int OverloadQueueTargets { get; private set; }
        public BondRules Bonds { get; private set; } = BondRules.None;
        public ColdRules Cold { get; private set; } = ColdRules.None;
        public BloodRules Blood { get; private set; } = BloodRules.None;
        public GolemRules Golem { get; private set; } = GolemRules.None;
        public RunedancerRules Runedancer { get; private set; } = RunedancerRules.None;
        public DuelistRules Duelist { get; private set; } = DuelistRules.None;
        public WindshotRules Windshot { get; private set; } = WindshotRules.None;
        public ShardcallerRules Shardcaller { get; private set; } = ShardcallerRules.None;
        public SandwalkerRules Sandwalker { get; private set; } = SandwalkerRules.None;
        public MinstrelRules Minstrel { get; private set; } = MinstrelRules.None;
        public DreamcatcherRules Dreamcatcher { get; private set; } = DreamcatcherRules.None;
        public LanternbearerRules Lanternbearer { get; private set; } = LanternbearerRules.None;

        public static ClassRules Read(ContentNode node)
        {
            var minstrel = node.Field("minstrel");
            var clockwork = node.Field("clockwright").Field("clockwork");
            return new ClassRules(
                node.Field("keyStreak").Field("gapSeconds").AsNumber(0.1, 60),
                clockwork.Field("resourceAbove").AsNumber(0, 1e6),
                clockwork.Field("slow").AsNumber(0, 1),
                clockwork.Field("floor").AsNumber(0, 1)) { HunterMark = HunterMarkRules.Read(node.Field("windshot").Field("mark")),
                EvilEye = EvilEyeRules.Read(node.Field("whisperer").Field("evilEye")),
                OverloadQueueTargets = node.Field("shardcaller").Field("overload").Field("queueTargets").AsInt(0, 100),
                Bonds = BondRules.Read(node.Field("mothcaller").Field("bonds")),
                Cold = ColdRules.Read(node.Field("cartomancer").Field("cold")),
                Blood = BloodRules.Read(node.Field("herbwitch").Field("blood")),
                Golem = GolemRules.Read(node.Field("golemancer").Field("golem")),
                Runedancer = RunedancerRules.Read(node.Field("runedancer")),
                Duelist = DuelistRules.Read(node.Field("duelist")),
                Windshot = WindshotRules.Read(node.Field("windshot")),
                Shardcaller = ShardcallerRules.Read(node.Field("shardcaller")),
                Sandwalker = SandwalkerRules.Read(node.Field("sandwalker")),
                Minstrel = MinstrelRules.Read(minstrel),
                Dreamcatcher = DreamcatcherRules.Read(node.Field("dreamcatcher")),
                Lanternbearer = LanternbearerRules.Read(node.Field("lanternbearer")) };
        }
    }
}
