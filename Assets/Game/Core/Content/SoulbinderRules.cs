namespace Game.Core.Content
{
    public sealed class SoulbinderRules
    {
        public static readonly SoulbinderRules None = new SoulbinderRules();

        public int SphereSize { get; private set; } = 6;
        public int SphereMax { get; private set; } = 12;
        public double SpiritHitsPerSecond { get; private set; } = 1;
        public double GuardianSoul { get; private set; } = 2;
        public double BossSoul { get; private set; } = 3;
        public double PushBlast { get; private set; } = 5;
        public double ReleaseEverySeconds { get; private set; } = 30;
        public double ReleaseMultiplier { get; private set; } = 3;
        public double QueueShare { get; private set; } = 0.7;
        public double ColdSeconds { get; private set; } = 5;
        public double ColdDamageTaken { get; private set; } = 0.15;
        public int ColdHits { get; private set; } = 25;
        public double ColdSoloShare { get; private set; } = 0.5;
        public double ColdEffectSeconds { get; private set; } = 1;
        public double BreathTwice { get; private set; } = 0.5;
        public int LendHits { get; private set; } = 50;
        public int LendHitsMin { get; private set; } = 10;
        public double LendSeconds { get; private set; } = 10;
        public double SoulBackResource { get; private set; } = 5;
        public int SoulBackHits { get; private set; } = 10;
        public double LendAllSeconds { get; private set; } = 5;

        public static SoulbinderRules Read(ContentNode node)
        {
            var sphere = node.Field("sphere");
            var breath = node.Field("breath");
            var lend = node.Field("lend");
            return new SoulbinderRules
            {
                SphereSize = sphere.Field("size").AsInt(1, 50),
                SphereMax = sphere.Field("max").AsInt(1, 50),
                SpiritHitsPerSecond = sphere.Field("hitsPerSecond").AsNumber(0.1, 20),
                GuardianSoul = sphere.Field("guardianSoul").AsNumber(1, 100),
                BossSoul = sphere.Field("bossSoul").AsNumber(1, 100),
                PushBlast = sphere.Field("pushBlast").AsNumber(0, 1000),
                ReleaseEverySeconds = sphere.Field("releaseEverySeconds").AsNumber(1, 3600),
                ReleaseMultiplier = sphere.Field("releaseMultiplier").AsNumber(0, 100),
                QueueShare = sphere.Field("queueShare").AsNumber(0, 10),
                ColdSeconds = breath.Field("coldSeconds").AsNumber(0, 600),
                ColdDamageTaken = breath.Field("coldDamageTaken").AsNumber(0, 10),
                ColdHits = breath.Field("coldHits").AsInt(1, 10_000),
                ColdSoloShare = breath.Field("coldSoloShare").AsNumber(0, 10),
                ColdEffectSeconds = breath.Field("effectSeconds").AsNumber(0.1, 60),
                BreathTwice = breath.Field("twice").AsNumber(0, 10),
                LendHits = lend.Field("hits").AsInt(1, 10_000),
                LendHitsMin = lend.Field("hitsMin").AsInt(1, 10_000),
                LendSeconds = lend.Field("seconds").AsNumber(0.1, 600),
                SoulBackResource = lend.Field("soulBackResource").AsNumber(0, 1000),
                SoulBackHits = lend.Field("soulBackHits").AsInt(0, 10_000),
                LendAllSeconds = lend.Field("allSeconds").AsNumber(0.1, 600)
            };
        }
    }
}
