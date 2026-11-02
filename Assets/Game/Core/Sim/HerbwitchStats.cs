using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class HerbwitchStats
    {
        public static readonly StatId BloomPower = new StatId("bloomPower");
        public static readonly StatId BloomStart = new StatId("bloomStart");
        public static readonly StatId BloomVines = new StatId("bloomVines");
        public static readonly StatId BloomHits = new StatId("bloomHits");
        public static readonly StatId BloomGrow = new StatId("bloomGrow");
        public static readonly StatId BloomReplant = new StatId("bloomReplant");
        public static readonly StatId BloomThorn = new StatId("bloomThorn");
        public static readonly StatId BloomQueue = new StatId("bloomQueue");
        public static readonly StatId BloomSingle = new StatId("bloomSingle");
        public static readonly StatId BloomParty = new StatId("bloomParty");

        public static readonly StatId MyceliumPower = new StatId("myceliumPower");
        public static readonly StatId MyceliumQueue = new StatId("myceliumQueue");
        public static readonly StatId MyceliumDeep = new StatId("myceliumDeep");
        public static readonly StatId MyceliumDouble = new StatId("myceliumDouble");
        public static readonly StatId MyceliumNoCarry = new StatId("myceliumNoCarry");
        public static readonly StatId MyceliumMax = new StatId("myceliumMax");
        public static readonly StatId MyceliumFast = new StatId("myceliumFast");
        public static readonly StatId MyceliumParty = new StatId("myceliumParty");

        public static readonly StatId SapPower = new StatId("sapPower");
        public static readonly StatId SapHits = new StatId("sapHits");
        public static readonly StatId SapHeal = new StatId("sapHeal");
        public static readonly StatId SapLeave = new StatId("sapLeave");
        public static readonly StatId SapPoison = new StatId("sapPoison");
        public static readonly StatId SapGreedy = new StatId("sapGreedy");
        public static readonly StatId SapParty = new StatId("sapParty");
    }
}
