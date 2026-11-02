namespace Game.Core.Content
{
    public sealed class ClockwrightRules
    {
        public static readonly ClockwrightRules None = new ClockwrightRules();

        public double SpringLow { get; private set; } = 0.1;
        public double SpringPenalty { get; private set; } = 0.1;
        public double SpringDrainPerSecond { get; private set; } = 1;
        public double SpringWindPerHit { get; private set; } = 0.5;
        public double SpringAllyWind { get; private set; } = 0.1;
        public double SpringFullAbove { get; private set; } = 0.95;
        public double SpringFullSlow { get; private set; } = 0.15;
        public double SpringStrike { get; private set; } = 0.2;
        public double SpringRelaySeconds { get; private set; } = 3;
        public double SpringBurstEverySeconds { get; private set; } = 30;
        public double SpringBurstPerWind { get; private set; } = 0.2;
        public double LoopEverySeconds { get; private set; } = 40;
        public double LoopEverySecondsMin { get; private set; } = 10;
        public double LoopWindowSeconds { get; private set; } = 5;
        public double LoopWindowMax { get; private set; } = 7;
        public double LoopSolo { get; private set; } = 0.5;
        public double LoopTwice { get; private set; } = 0.5;
        public double LoopStopSeconds { get; private set; } = 3;
        public int GearHits { get; private set; } = 60;
        public int GearHitsMin { get; private set; } = 10;
        public double GearOwn { get; private set; } = 2;
        public double GearOwnSoft { get; private set; } = 1.5;
        public double GearMeshSeconds { get; private set; } = 1.5;
        public double GearMeshCooldownSeconds { get; private set; } = 5;
        public double GearResource { get; private set; } = 5;
        public int GearResetEvery { get; private set; } = 10;
        public double GearCallSeconds { get; private set; } = 30;
        public double GearIdleSeconds { get; private set; } = 10;

        public static ClockwrightRules Read(ContentNode node)
        {
            var spring = node.Field("spring");
            var loop = node.Field("loop");
            var gears = node.Field("gears");
            return new ClockwrightRules
            {
                SpringLow = spring.Field("low").AsNumber(0, 1),
                SpringPenalty = spring.Field("penalty").AsNumber(0, 1),
                SpringDrainPerSecond = spring.Field("drainPerSecond").AsNumber(0, 100),
                SpringWindPerHit = spring.Field("windPerHit").AsNumber(0, 100),
                SpringAllyWind = spring.Field("allyWind").AsNumber(0, 100),
                SpringFullAbove = spring.Field("fullAbove").AsNumber(0, 1),
                SpringFullSlow = spring.Field("fullSlow").AsNumber(0, 1),
                SpringStrike = spring.Field("strike").AsNumber(0, 10),
                SpringRelaySeconds = spring.Field("relaySeconds").AsNumber(0.5, 60),
                SpringBurstEverySeconds = spring.Field("burstEverySeconds").AsNumber(1, 3600),
                SpringBurstPerWind = spring.Field("burstPerWind").AsNumber(0, 100),
                LoopEverySeconds = loop.Field("everySeconds").AsNumber(1, 3600),
                LoopEverySecondsMin = loop.Field("everySecondsMin").AsNumber(1, 3600),
                LoopWindowSeconds = loop.Field("windowSeconds").AsNumber(0.1, 60),
                LoopWindowMax = loop.Field("windowMax").AsNumber(0.1, 60),
                LoopSolo = loop.Field("solo").AsNumber(0, 10),
                LoopTwice = loop.Field("twice").AsNumber(0, 10),
                LoopStopSeconds = loop.Field("stopSeconds").AsNumber(0, 60),
                GearHits = gears.Field("hits").AsInt(1, 10_000),
                GearHitsMin = gears.Field("hitsMin").AsInt(1, 10_000),
                GearOwn = gears.Field("own").AsNumber(1, 10),
                GearOwnSoft = gears.Field("ownSoft").AsNumber(1, 10),
                GearMeshSeconds = gears.Field("meshSeconds").AsNumber(0.1, 10),
                GearMeshCooldownSeconds = gears.Field("meshCooldownSeconds").AsNumber(0, 600),
                GearResource = gears.Field("resource").AsNumber(0, 100),
                GearResetEvery = gears.Field("resetEvery").AsInt(1, 1000),
                GearCallSeconds = gears.Field("callSeconds").AsNumber(1, 3600),
                GearIdleSeconds = gears.Field("idleSeconds").AsNumber(1, 3600)
            };
        }
    }
}
