using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class RaidText
    {
        private const long MinuteMs = 60_000;
        private const long HourMs = 60 * MinuteMs;
        private const long DayMs = 24 * HourMs;

        public static string Boss(string boss) => Texts.Get("raid.boss." + boss);

        public static string Input(string layerId) => Texts.Get("raid.input." + layerId);

        public static string Layer(string layerId, double left, double hp)
        {
            var name = Texts.Get("raid.layer." + layerId);
            return left <= 0 ? Texts.Format("raid.layerDown", name) : Texts.Format("raid.layerLeft", name, Percent(left / hp));
        }

        public static string Hp(double leftShare) => leftShare <= 0 ? Texts.Get("raid.killed") : Texts.Format("raid.hp", Percent(leftShare));

        public static string Own(double share, int place, int players) =>
            place <= 0 ? Texts.Get("raid.noShare") : Texts.Format("raid.own", (share * 100).ToString("0.#", CultureInfo.InvariantCulture), place, players);

        public static string EndsIn(long endsAtUnixMs, long nowUnixMs)
        {
            var left = System.Math.Max(0, endsAtUnixMs - nowUnixMs);
            if (left >= DayMs)
                return Texts.Format("raid.endsIn.days", left / DayMs, left % DayMs / HourMs);
            if (left >= HourMs)
                return Texts.Format("raid.endsIn.hours", left / HourMs, left % HourMs / MinuteMs);
            return Texts.Format("raid.endsIn.minutes", System.Math.Max(1, left / MinuteMs));
        }

        public static string Last(bool killed, int place, int players, long seals) =>
            place <= 0 ? Texts.Get(killed ? "raid.last.killedWithoutYou" : "raid.last.survivedWithoutYou")
                : Texts.Format(killed ? "raid.last.killed" : "raid.last.survived", place, players, seals);

        private static string Percent(double share) => System.Math.Min(100, System.Math.Ceiling(share * 100 - 1e-9)).ToString("0", CultureInfo.InvariantCulture);
    }
}
