using System;
using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public static class CoopSignals
    {
        private static readonly Dictionary<string, CoopSignal> Names = new Dictionary<string, CoopSignal>(StringComparer.Ordinal)
        {
            ["resourceFill"] = CoopSignal.ResourceFill,
            ["partyMembers"] = CoopSignal.PartyMembers,
            ["combo"] = CoopSignal.Combo,
            ["killStreak"] = CoopSignal.KillStreak,
            ["crit"] = CoopSignal.Crit,
            ["kill"] = CoopSignal.Kill,
            ["bossKill"] = CoopSignal.BossKill,
            ["skillCast"] = CoopSignal.SkillCast,
            ["keyHit"] = CoopSignal.KeyHit,
            ["runeWord"] = CoopSignal.RuneWord,
            ["bladeRain"] = CoopSignal.BladeRain,
            ["runeSeal"] = CoopSignal.RuneSeal,
            ["blades"] = CoopSignal.Blades,
            ["whirlBlades"] = CoopSignal.WhirlBlades,
            ["whirlFull"] = CoopSignal.WhirlFull
        };

        public static bool TryParse(string name, out CoopSignal signal) => Names.TryGetValue(name, out signal);

        public static bool IsEvent(CoopSignal signal) => signal switch
        {
            CoopSignal.Crit => true,
            CoopSignal.Kill => true,
            CoopSignal.BossKill => true,
            CoopSignal.SkillCast => true,
            CoopSignal.KeyHit => true,
            CoopSignal.RuneWord => true,
            CoopSignal.BladeRain => true,
            _ => false
        };

        public static double Read(CoopSignal signal, GameState state) => signal switch
        {
            CoopSignal.ResourceFill => state.ResourceFill,
            CoopSignal.PartyMembers => state.PartyMembers,
            CoopSignal.Combo => state.Combo,
            CoopSignal.KillStreak => state.KillStreak,
            CoopSignal.Crit => state.Crits,
            CoopSignal.Kill => state.Kills,
            CoopSignal.BossKill => state.BossKills,
            CoopSignal.SkillCast => state.SkillCasts,
            CoopSignal.KeyHit => state.KeyHits,
            CoopSignal.RuneWord => state.Runedancer.Words,
            CoopSignal.BladeRain => state.Runedancer.Rains,
            CoopSignal.RuneSeal => state.Runedancer.SealTicks > 0 ? 1 : 0,
            CoopSignal.Blades => state.Runedancer.Blades,
            CoopSignal.WhirlBlades => state.Runedancer.WhirlBlades,
            CoopSignal.WhirlFull => state.Runedancer.WhirlFull ? 1 : 0,
            _ => 0
        };
    }
}
