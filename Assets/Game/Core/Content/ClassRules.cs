namespace Game.Core.Content
{
    public sealed class ClassRules
    {
        public ClassRules(double keyStreakGapSeconds)
        {
            KeyStreakGapSeconds = keyStreakGapSeconds;
        }

        public double KeyStreakGapSeconds { get; }
        public HunterMarkRules HunterMark { get; private set; } = HunterMarkRules.None;
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
        public WhispererRules Whisperer { get; private set; } = WhispererRules.None;
        public ClockwrightRules Clockwright { get; private set; } = ClockwrightRules.None;
        public AlchemistRules Alchemist { get; private set; } = AlchemistRules.None;
        public DowserRules Dowser { get; private set; } = DowserRules.None;
        public SoulbinderRules Soulbinder { get; private set; } = SoulbinderRules.None;
        public GhostcaptainRules Ghostcaptain { get; private set; } = GhostcaptainRules.None;
        public MothcallerRules Mothcaller { get; private set; } = MothcallerRules.None;
        public CartomancerRules Cartomancer { get; private set; } = CartomancerRules.None;
        public HerbwitchRules Herbwitch { get; private set; } = HerbwitchRules.None;
        public GolemancerRules Golemancer { get; private set; } = GolemancerRules.None;

        public static ClassRules Read(ContentNode node)
        {
            var minstrel = node.Field("minstrel");
            return new ClassRules(
                node.Field("keyStreak").Field("gapSeconds").AsNumber(0.1, 60)) { HunterMark = HunterMarkRules.Read(node.Field("windshot").Field("mark")),
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
                Lanternbearer = LanternbearerRules.Read(node.Field("lanternbearer")),
                Whisperer = WhispererRules.Read(node.Field("whisperer")),
                Clockwright = ClockwrightRules.Read(node.Field("clockwright")),
                Alchemist = AlchemistRules.Read(node.Field("alchemist")),
                Dowser = DowserRules.Read(node.Field("dowser")),
                Soulbinder = SoulbinderRules.Read(node.Field("soulbinder")),
                Ghostcaptain = GhostcaptainRules.Read(node.Field("ghostcaptain")),
                Mothcaller = MothcallerRules.Read(node.Field("mothcaller")),
                Cartomancer = CartomancerRules.Read(node.Field("cartomancer")),
                Herbwitch = HerbwitchRules.Read(node.Field("herbwitch")),
                Golemancer = GolemancerRules.Read(node.Field("golemancer")) };
        }
    }
}
