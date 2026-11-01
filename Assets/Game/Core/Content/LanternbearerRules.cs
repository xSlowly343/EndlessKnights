namespace Game.Core.Content
{
    public sealed class LanternbearerRules
    {
        public static readonly LanternbearerRules None = new LanternbearerRules();

        public double LightPerHit { get; private set; } = 0.01;
        public double LightPerHitMin { get; private set; } = 0.001;
        public double ShadowSeconds { get; private set; } = 10;
        public double ShadowSecondsMin { get; private set; } = 1;
        public double ShadowShare { get; private set; } = 0.25;
        public double LightOil { get; private set; } = 10;
        public double FlareMultiplier { get; private set; } = 1.5;
        public double FlareSeconds { get; private set; } = 5;
        public double FlareKeep { get; private set; } = 0.5;
        public double LightEffectSeconds { get; private set; } = 2;
        public int FireflyHits { get; private set; } = 20;
        public int FireflyHitsMin { get; private set; } = 3;
        public double FireflySeconds { get; private set; } = 20;
        public double FireflySecondsMin { get; private set; } = 2;
        public int FireflyMax { get; private set; } = 10;
        public double SparkOil { get; private set; } = 1;
        public double BurstMultiplier { get; private set; } = 3;
        public int SparkEvery { get; private set; } = 10;
        public double BeaconSeconds { get; private set; } = 60;
        public double BeaconSecondsMin { get; private set; } = 10;
        public double BeaconStrike { get; private set; } = 0.2;
        public double BeaconStrikeSeconds { get; private set; } = 5;

        public static LanternbearerRules Read(ContentNode node)
        {
            var light = node.Field("light");
            var firefly = node.Field("firefly");
            var beacon = node.Field("beacon");
            return new LanternbearerRules
            {
                LightPerHit = light.Field("perHit").AsNumber(0, 1),
                LightPerHitMin = light.Field("perHitMin").AsNumber(0, 1),
                ShadowSeconds = light.Field("shadowSeconds").AsNumber(0.1, 600),
                ShadowSecondsMin = light.Field("shadowSecondsMin").AsNumber(0.1, 600),
                ShadowShare = light.Field("shadowShare").AsNumber(0, 1),
                LightOil = light.Field("oil").AsNumber(0, 1000),
                FlareMultiplier = light.Field("flareMultiplier").AsNumber(1, 100),
                FlareSeconds = light.Field("flareSeconds").AsNumber(0, 600),
                FlareKeep = light.Field("flareKeep").AsNumber(0, 1),
                LightEffectSeconds = light.Field("effectSeconds").AsNumber(0.1, 60),
                FireflyHits = firefly.Field("hits").AsInt(1, 10_000),
                FireflyHitsMin = firefly.Field("hitsMin").AsInt(1, 10_000),
                FireflySeconds = firefly.Field("seconds").AsNumber(0.1, 3600),
                FireflySecondsMin = firefly.Field("secondsMin").AsNumber(0.1, 3600),
                FireflyMax = firefly.Field("max").AsInt(1, 100),
                SparkOil = firefly.Field("oil").AsNumber(0, 1000),
                BurstMultiplier = firefly.Field("burstMultiplier").AsNumber(0, 100),
                SparkEvery = firefly.Field("sparkEvery").AsInt(1, 1000),
                BeaconSeconds = beacon.Field("seconds").AsNumber(1, 3600),
                BeaconSecondsMin = beacon.Field("secondsMin").AsNumber(1, 3600),
                BeaconStrike = beacon.Field("strike").AsNumber(0, 10),
                BeaconStrikeSeconds = beacon.Field("strikeSeconds").AsNumber(0, 600)
            };
        }
    }
}
