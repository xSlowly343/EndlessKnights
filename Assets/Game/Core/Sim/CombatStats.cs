using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class CombatStats
    {
        public static readonly StatId Damage = new StatId("damage");
        public static readonly StatId CritChance = new StatId("critChance");
        public static readonly StatId CritMultiplier = new StatId("critMultiplier");
    }
}
