using System.Collections.Generic;
using System.Globalization;
using Game.Core.Build;
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
            AddFocusAndResource(lines, loop);
            AddAttributes(lines, loop);
            AddSources(lines, loop);
            return lines;
        }

        public static double AverageHit(double hit, double critChance, double critMultiplier) =>
            hit * (1 + critChance * (critMultiplier - 1));

        public static (double Increased, double More) DamageShare(IReadOnlyList<Modifier> modifiers)
        {
            var increased = 0.0;
            var more = 1.0;
            foreach (var modifier in modifiers)
            {
                if (!modifier.AppliesTo(CombatStats.Damage, StatTags.Keyboard))
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
            var hit = combat.SkillDamage(combat.BaseKeyDamage, 1, StatTags.Keyboard);
            var crit = combat.CritChance(StatTags.Keyboard);
            var critMultiplier = combat.CritMultiplier(StatTags.Keyboard);
            var damage = loop.Hero.TotalsOf(CombatStats.Damage, StatTags.Keyboard);
            var every = (long)System.Math.Round(loop.Hero.Value(CombatStats.KeyStrikeEvery, 0, StatTags.Keyboard));
            var heavy = System.Math.Max(1, loop.Hero.Value(CombatStats.KeyStrikeMultiplier, 0, StatTags.Keyboard));
            var boss = loop.Hero.TotalsOf(CombatStats.DamageVsBoss);
            lines.Add(Header(0, "hero.stats.damage"));
            lines.Add(Line(0, "hero.stats.keyHit", NumberFormatter.Format(hit, format)));
            lines.Add(Line(0, "hero.stats.averageHit", NumberFormatter.Format(BigNumber.FromDouble(AverageHit(hit.ToDouble(), crit, critMultiplier)), format)));
            lines.Add(Line(0, "hero.stats.dps", NumberFormatter.Format(loop.State.RecentDps, format)));
            lines.Add(Line(0, "hero.stats.critChance", Invariant("{0:0.#}%", crit * 100)));
            lines.Add(Line(0, "hero.stats.critMultiplier", Invariant("×{0:0.##}", critMultiplier)));
            lines.Add(Line(0, "hero.stats.increased", Percent(damage.IncreasedMultiplier - 1)));
            lines.Add(Line(0, "hero.stats.more", Invariant("×{0:0.##}", damage.MoreMultiplier)));
            lines.Add(Line(0, "hero.stats.heavyStrike", every >= 1 ? Texts.Format("hero.stats.heavyEvery", every, heavy.ToString("0.#", CultureInfo.InvariantCulture)) : Texts.Get("hero.stats.none")));
            lines.Add(Line(0, "hero.stats.vsBoss", Percent(boss.IncreasedMultiplier * boss.MoreMultiplier - 1)));
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
