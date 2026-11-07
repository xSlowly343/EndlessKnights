using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class PartyRole
    {
        public static double Giving(StatSheet hero) => 1 + System.Math.Max(0, hero.Value(CombatStats.Cohesion, 0));

        public static double Receiving(StatSheet hero) => 1 + System.Math.Max(0, hero.Value(CombatStats.Receptivity, 0));
    }
}
