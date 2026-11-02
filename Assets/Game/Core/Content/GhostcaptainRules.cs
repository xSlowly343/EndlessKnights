namespace Game.Core.Content
{
    public sealed class GhostcaptainRules
    {
        public static readonly GhostcaptainRules None = new GhostcaptainRules();

        public int Cannons { get; private set; } = 8;
        public int CannonsMax { get; private set; } = 12;
        public int CannonHits { get; private set; } = 10;
        public int CannonHitsMin { get; private set; } = 3;
        public double SameEnemy { get; private set; } = 2;
        public double BreachSeconds { get; private set; } = 3;
        public double BreachDamageTaken { get; private set; } = 0.15;
        public double BreachEffectSeconds { get; private set; } = 1;
        public double VolleyQueueShare { get; private set; } = 0.5;
        public double VolleyPowder { get; private set; } = 10;
        public int LoadedAfter { get; private set; } = 2;
        public double CrewHitsPerSecond { get; private set; } = 1;
        public double DawnSeconds { get; private set; } = 120;
        public double DawnSecondsMin { get; private set; } = 30;
        public int GuardianSailors { get; private set; } = 2;
        public int BossSailors { get; private set; } = 3;
        public double Farewell { get; private set; } = 5;
        public double CrewKeep { get; private set; } = 0.25;
        public double CrewQueueShare { get; private set; } = 0.5;
        public double MateMultiplier { get; private set; } = 10;
        public int ParrotEvery { get; private set; } = 3;
        public int ParrotEveryMin { get; private set; } = 2;
        public double OfferSeconds { get; private set; } = 5;
        public double HeavyCooldownSeconds { get; private set; } = 10;
        public double ParrotPowder { get; private set; } = 5;
        public double ParrotTwoShare { get; private set; } = 0.6;
        public double ParrotQueueShare { get; private set; } = 0.5;
        public double StrikeFallback { get; private set; } = 3;

        public static GhostcaptainRules Read(ContentNode node)
        {
            var broadside = node.Field("broadside");
            var crew = node.Field("crew");
            var parrot = node.Field("parrot");
            return new GhostcaptainRules
            {
                Cannons = broadside.Field("cannons").AsInt(1, 50),
                CannonsMax = broadside.Field("cannonsMax").AsInt(1, 50),
                CannonHits = broadside.Field("hits").AsInt(1, 10_000),
                CannonHitsMin = broadside.Field("hitsMin").AsInt(1, 10_000),
                SameEnemy = broadside.Field("sameEnemy").AsNumber(1, 100),
                BreachSeconds = broadside.Field("breachSeconds").AsNumber(0, 600),
                BreachDamageTaken = broadside.Field("breachDamageTaken").AsNumber(0, 10),
                BreachEffectSeconds = broadside.Field("effectSeconds").AsNumber(0.1, 60),
                VolleyQueueShare = broadside.Field("queueShare").AsNumber(0, 10),
                VolleyPowder = broadside.Field("powder").AsNumber(0, 1000),
                LoadedAfter = broadside.Field("loadedAfter").AsInt(0, 50),
                CrewHitsPerSecond = crew.Field("hitsPerSecond").AsNumber(0.1, 20),
                DawnSeconds = crew.Field("dawnSeconds").AsNumber(1, 3600),
                DawnSecondsMin = crew.Field("dawnSecondsMin").AsNumber(1, 3600),
                GuardianSailors = crew.Field("guardianSailors").AsInt(1, 100),
                BossSailors = crew.Field("bossSailors").AsInt(1, 100),
                Farewell = crew.Field("farewell").AsNumber(0, 1000),
                CrewKeep = crew.Field("keep").AsNumber(0, 1),
                CrewQueueShare = crew.Field("queueShare").AsNumber(0, 10),
                MateMultiplier = crew.Field("mateMultiplier").AsNumber(0, 10_000),
                ParrotEvery = parrot.Field("every").AsInt(1, 100),
                ParrotEveryMin = parrot.Field("everyMin").AsInt(1, 100),
                OfferSeconds = parrot.Field("offerSeconds").AsNumber(0.1, 600),
                HeavyCooldownSeconds = parrot.Field("heavyCooldownSeconds").AsNumber(0, 3600),
                ParrotPowder = parrot.Field("powder").AsNumber(0, 1000),
                ParrotTwoShare = parrot.Field("twoShare").AsNumber(0, 10),
                ParrotQueueShare = parrot.Field("queueShare").AsNumber(0, 10),
                StrikeFallback = parrot.Field("strikeFallback").AsNumber(0, 1000)
            };
        }
    }
}
