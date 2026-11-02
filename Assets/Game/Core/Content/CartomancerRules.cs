namespace Game.Core.Content
{
    public sealed class CartomancerRules
    {
        public static readonly CartomancerRules None = new CartomancerRules();

        public int DrawHits { get; private set; } = 10;
        public int DrawHitsMin { get; private set; } = 4;
        public int HandMax { get; private set; } = 7;
        public int ArcanaCards { get; private set; } = 3;
        public double ArcanaSeconds { get; private set; } = 15;
        public double ArcanaShortSeconds { get; private set; } = 8;
        public double LongSeconds { get; private set; } = 5;
        public double CupsResource { get; private set; } = 4;
        public double CoinsGold { get; private set; } = 2;
        public double WandsCut { get; private set; } = 1;
        public double SwordsBoost { get; private set; } = 2;
        public double OtherPenalty { get; private set; } = 0.5;
        public double HandBonus { get; private set; } = 0.03;
        public double TermShare { get; private set; } = 0.6;
        public double TermMinSeconds { get; private set; } = 5;
        public double LongTerm { get; private set; } = 1.3;
        public double ShortTerm { get; private set; } = 0.7;
        public double Penalty { get; private set; } = 0.2;
        public double ExactPenalty { get; private set; } = 0.1;
        public double ExactSeconds { get; private set; } = 3;
        public double ExactGold { get; private set; } = 1;
        public double RushSeconds { get; private set; } = 5;
        public double Rush { get; private set; } = 0.3;
        public double StreakStep { get; private set; } = 0.25;
        public int StreakMax { get; private set; } = 4;
        public int LayHits { get; private set; } = 10;
        public int LayHitsMin { get; private set; } = 3;
        public int LaidMax { get; private set; } = 7;
        public int LaidCap { get; private set; } = 12;
        public double CupsHit { get; private set; } = 5;
        public double CoinsPerCard { get; private set; } = 1.25;
        public double WandsSlow { get; private set; } = 0.75;
        public double WandsSlowMax { get; private set; } = 0.5;
        public int RevealHits { get; private set; } = 20;
        public double RevealSoloShare { get; private set; } = 0.5;
        public double RevealSeconds { get; private set; } = 5;
        public double RevealMultiplier { get; private set; } = 2;
        public double RevealStrong { get; private set; } = 3;
        public double KeepShare { get; private set; } = 0.5;
        public double EffectSeconds { get; private set; } = 1;

        public static CartomancerRules Read(ContentNode node)
        {
            var spread = node.Field("spread");
            var forecast = node.Field("forecast");
            var reversed = node.Field("reversed");
            return new CartomancerRules
            {
                DrawHits = spread.Field("hits").AsInt(1, 10_000),
                DrawHitsMin = spread.Field("hitsMin").AsInt(1, 10_000),
                HandMax = spread.Field("handMax").AsInt(1, 50),
                ArcanaCards = spread.Field("arcanaCards").AsInt(1, 50),
                ArcanaSeconds = spread.Field("seconds").AsNumber(0.1, 600),
                ArcanaShortSeconds = spread.Field("shortSeconds").AsNumber(0.1, 600),
                LongSeconds = spread.Field("longSeconds").AsNumber(0, 600),
                CupsResource = spread.Field("cupsResource").AsNumber(0, 1000),
                CoinsGold = spread.Field("coinsGold").AsNumber(0, 1000),
                WandsCut = spread.Field("wandsCut").AsNumber(0, 1000),
                SwordsBoost = spread.Field("swordsBoost").AsNumber(1, 100),
                OtherPenalty = spread.Field("otherPenalty").AsNumber(0, 1),
                HandBonus = spread.Field("handBonus").AsNumber(0, 10),
                TermShare = forecast.Field("termShare").AsNumber(0.01, 1),
                TermMinSeconds = forecast.Field("termMinSeconds").AsNumber(0, 600),
                LongTerm = forecast.Field("longTerm").AsNumber(1, 10),
                ShortTerm = forecast.Field("shortTerm").AsNumber(0.01, 1),
                Penalty = forecast.Field("penalty").AsNumber(0, 1),
                ExactPenalty = forecast.Field("exactPenalty").AsNumber(0, 1),
                ExactSeconds = forecast.Field("exactSeconds").AsNumber(0, 600),
                ExactGold = forecast.Field("exactGold").AsNumber(0, 1000),
                RushSeconds = forecast.Field("rushSeconds").AsNumber(0, 600),
                Rush = forecast.Field("rush").AsNumber(0, 10),
                StreakStep = forecast.Field("streakStep").AsNumber(0, 10),
                StreakMax = forecast.Field("streakMax").AsInt(0, 100),
                LayHits = reversed.Field("hits").AsInt(1, 10_000),
                LayHitsMin = reversed.Field("hitsMin").AsInt(1, 10_000),
                LaidMax = reversed.Field("max").AsInt(1, 50),
                LaidCap = reversed.Field("cap").AsInt(1, 50),
                CupsHit = reversed.Field("cupsHit").AsNumber(0, 1000),
                CoinsPerCard = reversed.Field("coinsPerCard").AsNumber(0, 1000),
                WandsSlow = reversed.Field("wandsSlow").AsNumber(0, 100),
                WandsSlowMax = reversed.Field("wandsSlowMax").AsNumber(0, 1),
                RevealHits = reversed.Field("revealHits").AsInt(1, 10_000),
                RevealSoloShare = reversed.Field("revealSoloShare").AsNumber(0, 10),
                RevealSeconds = reversed.Field("revealSeconds").AsNumber(0.1, 600),
                RevealMultiplier = reversed.Field("revealMultiplier").AsNumber(1, 100),
                RevealStrong = reversed.Field("revealStrong").AsNumber(1, 100),
                KeepShare = reversed.Field("keepShare").AsNumber(0, 1),
                EffectSeconds = reversed.Field("effectSeconds").AsNumber(0.1, 60)
            };
        }
    }
}
