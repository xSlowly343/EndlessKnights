using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content.Gems;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public enum PartyAuraStatus
    {
        Working,
        HeroOnly,
        OwnStronger,
        AllyStronger
    }

    public sealed class PartyAuraRow
    {
        public int Giver { get; set; }
        public GemDef Gem { get; set; } = null!;
        public AuraTheme Theme { get; set; }
        public string Effect { get; set; } = string.Empty;
        public bool Timed { get; set; }
        public double SecondsLeft { get; set; }
        public double Reserve { get; set; }
        public PartyAuraStatus Status { get; set; }
        public int CoveredBy { get; set; } = PartyBuffs.NoAlly;

        public bool Works => Status == PartyAuraStatus.Working || Status == PartyAuraStatus.HeroOnly;
    }

    public static class PartyAuraList
    {
        public const int Hero = -1;

        private const string EffectSeparator = ", ";

        public static void Fill(IReadOnlyList<SkillSlot> skills, PartyBuffs? party, AuraShowRules rules, List<PartyAuraRow> into)
        {
            into.Clear();
            AddHero(skills, rules, into);
            if (party == null)
                return;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var ally = party.Ally(slot);
                if (ally.Present)
                    AddAlly(party, slot, ally, rules, into);
            }
        }

        public static int Working(IReadOnlyList<PartyAuraRow> rows)
        {
            var working = 0;
            foreach (var row in rows)
            {
                if (row.Works)
                    working++;
            }
            return working;
        }

        public static string Summary(IReadOnlyList<PartyAuraRow> rows) =>
            rows.Count == 0 ? Texts.Get("aura.list.empty") : Texts.Format("aura.list.summary", Working(rows), rows.Count);

        public static string OpenLabel(IReadOnlyList<PartyAuraRow> rows) => Texts.Format("aura.list.open", Working(rows));

        public static string Giver(PartyAuraRow row, Func<int, string> allyName) => row.Giver == Hero ? Texts.Get("aura.list.you") : allyName(row.Giver);

        public static string Kind(PartyAuraRow row)
        {
            if (row.Timed)
                return Texts.Format("aura.list.left", System.Math.Max(0, (int)System.Math.Ceiling(row.SecondsLeft)));
            return row.Reserve > 0 ? Texts.Format("aura.list.reserve", row.Reserve.ToString("0.##", CultureInfo.InvariantCulture)) : string.Empty;
        }

        public static string Status(PartyAuraRow row, Func<int, string> allyName) => row.Status switch
        {
            PartyAuraStatus.HeroOnly => Texts.Get("aura.list.status.heroOnly"),
            PartyAuraStatus.OwnStronger => Texts.Get("aura.list.status.ownStronger"),
            PartyAuraStatus.AllyStronger => Texts.Format("aura.list.status.allyStronger", allyName(row.CoveredBy)),
            _ => Texts.Get("aura.list.status.working")
        };

        public static string ThemeName(AuraTheme theme) => Texts.Get("aura.theme." + AuraThemes.Key(theme));

        private static void AddHero(IReadOnlyList<SkillSlot> skills, AuraShowRules rules, List<PartyAuraRow> into)
        {
            foreach (var slot in skills)
            {
                if (!slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura))
                    continue;
                var timed = !slot.Def.Reserves && slot.ActiveTicks > 0;
                into.Add(new PartyAuraRow
                {
                    Giver = Hero,
                    Gem = slot.Skill,
                    Theme = rules.ThemeOf(slot.Skill),
                    Effect = Describe(slot.AuraModifiers, 1),
                    Timed = timed,
                    SecondsLeft = timed ? slot.ActiveTicks * GameLoop.TickSeconds : 0,
                    Reserve = slot.ReservedApplied > 0 ? slot.ReservedApplied : aura.Reserve,
                    Status = aura.Party ? PartyAuraStatus.Working : PartyAuraStatus.HeroOnly
                });
            }
        }

        private static void AddAlly(PartyBuffs party, int slot, AllyOffer ally, AuraShowRules rules, List<PartyAuraRow> into)
        {
            var receiving = party.Receiving;
            foreach (var aura in ally.Auras)
            {
                if (!aura.Active || !PartyBuffs.Shared(aura.Gem!))
                    continue;
                var gem = aura.Gem!;
                var def = gem.Skill!;
                var timed = !def.Reserves && def.DurationSeconds > 0;
                var stronger = party.StrongerAlly(slot, aura);
                into.Add(new PartyAuraRow
                {
                    Giver = slot,
                    Gem = gem,
                    Theme = rules.ThemeOf(gem),
                    Effect = AllyEffect(gem, aura.Strength * receiving),
                    Timed = timed,
                    SecondsLeft = timed ? aura.TicksLeft * GameLoop.TickSeconds : 0,
                    Reserve = (def.Effect as AuraEffect)?.Reserve ?? 0,
                    Status = aura.Suppressed ? PartyAuraStatus.OwnStronger : stronger != PartyBuffs.NoAlly ? PartyAuraStatus.AllyStronger : PartyAuraStatus.Working,
                    CoveredBy = aura.Suppressed ? PartyBuffs.NoAlly : stronger
                });
            }
        }

        private static string AllyEffect(GemDef gem, double strength)
        {
            if (gem.Skill?.Effect is AuraEffect aura)
                return Describe(aura.Modifiers, strength);
            var stacks = SkillSystem.StacksOf(gem);
            return stacks == null ? string.Empty : ModifierText.Describe(new Modifier(stacks.PerStack.Stat, stacks.PerStack.Kind, strength, stacks.PerStack.Tags));
        }

        private static string Describe(IReadOnlyList<Modifier> modifiers, double scale)
        {
            var parts = new string[modifiers.Count];
            for (var i = 0; i < modifiers.Count; i++)
                parts[i] = ModifierText.Describe(modifiers[i].WithValue(modifiers[i].Value * scale));
            return string.Join(EffectSeparator, parts);
        }
    }
}
