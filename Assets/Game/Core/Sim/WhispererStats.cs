using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class WhispererStats
    {
        public static readonly StatId KnotPower = new StatId("knotPower");
        public static readonly StatId KnotHits = new StatId("knotHits");
        public static readonly StatId KnotBoss = new StatId("knotBoss");
        public static readonly StatId KnotCarry = new StatId("knotCarry");
        public static readonly StatId KnotTriple = new StatId("knotTriple");
        public static readonly StatId KnotDouble = new StatId("knotDouble");
        public static readonly StatId KnotBlast = new StatId("knotBlast");
        public static readonly StatId KnotWhisper = new StatId("knotWhisper");
        public static readonly StatId KnotParty = new StatId("knotParty");

        public static readonly StatId NamePower = new StatId("namePower");
        public static readonly StatId NameHits = new StatId("nameHits");
        public static readonly StatId NameBlast = new StatId("nameBlast");
        public static readonly StatId NameMemory = new StatId("nameMemory");
        public static readonly StatId NameProgress = new StatId("nameProgress");
        public static readonly StatId NameTime = new StatId("nameTime");

        public static readonly StatId VowPower = new StatId("vowPower");
        public static readonly StatId HushHits = new StatId("hushHits");
        public static readonly StatId HushAll = new StatId("hushAll");
        public static readonly StatId VowGrow = new StatId("vowGrow");
        public static readonly StatId VowBreak = new StatId("vowBreak");
        public static readonly StatId VowQueue = new StatId("vowQueue");
        public static readonly StatId VowCurse = new StatId("vowCurse");
        public static readonly StatId HushParty = new StatId("hushParty");
    }
}
