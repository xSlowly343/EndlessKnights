namespace Game.Core.Content
{
    public sealed class HerbwitchRules
    {
        public static readonly HerbwitchRules None = new HerbwitchRules();

        public int SeedHits { get; private set; } = 15;
        public int SeedHitsMin { get; private set; } = 4;
        public int Vines { get; private set; } = 3;
        public int VinesCap { get; private set; } = 8;
        public double GrowSeconds { get; private set; } = 5;
        public double GrowSecondsMin { get; private set; } = 1;
        public double StageMultiplier { get; private set; } = 2;
        public double ThornMultiplier { get; private set; } = 3;
        public double SingleMultiplier { get; private set; } = 4;
        public double QueueShare { get; private set; } = 0.5;
        public int PetalHits { get; private set; } = 20;
        public double PetalSeconds { get; private set; } = 20;
        public double GardenEffectSeconds { get; private set; } = 1;
        public double VenomMax { get; private set; } = 50;
        public double VenomCarry { get; private set; } = 0.5;
        public double VenomDeepCarry { get; private set; } = 0.75;
        public double VenomQueueShare { get; private set; } = 0.25;
        public double FastMultiplier { get; private set; } = 2;
        public double BrewPerHit { get; private set; } = 2;
        public double BrewMax { get; private set; } = 200;
        public int DrinkHits { get; private set; } = 20;
        public int DrinkHitsMin { get; private set; } = 5;
        public double SapPerStack { get; private set; } = 0.2;
        public double LeaveShare { get; private set; } = 0.3;
        public double PartyPoisonSeconds { get; private set; } = 10;

        public static HerbwitchRules Read(ContentNode node)
        {
            var bloom = node.Field("bloom");
            var mycelium = node.Field("mycelium");
            var sap = node.Field("sap");
            return new HerbwitchRules
            {
                SeedHits = bloom.Field("hits").AsInt(1, 10_000),
                SeedHitsMin = bloom.Field("hitsMin").AsInt(1, 10_000),
                Vines = bloom.Field("vines").AsInt(1, 50),
                VinesCap = bloom.Field("vinesCap").AsInt(1, 50),
                GrowSeconds = bloom.Field("growSeconds").AsNumber(0.1, 600),
                GrowSecondsMin = bloom.Field("growSecondsMin").AsNumber(0.1, 600),
                StageMultiplier = bloom.Field("stageMultiplier").AsNumber(1, 100),
                ThornMultiplier = bloom.Field("thornMultiplier").AsNumber(1, 100),
                SingleMultiplier = bloom.Field("singleMultiplier").AsNumber(1, 100),
                QueueShare = bloom.Field("queueShare").AsNumber(0, 10),
                PetalHits = bloom.Field("petalHits").AsInt(1, 10_000),
                PetalSeconds = bloom.Field("petalSeconds").AsNumber(0.1, 600),
                GardenEffectSeconds = bloom.Field("effectSeconds").AsNumber(0.1, 60),
                VenomMax = mycelium.Field("max").AsNumber(1, 1e6),
                VenomCarry = mycelium.Field("carry").AsNumber(0, 1),
                VenomDeepCarry = mycelium.Field("deepCarry").AsNumber(0, 1),
                VenomQueueShare = mycelium.Field("queueShare").AsNumber(0, 10),
                FastMultiplier = mycelium.Field("fastMultiplier").AsNumber(1, 100),
                BrewPerHit = sap.Field("perHit").AsNumber(0, 1000),
                BrewMax = sap.Field("max").AsNumber(1, 1e6),
                DrinkHits = sap.Field("drinkHits").AsInt(1, 10_000),
                DrinkHitsMin = sap.Field("drinkHitsMin").AsInt(1, 10_000),
                SapPerStack = sap.Field("sapPerStack").AsNumber(0, 1000),
                LeaveShare = sap.Field("leaveShare").AsNumber(0, 1),
                PartyPoisonSeconds = sap.Field("partyPoisonSeconds").AsNumber(0, 600)
            };
        }
    }
}
