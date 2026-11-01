using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class WindshotStats
    {
        public static readonly StatId ShotPower = new StatId("shotPower");
        public static readonly StatId ShotEvery = new StatId("shotEvery");
        public static readonly StatId ShotSpend = new StatId("shotSpend");
        public static readonly StatId ShotPause = new StatId("shotPause");
        public static readonly StatId ShotPierce = new StatId("shotPierce");
        public static readonly StatId ShotRefund = new StatId("shotRefund");
        public static readonly StatId ShotStorm = new StatId("shotStorm");
        public static readonly StatId ShotAuto = new StatId("shotAuto");
        public static readonly StatId ShotPartyCrit = new StatId("shotPartyCrit");

        public static readonly StatId CalmPower = new StatId("calmPower");
        public static readonly StatId CalmPenalty = new StatId("calmPenalty");
        public static readonly StatId CalmStill = new StatId("calmStill");
        public static readonly StatId CalmDrift = new StatId("calmDrift");
        public static readonly StatId CalmTension = new StatId("calmTension");
        public static readonly StatId CalmDead = new StatId("calmDead");
        public static readonly StatId CalmFirst = new StatId("calmFirst");
        public static readonly StatId CalmStorm = new StatId("calmStorm");
        public static readonly StatId CalmEternal = new StatId("calmEternal");
        public static readonly StatId CalmPartyCrit = new StatId("calmPartyCrit");

        public static readonly StatId GustPower = new StatId("gustPower");
        public static readonly StatId GustPx = new StatId("gustPx");
        public static readonly StatId GustHits = new StatId("gustHits");
        public static readonly StatId GustMax = new StatId("gustMax");
        public static readonly StatId GustArrows = new StatId("gustArrows");
        public static readonly StatId GustTension = new StatId("gustTension");
        public static readonly StatId GustStorm = new StatId("gustStorm");
        public static readonly StatId GustPartyDouble = new StatId("gustPartyDouble");
        public static readonly StatId GustQueue = new StatId("gustQueue");
        public static readonly StatId GustTimer = new StatId("gustTimer");
        public static readonly StatId GustPartyCooldown = new StatId("gustPartyCooldown");
    }
}
