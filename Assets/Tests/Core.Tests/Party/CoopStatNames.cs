using Game.Core.Party;
using Game.Core.Stats;
using Game.Core.Tests.Sim;

namespace Game.Core.Tests.Party
{
    public static class CoopStatNames
    {
        public const string UnknownPartyBuff = "unknown party buff";

        public static string MechanicOf(Modifier modifier)
        {
            if (!CoopLine.IsCoop(modifier.Stat))
                return modifier.Stat.BaseName;
            if (!CoopLine.TryGet(modifier.Stat, out var line))
                return UnknownPartyBuff;
            if (line.To == CoopTarget.Enemy)
                return CoopRules.IsEnemyStat(line.Target) ? Game.Core.Sim.CombatStats.Damage.Name : UnknownPartyBuff;
            return TestWorld.Data.Party.Buffs.Coop.IndexOf(line.Target, line.Kind) >= 0 ? line.Target.Name : UnknownPartyBuff;
        }
    }
}
