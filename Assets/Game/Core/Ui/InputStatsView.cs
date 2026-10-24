using System.Collections.Generic;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class InputStatsView
    {
        public static string SessionTitle => Texts.Get("stats.session");
        public static string AllTimeTitle => Texts.Get("stats.allTime");

        public static IReadOnlyList<InputStatsRow> Rows(InputStats session, InputStats allTime, string thousandsSeparator = " ")
        {
            var russian = Texts.Language == StringTable.DefaultLanguage;
            return new[]
            {
                new InputStatsRow(Texts.Get("stats.keys"), StatFormat.Count(session.Keys, thousandsSeparator), StatFormat.Count(allTime.Keys, thousandsSeparator)),
                new InputStatsRow(Texts.Get("stats.clicks"), StatFormat.Count(session.Clicks, thousandsSeparator), StatFormat.Count(allTime.Clicks, thousandsSeparator),
                    ClickHint(session, thousandsSeparator), ClickHint(allTime, thousandsSeparator)),
                new InputStatsRow(Texts.Get("stats.mouse"), StatFormat.Distance(session.MouseMeters, russian, thousandsSeparator), StatFormat.Distance(allTime.MouseMeters, russian, thousandsSeparator))
            };
        }

        private static string ClickHint(InputStats stats, string separator) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, Texts.Get("stats.clicksHint"),
                StatFormat.Count(stats.LeftClicks, separator), StatFormat.Count(stats.RightClicks, separator), StatFormat.Count(stats.MiddleClicks, separator));
    }
}
