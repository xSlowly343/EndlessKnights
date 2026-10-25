using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public static class TreeGlyph
    {
        public const string Fallback = "damage";

        private static readonly Dictionary<string, string> ByStat = new Dictionary<string, string>
        {
            ["damage"] = "damage",
            ["damagePerKill"] = "damage",
            ["damagePerSector"] = "damage",
            ["firstHitsBonus"] = "damage",
            ["laterHitsPenalty"] = "damage",
            ["idleKillPenalty"] = "damage",
            ["arrowDamage"] = "arrow",
            ["cannonCount"] = "arrow",
            ["critChance"] = "crit",
            ["partyCritChance"] = "crit",
            ["critDamageBuff"] = "crit_power",
            ["critMultiplier"] = "crit_power",
            ["keyStrikeEvery"] = "crit_power",
            ["keyStrikeMultiplier"] = "crit_power",
            ["comboStrikeMultiplier"] = "crit_power",
            ["overloadMultiplier"] = "crit_power",
            ["overloadDisabled"] = "crit_power",
            ["resourceGain"] = "resource",
            ["resourceMax"] = "resource",
            ["partyResource"] = "resource",
            ["auraReserve"] = "resource",
            ["dotDamage"] = "dot",
            ["poisonStacks"] = "dot",
            ["dotSpread"] = "dot",
            ["dotDuration"] = "duration",
            ["burnSeconds"] = "duration",
            ["minionSeconds"] = "duration",
            ["curseSeconds"] = "duration",
            ["auraDuration"] = "duration",
            ["curseEffect"] = "curse",
            ["flickCurseBonus"] = "curse",
            ["skillCooldown"] = "cooldown",
            ["skillSlots"] = "cooldown",
            ["damageVsBoss"] = "boss",
            ["bossTimerSlow"] = "boss_timer",
            ["bossTimerSeconds"] = "boss_timer",
            ["mouseDistance"] = "mouse",
            ["mouseCountsAsKeys"] = "mouse",
            ["focusGain"] = "focus",
            ["focusMax"] = "focus",
            ["focusResetSeconds"] = "focus_timer",
            ["idleStartSeconds"] = "focus_timer",
            ["auraEffect"] = "aura",
            ["bannerAuraBonus"] = "aura",
            ["concurrentAuras"] = "aura",
            ["partyDamage"] = "party",
            ["partyGold"] = "party",
            ["loopShare"] = "party",
            ["burnDamage"] = "burn",
            ["overheatSeconds"] = "heat",
            ["overheatDisabled"] = "heat",
            ["pressureDecay"] = "heat",
            ["minionSpeed"] = "minion",
            ["minionCount"] = "minion",
            ["minionExplode"] = "minion",
            ["gold"] = "gold",
            ["offlineGold"] = "offline",
            ["offlineCapHours"] = "offline",
            ["gemDrop"] = "gem_drop",
            ["songMouseSeconds"] = "song",
            ["readingGain"] = "reading",
            ["comboTimeoutSeconds"] = "combo",
            ["rageBurnDelaySeconds"] = "rage",
            ["chainTargets"] = "chain",
            ["keyHitLightning"] = "chain",
            ["strength"] = "strength",
            ["dexterity"] = "dexterity",
            ["intelligence"] = "intelligence"
        };

        public static bool TryOfStat(StatId stat, out string glyph) => ByStat.TryGetValue(stat.Name, out glyph!);

        public static string Of(TreeNodeDef node)
        {
            switch (node.Type)
            {
                case TreeNodeType.Keystone:
                    return "keystone";
                case TreeNodeType.Mastery:
                    return "reading";
                case TreeNodeType.Start:
                    return "start";
                case TreeNodeType.Jewel:
                    return string.Empty;
            }
            foreach (var modifier in node.Modifiers)
                if (TryOfStat(modifier.Stat, out var glyph))
                    return glyph;
            return Fallback;
        }
    }
}
