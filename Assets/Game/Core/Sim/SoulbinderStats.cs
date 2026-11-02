using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class SoulbinderStats
    {
        public static readonly StatId SoulPower = new StatId("soulPower");
        public static readonly StatId SoulMax = new StatId("soulMax");
        public static readonly StatId SoulPush = new StatId("soulPush");
        public static readonly StatId SoulMini = new StatId("soulMini");
        public static readonly StatId SoulHungry = new StatId("soulHungry");
        public static readonly StatId SoulRelease = new StatId("soulRelease");
        public static readonly StatId SoulQueue = new StatId("soulQueue");
        public static readonly StatId SoulParty = new StatId("soulParty");

        public static readonly StatId BreathPower = new StatId("breathPower");
        public static readonly StatId BreathQueue = new StatId("breathQueue");
        public static readonly StatId BreathBoss = new StatId("breathBoss");
        public static readonly StatId BreathColdSeconds = new StatId("breathColdSeconds");
        public static readonly StatId BreathSmall = new StatId("breathSmall");
        public static readonly StatId BreathColdHits = new StatId("breathColdHits");
        public static readonly StatId BreathTwice = new StatId("breathTwice");
        public static readonly StatId BreathNoCold = new StatId("breathNoCold");
        public static readonly StatId BreathParty = new StatId("breathParty");

        public static readonly StatId LendPower = new StatId("lendPower");
        public static readonly StatId LendHits = new StatId("lendHits");
        public static readonly StatId LendSeconds = new StatId("lendSeconds");
        public static readonly StatId LendTwo = new StatId("lendTwo");
        public static readonly StatId LendBack = new StatId("lendBack");
        public static readonly StatId LendAll = new StatId("lendAll");
        public static readonly StatId LendParty = new StatId("lendParty");
    }
}
