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

        public const int RowCount = 5;

        public static IReadOnlyList<InputStatsRow> Rows(InputStats session, InputStats allTime, long startedUnixMs, string thousandsSeparator = " ")
        {
            var russian = Texts.Language == StringTable.DefaultLanguage;
            return new[]
            {
                new InputStatsRow(Texts.Get("stats.playTime"), StatFormat.Duration(session.PlaySeconds, russian), StatFormat.Duration(allTime.PlaySeconds, russian)),
                new InputStatsRow(Texts.Get("stats.started"), string.Empty, StatFormat.Date(startedUnixMs, russian)),
                new InputStatsRow(Texts.Get("stats.keys"), StatFormat.Count(session.Keys, thousandsSeparator), StatFormat.Count(allTime.Keys, thousandsSeparator)),
                new InputStatsRow(Texts.Get("stats.clicks"), StatFormat.Count(session.Clicks, thousandsSeparator), StatFormat.Count(allTime.Clicks, thousandsSeparator)),
                new InputStatsRow(Texts.Get("stats.mouse"), StatFormat.Distance(session.MouseMeters, russian, thousandsSeparator), StatFormat.Distance(allTime.MouseMeters, russian, thousandsSeparator))
            };
        }
    }
}
