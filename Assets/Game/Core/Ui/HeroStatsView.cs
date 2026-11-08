using System.Collections.Generic;
using System.Globalization;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct HeroStatLine
    {
        public readonly int Column;
        public readonly string Label;
        public readonly string Value;
        public readonly bool IsHeader;

        public HeroStatLine(int column, string label, string value, bool isHeader = false)
        {
            Column = column;
            Label = label;
            Value = value;
            IsHeader = isHeader;
        }
    }

    public static class HeroStatsView
    {
        public const int Columns = 2;


        public static IReadOnlyList<HeroStatLine> Lines(GameLoop loop, NumberFormatSettings format)
        {
            var lines = new List<HeroStatLine>();
            AddDamage(lines, loop, format);
            AddPartyRole(lines, loop);
            AddFocusAndResource(lines, loop);
            AddAttributes(lines, loop);
            AddPeaceful(lines, loop);
            AddSources(lines, loop);
            return lines;
        }

        public static double AverageHit(double hit, double critChance, double critMultiplier) =>
            hit * (1 + critChance * (critMultiplier - 1));

        public static double AverageHit(double hit, double critChance, double critMultiplier, double overcritChance, double overcritMultiplier) =>
            hit * (1 + critChance * (critMultiplier - 1) + overcritChance * critMultiplier * (overcritMultiplier - 1));

        public static (double Increased, double More) DamageShare(IReadOnlyList<Modifier> modifiers)
        {
            var increased = 0.0;
            var more = 1.0;
            foreach (var modifier in modifiers)
            {
                if (!modifier.AppliesTo(CombatStats.Damage, StatTags.Strike))
                    continue;
                if (modifier.Kind == ModifierKind.Increased)
                    increased += modifier.Value;
                else if (modifier.Kind == ModifierKind.More)
                    more *= 1 + modifier.Value;
            }
            return (increased, more);
        }

        private static void AddDamage(List<HeroStatLine> lines, GameLoop loop, NumberFormatSettings format)
        {
            var combat = loop.Combat;
            var hit = combat.SkillDamage(combat.BaseKeyDamage, 1, StatTags.Strike);
            var crit = combat.CritChance(StatTags.Strike);
            var critMultiplier = combat.CritMultiplier(StatTags.Strike);
            var overcrit = combat.Overcrit(StatTags.Strike);
            var overcritMultiplier = combat.OvercritMultiplier(StatTags.Strike);
            var every = (long)System.Math.Round(loop.Hero.Value(CombatStats.KeyStrikeEvery, 0, StatTags.Strike));
            var heavy = System.Math.Max(1, loop.Hero.Value(CombatStats.KeyStrikeMultiplier, 0, StatTags.Strike));
            var boss = loop.Hero.TotalsOf(CombatStats.DamageVsBoss);
            lines.Add(Header(0, "hero.stats.damage"));
            lines.Add(Line(0, "hero.stats.attack", Invariant("{0:0.#}", combat.Attack)));
            lines.Add(Line(0, "hero.stats.attackFrom", Texts.Format("hero.stats.attackFromShow", TreeNodeText.AttributeName(loop.ClassAttributeDef), Whole(combat.AttributeTotal, format), Whole(combat.WeaponDamage, format), Whole(combat.FlatAttack, format))));
            lines.Add(Line(0, "hero.stats.keyHit", NumberFormatter.Format(hit, format)));
            lines.Add(Line(0, "hero.stats.averageHit", NumberFormatter.Format(BigNumber.FromDouble(AverageHit(hit.ToDouble(), crit, critMultiplier, overcrit, overcritMultiplier)), format)));
            lines.Add(Line(0, "hero.stats.dps", NumberFormatter.Format(loop.State.RecentDps, format)));
            lines.Add(Line(0, "hero.stats.critChance", Invariant("{0:0.#}%", crit * 100)));
            lines.Add(Line(0, "hero.stats.critMultiplier", Invariant("×{0:0.##}", critMultiplier)));
            if (overcrit > 0)
            {
                lines.Add(Line(0, "hero.stats.overcritChance", Invariant("{0:0.#}%", overcrit * 100)));
                lines.Add(Line(0, "hero.stats.overcritMultiplier", Invariant("×{0:0.##}", critMultiplier * overcritMultiplier)));
            }
            AddFracture(lines, loop);
            AddDefense(lines, loop);
            AddElements(lines, loop);
            AddTempo(lines, loop);
            lines.Add(Line(0, "hero.stats.heavyStrike", every >= 1 ? Texts.Format("hero.stats.heavyEvery", every, heavy.ToString("0.#", CultureInfo.InvariantCulture)) : Texts.Get("hero.stats.none")));
            lines.Add(Line(0, "hero.stats.vsBoss", Percent(boss.IncreasedMultiplier * boss.MoreMultiplier - 1)));
            AddGroups(lines, DamageBreakdown.Of(loop));
        }

        public static IReadOnlyList<(string Key, double Factor)> Groups(in DamageBreakdown breakdown) => new[]
        {
            ("hero.stats.increased", breakdown.Increased),
            ("hero.stats.treeBasket", breakdown.TreeBasket),
            ("hero.stats.more", breakdown.More),
            ("hero.stats.additional", breakdown.Additional),
            ("hero.stats.critAverage", breakdown.Crit),
            ("hero.stats.defenseGroup", breakdown.Defense),
            ("hero.stats.elementGroup", breakdown.Element),
            ("hero.stats.fractureGroup", breakdown.Fracture),
            ("hero.stats.tempoGroup", breakdown.Tempo),
            ("hero.stats.pillarGroup", breakdown.Pillar)
        };

        private static void AddGroups(List<HeroStatLine> lines, in DamageBreakdown breakdown)
        {
            lines.Add(Header(0, "hero.stats.groups"));
            foreach (var (key, factor) in Groups(breakdown))
            {
                if (key == "hero.stats.increased" || key == "hero.stats.more" || System.Math.Abs(factor - 1) > 1e-9)
                    lines.Add(Line(0, key, Factor(factor)));
            }
            lines.Add(Line(0, "hero.stats.groupsTotal", Factor(breakdown.Groups)));
        }

        public static string Factor(double factor) =>
            factor < 1000 ? Invariant("×{0:0.##}", factor) : "×" + NumberFormatter.Format(BigNumber.FromDouble(factor), NumberFormatSettings.Default);

        private static string Whole(double value, NumberFormatSettings format) => NumberFormatter.Format(BigNumber.FromDouble(System.Math.Floor(System.Math.Max(0, value))), format);

        private static void AddFracture(List<HeroStatLine> lines, GameLoop loop)
        {
            var combat = loop.Combat;
            var chance = combat.FractureChance(StatTags.Strike);
            if (chance > 0 || loop.State.FractureStacks > 0)
                lines.Add(Line(0, "hero.stats.fracture", Texts.Format("hero.stats.fractureShow", Chance(chance), loop.State.FractureStacks, combat.FractureMax, Invariant("{0:0.##}", 1 + combat.FractureBonus))));
        }

        private static void AddDefense(List<HeroStatLine> lines, GameLoop loop)
        {
            var combat = loop.Combat;
            var defense = loop.State.EnemyDefense;
            if (!(defense > 0))
                return;
            var rules = combat.Defense;
            var gain = rules.Gain(defense, combat.DefenseShred, combat.ArmorPen(StatTags.Strike));
            var cutAfter = 1 - (1 - rules.Cut(defense)) * gain;
            lines.Add(Line(0, "hero.stats.defense", Texts.Format("hero.stats.defenseShow", Chance(rules.Cut(defense)), Chance(cutAfter), Invariant("{0:0.##}", gain))));
        }

        private static void AddElements(List<HeroStatLine> lines, GameLoop loop)
        {
            var state = loop.State;
            if (state.EnemyResists != ElementRules.None)
                lines.Add(Line(0, "hero.stats.enemyElements", Texts.Format("hero.stats.enemyElementsShow", ElementName(state.EnemyResists), ElementName(state.EnemyWeakTo))));
            var parts = new System.Text.StringBuilder();
            for (var i = 0; i < ElementRules.Count; i++)
            {
                var share = loop.Hero.Value(ElementRules.Conversion[i], 0, StatTags.Strike);
                if (!(share > 0))
                    continue;
                if (parts.Length > 0)
                    parts.Append(", ");
                parts.Append(ElementName(i)).Append(' ').Append(Chance(share));
            }
            if (parts.Length > 0)
                lines.Add(Line(0, "hero.stats.conversion", parts.ToString()));
        }

        private static string ElementName(int element) =>
            element == ElementRules.None ? Texts.Get("hero.stats.none") : Texts.Get("element." + ElementRules.IdOf((Element)element));

        private static void AddTempo(List<HeroStatLine> lines, GameLoop loop)
        {
            var combat = loop.Combat;
            var state = loop.State;
            if (loop.Hero.Value(CombatStats.TempoDamage, 0) > 0)
                lines.Add(Line(0, "hero.stats.tempo", Texts.Format("hero.stats.tempoShow", Invariant("{0:0}", state.TempoPerMinute), Percent(combat.TempoBonus))));
            if (combat.RestChargeMax > 0)
                lines.Add(Line(0, "hero.stats.rest", Texts.Format("hero.stats.restShow", Percent(state.RestCharge), Percent(combat.RestChargeMax), state.RestHitsLeft)));
        }

        private static void AddPartyRole(List<HeroStatLine> lines, GameLoop loop)
        {
            var combat = loop.Combat;
            var pillar = System.Math.Max(0, loop.Hero.Value(CombatStats.Pillar, 0));
            if (combat.Giving <= 1 && combat.Receiving <= 1 && pillar <= 0)
                return;
            lines.Add(Header(0, "hero.stats.partyRole"));
            if (combat.Giving > 1)
                lines.Add(Line(0, "hero.stats.cohesion", Percent(combat.Giving - 1)));
            if (combat.Receiving > 1)
                lines.Add(Line(0, "hero.stats.receptivity", Percent(combat.Receiving - 1)));
            if (pillar > 0)
                lines.Add(Line(0, "hero.stats.pillar", Texts.Format("hero.stats.pillarShow", Percent(pillar), Invariant("{0:0.##}", 1 + combat.PillarBonus))));
        }

        private static void AddFocusAndResource(List<HeroStatLine> lines, GameLoop loop)
        {
            var state = loop.State;
            lines.Add(Header(0, "hero.stats.focusAndResource"));
            lines.Add(Line(0, "hero.stats.focus", Invariant("×{0:0.0} / {1:0.#}", state.Focus, loop.Focus.Max)));
            lines.Add(Line(0, "hero.stats.focusGain", Percent(loop.Hero.Value(CombatStats.FocusGain, 1) - 1)));
            lines.Add(Line(0, "hero.stats.focusReset", Texts.Format("hero.stats.seconds", loop.Focus.ResetAfterIdleSeconds.ToString("0.#", CultureInfo.InvariantCulture))));
            if (state.RelayFocus > 0)
                lines.Add(Line(0, "hero.stats.focusRelay", FocusRelayText.Active(state.RelayFocus, loop.Focus.RelaySecondsLeft)));
            lines.Add(Line(0, "hero.stats.resource", Invariant("{0:0} / {1:0}", state.Resource, loop.Resource.Max)));
            lines.Add(Line(0, "hero.stats.resourceGain", Percent(loop.Hero.Value(CombatStats.ResourceGain, 1) - 1)));
        }

        private static void AddAttributes(List<HeroStatLine> lines, GameLoop loop)
        {
            lines.Add(Header(1, "hero.stats.attributes"));
            lines.Add(new HeroStatLine(1, TreeNodeText.AttributeName(loop.ClassAttributeDef), ((long)System.Math.Floor(loop.ClassAttribute)).ToString(CultureInfo.InvariantCulture)));
            foreach (var modifier in loop.ClassAttributeModifiers)
                lines.Add(new HeroStatLine(1, Texts.Get("hero.stats.classFeature"), ModifierText.Describe(modifier)));
        }

        private static void AddPeaceful(List<HeroStatLine> lines, GameLoop loop)
        {
            var bonus = loop.State.PeacefulBonus;
            var fishing = loop.Fishing.Rules;
            var wait = (fishing.BiteMinSeconds + fishing.BiteMaxSeconds) / 2 * (1 - loop.State.Perk(MuseumPerk.BiteFaster));
            var plants = loop.Garden.Rules.Plants;
            var harvest = plants.Count > 0 ? plants[0].Yield + (int)loop.State.Perk(MuseumPerk.ExtraHerb) : 0;
            var legend = fishing.LegendChance + loop.State.Perk(MuseumPerk.LegendChance);
            var fine = loop.State.Perk(MuseumPerk.FinePotion);
            lines.Add(Header(1, "hero.stats.peaceful"));
            lines.Add(Line(1, "hero.stats.peacefulSpeed", Texts.Format("hero.stats.peacefulSpeedShow", Percent(bonus.Speed), Clock(wait), Clock(wait * bonus.TimeFactor))));
            lines.Add(Line(1, "hero.stats.peacefulYield", Texts.Format("hero.stats.peacefulYieldShow", Percent(bonus.Yield), harvest, bonus.More(harvest))));
            lines.Add(Line(1, "hero.stats.peacefulLuck", Texts.Format("hero.stats.peacefulLuckShow", Percent(bonus.Luck), Chance(legend), Chance(legend * bonus.LuckFactor))));
            lines.Add(Line(1, "hero.stats.peacefulMastery", Texts.Format("hero.stats.peacefulMasteryShow", Percent(bonus.Mastery), Chance(fine), Chance(System.Math.Min(1, fine + bonus.MasteryChance)))));
            if (bonus.Party > 0)
                lines.Add(Line(1, "hero.stats.peacefulParty", Invariant("×{0:0.##}", 1 + bonus.Party)));
        }

        public static string Clock(double seconds)
        {
            var whole = (long)System.Math.Round(System.Math.Max(0, seconds));
            return Invariant("{0}:{1:00}", whole / 60, whole % 60);
        }

        private static string Chance(double chance) => Invariant("{0:0.#}%", chance * 100);

        private static void AddSources(List<HeroStatLine> lines, GameLoop loop)
        {
            lines.Add(Header(1, "hero.stats.sources"));
            AddSource(lines, "hero.stats.source.tree", loop.TreeModifiers);
            AddSource(lines, "hero.stats.source.weapons", loop.Weapons.Modifiers);
            AddSource(lines, "hero.stats.source.upgrades", loop.Upgrades.Modifiers);
            AddSource(lines, "hero.stats.source.artifacts", loop.Artifacts.Modifiers);
            AddSource(lines, "hero.stats.source.gems", loop.Synergy.Modifiers);
            AddSource(lines, "hero.stats.source.party", loop.PartyBuffs.Modifiers);
            AddSource(lines, "hero.stats.source.rift", loop.Rift.Modifiers);
        }

        private static void AddSource(List<HeroStatLine> lines, string key, IReadOnlyList<Modifier> modifiers)
        {
            var (increased, more) = DamageShare(modifiers);
            var value = increased == 0 && more == 1
                ? Texts.Get("hero.stats.none")
                : Invariant("{0:+0;-0;0}% · ×{1:0.##}", increased * 100, more);
            lines.Add(Line(1, key, value));
        }

        private static HeroStatLine Header(int column, string key) => new HeroStatLine(column, Texts.Get(key), string.Empty, true);

        private static HeroStatLine Line(int column, string key, string value) => new HeroStatLine(column, Texts.Get(key), value);

        private static string Percent(double share) => Invariant("{0:+0;-0;0}%", share * 100);

        private static string Invariant(string format, double first, double second = 0) => string.Format(CultureInfo.InvariantCulture, format, first, second);
    }
}
