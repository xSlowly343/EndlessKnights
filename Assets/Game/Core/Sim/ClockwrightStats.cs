using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class ClockwrightStats
    {
        public static readonly StatId SpringPower = new StatId("springPower");
        public static readonly StatId SpringDrain = new StatId("springDrain");
        public static readonly StatId SpringTight = new StatId("springTight");
        public static readonly StatId SpringFast = new StatId("springFast");
        public static readonly StatId SpringFull = new StatId("springFull");
        public static readonly StatId SpringAllies = new StatId("springAllies");
        public static readonly StatId SpringForever = new StatId("springForever");
        public static readonly StatId SpringBurst = new StatId("springBurst");
        public static readonly StatId SpringStrike = new StatId("springStrike");
        public static readonly StatId SpringParty = new StatId("springParty");

        public static readonly StatId LoopPower = new StatId("loopPower");
        public static readonly StatId LoopSeconds = new StatId("loopSeconds");
        public static readonly StatId LoopWindow = new StatId("loopWindow");
        public static readonly StatId LoopTwice = new StatId("loopTwice");
        public static readonly StatId LoopBoss = new StatId("loopBoss");
        public static readonly StatId LoopOwn = new StatId("loopOwn");
        public static readonly StatId LoopStop = new StatId("loopStop");
        public static readonly StatId LoopParty = new StatId("loopParty");

        public static readonly StatId GearPower = new StatId("gearPower");
        public static readonly StatId GearHits = new StatId("gearHits");
        public static readonly StatId GearOwn = new StatId("gearOwn");
        public static readonly StatId GearReset = new StatId("gearReset");
        public static readonly StatId GearMesh = new StatId("gearMesh");
        public static readonly StatId GearIdle = new StatId("gearIdle");
        public static readonly StatId GearResource = new StatId("gearResource");
        public static readonly StatId GearParty = new StatId("gearParty");
    }
}
