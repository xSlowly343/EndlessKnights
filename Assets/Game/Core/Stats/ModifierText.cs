using System.Collections.Generic;
using System.Globalization;
using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Stats
{
    public static class ModifierText
    {
        private static readonly HashSet<string> FlatPercent = new HashSet<string>
        {
            "critChance", "critMultiplier", "crushCleave", "crushMultiplier", "damagePerSector",
            "doomedCritChance", "doomedCritMultiplier", "dotSpread", "doubleStrike", "doubleHitChance", "damageTaken", "flareMultiplier", "flareSplash",
            "focusMax", "partyContribution", "partyCritChance", "partyCritMultiplier", "pierceShare", "volleySplash",
            "windupKeptAfterCrush", "frozenFirstHit", "overloadKeep", "keyHitQueueAll", "damagePerInputKind", "damagePerTenPoison", "damagePerKill", "idleKillPenalty", "critDamageBuff", "firstHitsBonus",
            "laterHitsPenalty", "minionExplode", "damagePerResource", "keyHitQueueShare", "damagePerCombo", "damagePerComboMax",
            "damageAtBalance", "rampPerSecond", "rampMax", "overloadQueueShare", "goldPerResource", "enemyHpCut", "damagePerAura",
            "echoShare", "doomShare", "doomMultiplier", "dotQueueShare", "skeletonOnKill", "beastOnKill", "minionQueueShare",
            "goldJackpotChance", "wandererChance", "offlineDpsShare", "cooldownPerResource", "cooldownPerResourceMax", "markDamage", "markPower", "companionPerMark", "auraInPause", "overloadBoss", "minionBoss", "mouseQueueShare", "focusIdleFloor",
            "traitHitWeight", "runeRhyme", "runeWeave", "runeFirstRune", "runeCapital", "bladeKeep", "whirlSkillPerFive",
            "breachExpose", "toucheExecute", "toucheOffPenalty", "toucheMarkDamage", "toucheBossHalf", "phantomPower", "phantomSelfPenalty", "phantomEcho",
            "calmPenalty", "partySkillCooldown",
            "shellPenalty",
            "flipPower", "flipPenalty", "mirageSelfPenalty",
            "versePower", "songPower", "songSelfPenalty", "songPartyPenalty",
            "netPower", "netTrophy", "netParty", "albumPower", "albumMax", "prophecyPower", "albumParty",
            "lightPower", "lightPerHit", "lightShadow", "lightCarry", "lightParty", "beaconPower", "beaconParty",
            "knotPower", "knotParty", "namePower", "hushParty",
            "springPower", "springDrain", "springParty", "loopPower",
            "solventPower", "solventParty", "mixParty", "elixirParty",
            "luckPower", "luckParty", "artelPower", "artelPenalty",
            "breathPower", "breathParty", "soulParty", "lendParty",
            "parrotPower", "broadsideParty", "crewParty",
            "mothParty",
            "spreadPower", "forecastPower", "reversedPower", "reversedParty",
            "bloomParty",
            "eternalParty", "eternalRuneBonus", "minorParty"
        };

        private static readonly StatTags[] LabelledTags =
        {
            StatTags.Strike, StatTags.Mouse, StatTags.Click, StatTags.Idle, StatTags.Skill, StatTags.Aura, StatTags.DoT, StatTags.Crit, StatTags.Minion
        };

        public static string Label(StatId stat)
        {
            var key = "stat." + stat.BaseName;
            return Texts.Has(key) ? Texts.Get(key) : stat.Name;
        }

        public static string Subject(Modifier modifier) => Label(modifier.Stat) + TagSuffix(modifier.Tags);

        public static string Describe(Modifier modifier)
        {
            if (CoopLine.IsCoop(modifier.Stat) && CoopLine.TryGet(modifier.Stat, out var line))
                return DescribeCoop(modifier, line);
            var label = Subject(modifier);
            var text = modifier.Kind switch
            {
                ModifierKind.Increased => Texts.Format("modifier.increased", Signed(modifier.Value * 100), label),
                ModifierKind.More => Texts.Format("modifier.more", Signed(modifier.Value * 100), label),
                _ => Texts.Format("modifier.flat", FlatAmount(modifier.Stat, modifier.Value, true), label)
            };
            return text + ConditionSuffix(modifier.When);
        }

        private static string DescribeCoop(Modifier modifier, CoopLine line)
        {
            var buff = Describe(new Modifier(line.Target, line.To == CoopTarget.Enemy ? ModifierKind.Flat : line.Kind, modifier.Value, StatTags.None, modifier.Pool, modifier.When));
            var text = Texts.Format("coop.to." + line.To.ToString().ToLowerInvariant(), buff);
            if (line.HasPer)
                text += " " + Texts.Format("coop.per", Number(line.PerStep), SignalLabel(line.PerSignal), line.PerCap);
            if (line.HasAfter)
                text += " " + Texts.Format("coop.after", SignalLabel(line.AfterSignal), Number(line.AfterSeconds), line.AfterStacks);
            if (line.HasWhile)
                text += " " + Texts.Format("coop.while", SignalLabel(line.WhileSignal));
            if (line.ShareStat is { } share)
                text += " " + Texts.Format("coop.share", Label(share));
            return text;
        }

        private static string SignalLabel(CoopSignal signal)
        {
            var key = "coop.signal." + signal;
            return Texts.Has(key) ? Texts.Get(key) : signal.ToString();
        }

        private static string Number(double value) => System.Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

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

        public static string Bare(Modifier modifier, double value)
        {
            var percent = modifier.Kind != ModifierKind.Flat || FlatPercent.Contains(modifier.Stat.BaseName);
            return Number(System.Math.Abs(percent ? value * 100 : value), false);
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
