using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class DamagePreviewText
    {
        public const double Unchanged = 1e-4;

        public static string Summary(in DamageChange change)
        {
            if (!change.Possible || (System.Math.Abs(change.Hit - 1) < Unchanged && System.Math.Abs(change.BossHit - 1) < Unchanged))
                return Texts.Get("preview.none");
            var hit = Texts.Format("preview.hit", HeroStatsView.Factor(change.Hit));
            return System.Math.Abs(change.BossHit - change.Hit) < Unchanged ? hit : hit + ", " + Texts.Format("preview.boss", HeroStatsView.Factor(change.BossHit));
        }

        public static IReadOnlyList<HeroStatLine> Groups(in DamageChange change)
        {
            var lines = new List<HeroStatLine>();
            if (!change.Possible)
                return lines;
            var before = HeroStatsView.Groups(change.Before);
            var after = HeroStatsView.Groups(change.After);
            if (System.Math.Abs(DamageChange.Ratio(change.Before.Attack, change.After.Attack) - 1) >= Unchanged)
                lines.Add(Line("hero.stats.attack", HeroStatsView.Factor(DamageChange.Ratio(change.Before.Attack, change.After.Attack))));
            for (var i = 0; i < before.Count; i++)
            {
                var ratio = DamageChange.Ratio(before[i].Factor, after[i].Factor);
                if (System.Math.Abs(ratio - 1) >= Unchanged)
                    lines.Add(Line(before[i].Key, HeroStatsView.Factor(ratio)));
            }
            var boss = DamageChange.Ratio(change.Before.VsBoss, change.After.VsBoss);
            if (System.Math.Abs(boss - 1) >= Unchanged)
                lines.Add(Line("hero.stats.vsBoss", HeroStatsView.Factor(boss)));
            return lines;
        }

        private static HeroStatLine Line(string key, string value) => new HeroStatLine(0, Texts.Get(key), value);
    }
}
