using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class SandwalkerStats
    {
        public static readonly StatId FlipPower = new StatId("flipPower");
        public static readonly StatId FlipEvery = new StatId("flipEvery");
        public static readonly StatId FlipPenalty = new StatId("flipPenalty");
        public static readonly StatId FlipSand = new StatId("flipSand");
        public static readonly StatId FlipBySkill = new StatId("flipBySkill");
        public static readonly StatId FlipStep = new StatId("flipStep");
        public static readonly StatId FlipBlind = new StatId("flipBlind");
        public static readonly StatId FlipBossOnly = new StatId("flipBossOnly");

        public static readonly StatId CaravanPower = new StatId("caravanPower");
        public static readonly StatId CampPx = new StatId("campPx");
        public static readonly StatId CampSand = new StatId("campSand");
        public static readonly StatId CampCut = new StatId("campCut");
        public static readonly StatId CaravanIdle = new StatId("caravanIdle");
        public static readonly StatId CaravanHitPx = new StatId("caravanHitPx");
        public static readonly StatId CaravanOasis = new StatId("caravanOasis");
        public static readonly StatId CaravanGold = new StatId("caravanGold");
        public static readonly StatId CaravanAll = new StatId("caravanAll");
        public static readonly StatId CaravanPartyCooldown = new StatId("caravanPartyCooldown");

        public static readonly StatId MiragePower = new StatId("miragePower");
        public static readonly StatId MiragePx = new StatId("miragePx");
        public static readonly StatId MirageMax = new StatId("mirageMax");
        public static readonly StatId MirageSeconds = new StatId("mirageSeconds");
        public static readonly StatId MirageTime = new StatId("mirageTime");
        public static readonly StatId MirageSelfPenalty = new StatId("mirageSelfPenalty");
        public static readonly StatId MirageOnBoss = new StatId("mirageOnBoss");
        public static readonly StatId MirageHits = new StatId("mirageHits");
        public static readonly StatId MirageEcho = new StatId("mirageEcho");
        public static readonly StatId MirageBlind = new StatId("mirageBlind");
    }
}
