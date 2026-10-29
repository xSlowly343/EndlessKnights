using System.Collections.Generic;
using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PartyChronicleText
    {
        public static string Feat(string id) => Texts.Get("chronicle.feat." + id);

        public static string Title(string id) => Texts.Get("chronicle.title." + id);

        public static string Banner(int level) => level > 0 ? Texts.Format("chronicle.banner", level) : Texts.Get("chronicle.noBanner");

        public static string Goal(string kind, bool tracked, int level, IReadOnlyList<double> levels, double progress)
        {
            if (!tracked)
                return Texts.Get("chronicle.later");
            if (levels.Count == 0)
                return string.Empty;
            var target = levels[System.Math.Min(level, levels.Count - 1)];
            var shown = System.Math.Min(System.Math.Floor(progress + 1e-9), target);
            var goal = Texts.Format("chronicle.goal." + kind, Number(shown), Number(target));
            return levels.Count > 1 ? Texts.Format("chronicle.level", goal, System.Math.Min(level + 1, levels.Count), levels.Count) : goal;
        }

        public static string Earned(long atUnixMs, int utcOffsetMinutes) => Texts.Format("chronicle.done", Day(atUnixMs, utcOffsetMinutes));

        public static string Record(string kind, long atUnixMs, string name, string feat, int utcOffsetMinutes)
        {
            var day = Day(atUnixMs, utcOffsetMinutes);
            return kind switch
            {
                "created" => Texts.Format("chronicle.record.created", day),
                "joined" => Texts.Format("chronicle.record.joined", day, name),
                "left" => Texts.Format("chronicle.record.left", day, name),
                "feat" => Texts.Format("chronicle.record.feat", day, Feat(feat)),
                "founded" => Texts.Format("chronicle.record.founded", day, name),
                "raid" => Texts.Format("chronicle.record.raid", day),
                "node" => Texts.Format("chronicle.record.node", day, name),
                _ => day
            };
        }

        private static string Day(long atUnixMs, int utcOffsetMinutes) =>
            System.DateTimeOffset.FromUnixTimeMilliseconds(atUnixMs).ToOffset(System.TimeSpan.FromMinutes(utcOffsetMinutes)).ToString("dd.MM", CultureInfo.InvariantCulture);

        private static string Number(double value) => value.ToString("0", CultureInfo.InvariantCulture);
    }
}
