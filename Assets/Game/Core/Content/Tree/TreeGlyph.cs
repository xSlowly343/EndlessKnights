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

        private const string SectorScopePrefix = "sector:";

        private static readonly Dictionary<string, string> ClassAttribute = new Dictionary<string, string>
        {
            ["viking"] = "red",
            ["ronin"] = "red",
            ["metalhead"] = "red",
            ["bannerbearer"] = "red",
            ["plague_doctor"] = "red",
            ["amazon"] = "green",
            ["stormweaver"] = "green",
            ["oracle"] = "green",
            ["captain"] = "green",
            ["voodoo"] = "blue",
            ["clockmaker"] = "blue",
            ["moonshiner"] = "blue",
            ["gravedigger"] = "blue",
            ["nomad"] = "white"
        };

        public static string AttributeOf(TreeNodeDef node)
        {
            if (node.Scope.StartsWith(SectorScopePrefix, System.StringComparison.Ordinal))
                switch (node.Scope.Substring(SectorScopePrefix.Length))
                {
                    case "strength":
                        return "red";
                    case "dexterity":
                        return "green";
                    case "intelligence":
                        return "blue";
                }
            return node.ScopeClass != null && ClassAttribute.TryGetValue(node.ScopeClass, out var attribute) ? attribute : "white";
        }

        public static bool TryOfStat(StatId stat, out string glyph) => ByStat.TryGetValue(stat.Name, out glyph!);

        public static string Of(TreeNodeDef node)
        {
            switch (node.Type)
            {
                case TreeNodeType.Keystone:
                    return "key_" + node.Id;
                case TreeNodeType.Mastery:
                    return "reading";
                case TreeNodeType.Start:
                    return "start_" + node.StartClass;
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
