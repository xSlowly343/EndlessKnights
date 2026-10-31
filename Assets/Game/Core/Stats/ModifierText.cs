using System.Collections.Generic;
using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Stats
{
    public static class ModifierText
    {
        private static readonly HashSet<string> FlatPercent = new HashSet<string>
        {
            "critChance", "critMultiplier", "comboStrikeMultiplier", "comboStrikeCritMultiplier", "clockworkSlow", "clearDayCritChance", "crushCleave", "crushMultiplier", "damagePerSector",
            "doomedCritChance", "doomedCritMultiplier", "dotSpread", "doubleStrike", "flareMultiplier", "flareSplash",
            "focusMax", "partyContribution", "partyCritChance", "partyCritMultiplier", "pierceShare", "volleySplash",
            "windupKeptAfterCrush", "gemDrop", "uncutGemChance", "frozenFirstHit", "overloadKeep", "keyHitQueueAll", "damagePerInputKind", "damagePerTenPoison", "cannonNextShare", "cannonShotDamage", "cannonShotDamageMax", "damagePerKill", "idleKillPenalty", "critDamageBuff", "firstHitsBonus",
            "laterHitsPenalty", "minionExplode", "damagePerResource", "keyHitQueueShare", "damagePerCombo", "damagePerComboMax",
            "damageAtBalance", "rampPerSecond", "rampMax", "overloadQueueShare", "goldPerResource", "enemyHpCut", "damagePerAura",
            "echoShare", "doomShare", "doomMultiplier", "dotQueueShare", "skeletonOnKill", "beastOnKill", "minionQueueShare",
            "goldJackpotChance", "wandererChance", "offlineDpsShare", "cooldownPerResource", "cooldownPerResourceMax", "markDamage", "markPower", "companionPerMark", "typingPartyBonus", "overdriveSongs", "choirPower", "choirSoloShare", "auraInPause", "evilEyeEffect", "evilEyePower", "overloadBoss", "minionBoss"
        };

        private static readonly StatTags[] LabelledTags =
        {
            StatTags.Keyboard, StatTags.Mouse, StatTags.Click, StatTags.Idle, StatTags.Skill, StatTags.Aura, StatTags.DoT, StatTags.Crit, StatTags.Minion
        };

        public static string Label(StatId stat)
        {
            var key = "stat." + stat.BaseName;
            return Texts.Has(key) ? Texts.Get(key) : stat.Name;
        }

        public static string Subject(Modifier modifier) => Label(modifier.Stat) + TagSuffix(modifier.Tags);

        public static string Describe(Modifier modifier)
        {
            var label = Subject(modifier);
            var text = modifier.Kind switch
            {
                ModifierKind.Increased => Texts.Format("modifier.increased", Signed(modifier.Value * 100), label),
                ModifierKind.More => Texts.Format("modifier.more", Signed(modifier.Value * 100), label),
                _ => Texts.Format("modifier.flat", FlatAmount(modifier.Stat, modifier.Value, true), label)
            };
            return text + ConditionSuffix(modifier.When);
        }

        public static string ConditionSuffix(ModifierCondition condition)
        {
            if (condition == ModifierCondition.Always)
                return string.Empty;
            var key = ModifierConditions.Key(condition);
            return " " + (Texts.Has(key) ? Texts.Get(key) : condition.ToString());
        }

        public static string FlatAmount(StatId stat, double value, bool signed)
        {
            var name = stat.BaseName;
            if (FlatPercent.Contains(name))
                return Texts.Format("unit.percent", Number(value * 100, signed));
            if (name.EndsWith("Seconds"))
                return Texts.Format("unit.seconds", Number(value, signed));
            if (name.EndsWith("Hours"))
                return Texts.Format("unit.hours", Number(value, signed));
            return Number(value, signed);
        }

        public static string Percent(double share) => Texts.Format("unit.percent", Signed(share * 100));

        public static string Times(double multiplier) => "×" + System.Math.Round(multiplier, 2).ToString("0.00", CultureInfo.InvariantCulture);

        private static string Number(double value, bool signed) =>
            signed ? Signed(value) : System.Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

        private static string TagSuffix(StatTags tags)
        {
            var suffix = string.Empty;
            foreach (var tag in LabelledTags)
                if ((tags & tag) != 0)
                    suffix += " " + Texts.Get("tag." + tag);
            return suffix;
        }

        private static string Signed(double value)
        {
            var text = System.Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
            return value >= 0 ? "+" + text : text;
        }
    }
}
