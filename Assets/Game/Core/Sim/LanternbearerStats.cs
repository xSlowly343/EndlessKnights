using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class LanternbearerStats
    {
        public static readonly StatId LightPower = new StatId("lightPower");
        public static readonly StatId LightPerHit = new StatId("lightPerHit");
        public static readonly StatId LightShadowSeconds = new StatId("lightShadowSeconds");
        public static readonly StatId LightShadow = new StatId("lightShadow");
        public static readonly StatId LightBoss = new StatId("lightBoss");
        public static readonly StatId LightOil = new StatId("lightOil");
        public static readonly StatId LightCarry = new StatId("lightCarry");
        public static readonly StatId LightNoon = new StatId("lightNoon");
        public static readonly StatId LightFlare = new StatId("lightFlare");
        public static readonly StatId LightParty = new StatId("lightParty");

        public static readonly StatId SparkPower = new StatId("sparkPower");
        public static readonly StatId FireflyHits = new StatId("fireflyHits");
        public static readonly StatId FireflySeconds = new StatId("fireflySeconds");
        public static readonly StatId FireflyMax = new StatId("fireflyMax");
        public static readonly StatId FireflyBurst = new StatId("fireflyBurst");
        public static readonly StatId SparkEvery = new StatId("sparkEvery");
        public static readonly StatId SparkOil = new StatId("sparkOil");

        public static readonly StatId BeaconPower = new StatId("beaconPower");
        public static readonly StatId BeaconSeconds = new StatId("beaconSeconds");
        public static readonly StatId BeaconCharges = new StatId("beaconCharges");
        public static readonly StatId BeaconStrike = new StatId("beaconStrike");
        public static readonly StatId BeaconCut = new StatId("beaconCut");
        public static readonly StatId BeaconMute = new StatId("beaconMute");
        public static readonly StatId BeaconBoss = new StatId("beaconBoss");
        public static readonly StatId BeaconParty = new StatId("beaconParty");
    }
}
