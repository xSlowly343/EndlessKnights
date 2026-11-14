using System.Collections.Generic;
using System.Globalization;
using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PartyGrowthView
    {
        public const double Unchanged = 1e-4;

        private static readonly (PartyGrowthSource Source, string Key)[] SourceKeys =
        {
            (PartyGrowthSource.Tree, "party.view.source.tree"),
            (PartyGrowthSource.Seals, "party.view.source.seals"),
            (PartyGrowthSource.Relics, "party.view.source.relics"),
            (PartyGrowthSource.Mascot, "party.view.source.mascot"),
            (PartyGrowthSource.Rites, "party.view.source.rites"),
            (PartyGrowthSource.Other, "party.view.source.other")
        };

        public static string Banner(PartyGrowthLayer layer) =>
            layer.InParty ? "×" + layer.DamageMultiplier.ToString("0.0#", CultureInfo.InvariantCulture) : string.Empty;

        public static string CallPopup(PartyMascotRules rules, string beast, double seconds)
        {
            var def = rules.Beast(beast);
            return def == null ? string.Empty : Texts.Format("party.call", Texts.Pick(def.NameRu, def.NameEn), seconds.ToString("0", CultureInfo.InvariantCulture));
        }

        public static string GainSummary(in PartyGain gain)
        {
            var lines = Gain(gain);
            return lines.Count == 0 ? Texts.Get("party.gain.none") : lines[0].Label + " " + lines[0].Value;
        }

        public static IReadOnlyList<HeroStatLine> Gain(in PartyGain gain)
        {
            var lines = new List<HeroStatLine>();
            var before = gain.Before;
            var after = gain.After;
            AddFactor(lines, "party.gain.damage", before.Damage, after.Damage);
            AddFactor(lines, "party.gain.boss", before.VsBoss, after.VsBoss);
            AddFactor(lines, "party.gain.guardian", before.VsGuardian, after.VsGuardian);
            AddFactor(lines, "party.gain.keys", before.Keys, after.Keys);
            AddFactor(lines, "party.gain.mouse", before.Mouse, after.Mouse);
            AddFactor(lines, "party.gain.skills", before.Skills, after.Skills);
            AddFactor(lines, "party.gain.together", before.Together, after.Together);
            AddFactor(lines, "party.gain.roleBuffs", before.RoleBuffs, after.RoleBuffs);
            AddFactor(lines, "party.gain.colossus", before.Colossus, after.Colossus);
            AddShare(lines, "party.gain.crit", before.CritChance, after.CritChance);
            AddShare(lines, "party.gain.peaceful", before.Peaceful, after.Peaceful);
            return lines;
        }

        public static IReadOnlyList<HeroStatLine> Window(PartyGrowthLayer layer)
        {
            var summary = PartyGrowthSummary.Of(layer);
            var lines = new List<HeroStatLine>();
            lines.Add(new HeroStatLine(0, Texts.Get("party.view.damage"), HeroStatsView.Factor(summary.Damage), true));
            lines.Add(new HeroStatLine(0, Texts.Get("party.view.compare"), Texts.Format("party.view.compareValue", HeroStatsView.Factor(summary.Damage), HeroStatsView.Factor(1))));
            foreach (var (source, key) in SourceKeys)
            {
                var factor = summary.Of(source);
                if (System.Math.Abs(factor - 1) >= Unchanged)
                    lines.Add(new HeroStatLine(0, Texts.Get(key), HeroStatsView.Factor(factor)));
            }
            if (summary.Capped)
                lines.Add(new HeroStatLine(0, Texts.Get("party.view.capped"), HeroStatsView.Factor(layer.Rules.DamageCap)));
            if (summary.InParty && summary.Share < 1)
                lines.Add(new HeroStatLine(0, Texts.Get("party.view.newcomer"), Percent(summary.Share)));
            lines.Add(new HeroStatLine(1, Texts.Get("party.view.targets"), string.Empty, true));
            AddTarget(lines, "party.gain.boss", summary.VsBoss);
            AddTarget(lines, "party.gain.guardian", summary.VsGuardian);
            AddTarget(lines, "party.gain.colossus", summary.Colossus);
            AddTarget(lines, "party.gain.keys", summary.Keys);
            AddTarget(lines, "party.gain.mouse", summary.Mouse);
            AddTarget(lines, "party.gain.skills", summary.Skills);
            AddTarget(lines, "party.gain.together", summary.Together);
            AddTarget(lines, "party.gain.roleBuffs", summary.RoleBuffs);
            if (summary.CritChance > 0)
                lines.Add(new HeroStatLine(1, Texts.Get("party.gain.crit"), "+" + Percent(summary.CritChance)));
            lines.Add(new HeroStatLine(2, Texts.Get("party.view.peaceful"), "+" + Percent(summary.Peaceful), true));
            return lines;
        }

        private static void AddTarget(List<HeroStatLine> lines, string key, double factor)
        {
            if (System.Math.Abs(factor - 1) >= Unchanged)
                lines.Add(new HeroStatLine(1, Texts.Get(key), HeroStatsView.Factor(factor)));
        }

        private static void AddFactor(List<HeroStatLine> lines, string key, double before, double after)
        {
            if (System.Math.Abs(after - before) < Unchanged)
                return;
            var change = before > 0 ? after / before - 1 : 0;
            lines.Add(new HeroStatLine(0, Texts.Get(key), Texts.Format("party.gain.factor", HeroStatsView.Factor(before), HeroStatsView.Factor(after), Signed(change))));
        }

        private static void AddShare(List<HeroStatLine> lines, string key, double before, double after)
        {
            if (System.Math.Abs(after - before) < Unchanged)
                return;
            lines.Add(new HeroStatLine(0, Texts.Get(key), Texts.Format("party.gain.share", "+" + Percent(before), "+" + Percent(after))));
        }

        private static string Percent(double share) => (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        private static string Signed(double share) => (share >= 0 ? "+" : "") + Percent(share);
    }
}
