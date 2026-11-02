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
            ["whirlFull"] = CoopSignal.WhirlFull,
            ["breachOpen"] = CoopSignal.BreachOpen,
            ["lunge"] = CoopSignal.Lunge,
            ["touche"] = CoopSignal.Touche,
            ["toucheMarks"] = CoopSignal.ToucheMarks,
            ["phantomFighting"] = CoopSignal.PhantomFighting,
            ["hunterMark"] = CoopSignal.HunterMark,
            ["shot"] = CoopSignal.Shot,
            ["calm"] = CoopSignal.Calm,
            ["gust"] = CoopSignal.Gust,
            ["gusts"] = CoopSignal.Gusts,
            ["crystals"] = CoopSignal.Crystals,
            ["gardenMass"] = CoopSignal.GardenMass,
            ["shellCracked"] = CoopSignal.ShellCracked,
            ["crack"] = CoopSignal.Crack,
            ["sandBottom"] = CoopSignal.SandBottom,
            ["flip"] = CoopSignal.Flip,
            ["camp"] = CoopSignal.Camp,
            ["mirages"] = CoopSignal.Mirages,
            ["verseShare"] = CoopSignal.VerseShare,
            ["verses"] = CoopSignal.Verses,
            ["chorus"] = CoopSignal.Chorus,
            ["songShare"] = CoopSignal.SongShare,
            ["sleepShare"] = CoopSignal.SleepShare,
            ["asleep"] = CoopSignal.Asleep
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
            CoopSignal.Lunge => true,
            CoopSignal.Touche => true,
            CoopSignal.Shot => true,
            CoopSignal.Gust => true,
            CoopSignal.Crack => true,
            CoopSignal.Flip => true,
            CoopSignal.Camp => true,
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
            CoopSignal.BreachOpen => state.Duelist.BreachOpen ? 1 : 0,
            CoopSignal.Lunge => state.Duelist.Lunges,
            CoopSignal.Touche => state.Duelist.Touches,
            CoopSignal.ToucheMarks => state.Duelist.Marks,
            CoopSignal.PhantomFighting => state.Duelist.PhantomFighting ? 1 : 0,
            CoopSignal.HunterMark => state.HunterMarkTicks > 0 ? 1 : 0,
            CoopSignal.Shot => state.Windshot.Shots,
            CoopSignal.Calm => state.Windshot.Calm ? 1 : 0,
            CoopSignal.Gust => state.Windshot.GustsStarted,
            CoopSignal.Gusts => state.Windshot.Gusts,
            CoopSignal.Crystals => state.Shardcaller.Crystals,
            CoopSignal.GardenMass => state.Shardcaller.Crystals * state.Shardcaller.Size,
            CoopSignal.ShellCracked => state.Shardcaller.Cracked ? 1 : 0,
            CoopSignal.Crack => state.Shardcaller.Cracks,
            CoopSignal.SandBottom => state.Sandwalker.Bottom ? 1 : 0,
            CoopSignal.Flip => state.Sandwalker.Flips,
            CoopSignal.Camp => state.Sandwalker.Camps,
            CoopSignal.Mirages => state.Sandwalker.Mirages,
            CoopSignal.VerseShare => state.Minstrel.PartyVerse,
            CoopSignal.Verses => state.Minstrel.Verses,
            CoopSignal.Chorus => state.Minstrel.Chorus ? 1 : 0,
            CoopSignal.SongShare => state.Minstrel.PartySong,
            CoopSignal.SleepShare => state.Minstrel.PartySleep,
            CoopSignal.Asleep => state.Minstrel.Asleep ? 1 : 0,
            _ => 0
        };
    }
}
