using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class GhostcaptainStats
    {
        public static readonly StatId BroadsidePower = new StatId("broadsidePower");
        public static readonly StatId BroadsideHits = new StatId("broadsideHits");
        public static readonly StatId BroadsideCannons = new StatId("broadsideCannons");
        public static readonly StatId BroadsideHalf = new StatId("broadsideHalf");
        public static readonly StatId BroadsideLoaded = new StatId("broadsideLoaded");
        public static readonly StatId BroadsideQueue = new StatId("broadsideQueue");
        public static readonly StatId BroadsideNoBreach = new StatId("broadsideNoBreach");
        public static readonly StatId BroadsidePowder = new StatId("broadsidePowder");
        public static readonly StatId BroadsideParty = new StatId("broadsideParty");

        public static readonly StatId CrewPower = new StatId("crewPower");
        public static readonly StatId CrewDawn = new StatId("crewDawn");
        public static readonly StatId CrewDouble = new StatId("crewDouble");
        public static readonly StatId CrewKeepBoss = new StatId("crewKeepBoss");
        public static readonly StatId CrewQueue = new StatId("crewQueue");
        public static readonly StatId CrewKeep = new StatId("crewKeep");
        public static readonly StatId CrewFarewell = new StatId("crewFarewell");
        public static readonly StatId CrewNoFarewell = new StatId("crewNoFarewell");
        public static readonly StatId CrewParty = new StatId("crewParty");

        public static readonly StatId ParrotPower = new StatId("parrotPower");
        public static readonly StatId ParrotEvery = new StatId("parrotEvery");
        public static readonly StatId ParrotCrit = new StatId("parrotCrit");
        public static readonly StatId ParrotPowder = new StatId("parrotPowder");
        public static readonly StatId ParrotTwo = new StatId("parrotTwo");
        public static readonly StatId ParrotHeavy = new StatId("parrotHeavy");
        public static readonly StatId ParrotQueue = new StatId("parrotQueue");
    }
}
