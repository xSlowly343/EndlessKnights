namespace Game.Core.Content
{
    public sealed class GolemancerRules
    {
        public static readonly GolemancerRules None = new GolemancerRules();

        public int SealHits { get; private set; } = 100;
        public int SealHitsMin { get; private set; } = 20;
        public double GolemSeconds { get; private set; } = 20;
        public double CornerBonus { get; private set; } = 0.25;
        public int CornerHits { get; private set; } = 25;
        public double CornerOfferSeconds { get; private set; } = 5;
        public double CrumbleMultiplier { get; private set; } = 5;
        public double CrystalPenalty { get; private set; } = 0.2;
        public double GuardStopSeconds { get; private set; } = 2;
        public double TwoShare { get; private set; } = 0.6;
        public double TitanSeconds { get; private set; } = 10;
        public int RuneHits { get; private set; } = 100;
        public int RuneHitsMin { get; private set; } = 20;
        public double RuneBonus { get; private set; } = 0.05;
        public int RunesMax { get; private set; } = 40;
        public int RunesCap { get; private set; } = 80;
        public double HeavyEverySeconds { get; private set; } = 2;
        public double HeavyMultiplier { get; private set; } = 2.5;
        public int BrandMax { get; private set; } = 10;
        public int BrandHits { get; private set; } = 20;
        public double BrandSoloShare { get; private set; } = 0.5;
        public double BrandMultiplier { get; private set; } = 3;
        public double BrandEffectSeconds { get; private set; } = 1;
        public double EternalQueueShare { get; private set; } = 0.5;
        public double BossMultiplier { get; private set; } = 2;
        public double NormalPenalty { get; private set; } = 0.3;
        public int MinorHits { get; private set; } = 20;
        public int MinorHitsMin { get; private set; } = 5;
        public double MinorSeconds { get; private set; } = 15;
        public int MinorMax { get; private set; } = 4;
        public int MinorCap { get; private set; } = 12;
        public double MinorQueueShare { get; private set; } = 0.5;
        public double MinorBirth { get; private set; } = 3;

        public static GolemancerRules Read(ContentNode node)
        {
            var seal = node.Field("seal");
            var eternal = node.Field("eternal");
            var minor = node.Field("minor");
            return new GolemancerRules
            {
                SealHits = seal.Field("hits").AsInt(1, 100_000),
                SealHitsMin = seal.Field("hitsMin").AsInt(1, 100_000),
                GolemSeconds = seal.Field("golemSeconds").AsNumber(0.1, 3600),
                CornerBonus = seal.Field("cornerBonus").AsNumber(0, 10),
                CornerHits = seal.Field("cornerHits").AsInt(1, 10_000),
                CornerOfferSeconds = seal.Field("cornerOfferSeconds").AsNumber(0.1, 600),
                CrumbleMultiplier = seal.Field("crumbleMultiplier").AsNumber(0, 1000),
                CrystalPenalty = seal.Field("crystalPenalty").AsNumber(0, 1),
                GuardStopSeconds = seal.Field("guardStopSeconds").AsNumber(0, 600),
                TwoShare = seal.Field("twoShare").AsNumber(0, 10),
                TitanSeconds = seal.Field("titanSeconds").AsNumber(0.1, 600),
                RuneHits = eternal.Field("runeHits").AsInt(1, 100_000),
                RuneHitsMin = eternal.Field("runeHitsMin").AsInt(1, 100_000),
                RuneBonus = eternal.Field("runeBonus").AsNumber(0, 10),
                RunesMax = eternal.Field("runesMax").AsInt(1, 1000),
                RunesCap = eternal.Field("runesCap").AsInt(1, 1000),
                HeavyEverySeconds = eternal.Field("heavyEverySeconds").AsNumber(0.1, 60),
                HeavyMultiplier = eternal.Field("heavyMultiplier").AsNumber(1, 100),
                BrandMax = eternal.Field("brandMax").AsInt(1, 100),
                BrandHits = eternal.Field("brandHits").AsInt(1, 10_000),
                BrandSoloShare = eternal.Field("brandSoloShare").AsNumber(0, 10),
                BrandMultiplier = eternal.Field("brandMultiplier").AsNumber(0, 1000),
                BrandEffectSeconds = eternal.Field("effectSeconds").AsNumber(0.1, 60),
                EternalQueueShare = eternal.Field("queueShare").AsNumber(0, 10),
                BossMultiplier = eternal.Field("bossMultiplier").AsNumber(1, 100),
                NormalPenalty = eternal.Field("normalPenalty").AsNumber(0, 1),
                MinorHits = minor.Field("hits").AsInt(1, 10_000),
                MinorHitsMin = minor.Field("hitsMin").AsInt(1, 10_000),
                MinorSeconds = minor.Field("seconds").AsNumber(0.1, 600),
                MinorMax = minor.Field("max").AsInt(1, 100),
                MinorCap = minor.Field("cap").AsInt(1, 100),
                MinorQueueShare = minor.Field("queueShare").AsNumber(0, 10),
                MinorBirth = minor.Field("birth").AsNumber(0, 1000)
            };
        }
    }
}
