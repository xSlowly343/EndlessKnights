namespace Game.Core.Content
{
    public sealed class MothcallerRules
    {
        public static readonly MothcallerRules None = new MothcallerRules();

        public int SwarmMax { get; private set; } = 50;
        public int SwarmCap { get; private set; } = 100;
        public double SwarmHitsPerSecond { get; private set; } = 1;
        public double SwarmCarry { get; private set; } = 0.5;
        public double SwarmKeep { get; private set; } = 0.75;
        public int GoldenEvery { get; private set; } = 10;
        public double GoldenMultiplier { get; private set; } = 3;
        public double SwarmBoss { get; private set; } = 2;
        public double SwarmQueueShare { get; private set; } = 0.5;
        public double PollenBurnSeconds { get; private set; } = 4;
        public double PollenFastSeconds { get; private set; } = 2;
        public double PollenMax { get; private set; } = 200;
        public double PollenRelaySeconds { get; private set; } = 2;
        public double PollenLight { get; private set; } = 5;
        public double PollenTwoShare { get; private set; } = 1.4;
        public double MothEverySeconds { get; private set; } = 30;
        public double MothEverySecondsMin { get; private set; } = 5;
        public double MothAwakeSeconds { get; private set; } = 8;
        public double MothAwakeSecondsMin { get; private set; } = 2;
        public double MothQueueShare { get; private set; } = 0.5;
        public double MothNormalPenalty { get; private set; } = 0.3;
        public double MothStrike { get; private set; } = 0.5;
        public double MothTimeSeconds { get; private set; } = 1;

        public static MothcallerRules Read(ContentNode node)
        {
            var swarm = node.Field("swarm");
            var pollen = node.Field("pollen");
            var moth = node.Field("moth");
            return new MothcallerRules
            {
                SwarmMax = swarm.Field("max").AsInt(1, 1000),
                SwarmCap = swarm.Field("cap").AsInt(1, 1000),
                SwarmHitsPerSecond = swarm.Field("hitsPerSecond").AsNumber(0.1, 20),
                SwarmCarry = swarm.Field("carry").AsNumber(0, 1),
                SwarmKeep = swarm.Field("keep").AsNumber(0, 1),
                GoldenEvery = swarm.Field("goldenEvery").AsInt(1, 1000),
                GoldenMultiplier = swarm.Field("goldenMultiplier").AsNumber(1, 100),
                SwarmBoss = swarm.Field("boss").AsNumber(1, 100),
                SwarmQueueShare = swarm.Field("queueShare").AsNumber(0, 10),
                PollenBurnSeconds = pollen.Field("burnSeconds").AsNumber(0.1, 60),
                PollenFastSeconds = pollen.Field("fastSeconds").AsNumber(0.1, 60),
                PollenMax = pollen.Field("max").AsNumber(0, 1e6),
                PollenRelaySeconds = pollen.Field("relaySeconds").AsNumber(0.1, 60),
                PollenLight = pollen.Field("light").AsNumber(0, 1000),
                PollenTwoShare = pollen.Field("twoShare").AsNumber(0, 10),
                MothEverySeconds = moth.Field("everySeconds").AsNumber(1, 3600),
                MothEverySecondsMin = moth.Field("everySecondsMin").AsNumber(1, 3600),
                MothAwakeSeconds = moth.Field("awakeSeconds").AsNumber(0.1, 600),
                MothAwakeSecondsMin = moth.Field("awakeSecondsMin").AsNumber(0.1, 600),
                MothQueueShare = moth.Field("queueShare").AsNumber(0, 10),
                MothNormalPenalty = moth.Field("normalPenalty").AsNumber(0, 1),
                MothStrike = moth.Field("strike").AsNumber(0, 10),
                MothTimeSeconds = moth.Field("timeSeconds").AsNumber(0, 60)
            };
        }
    }
}
