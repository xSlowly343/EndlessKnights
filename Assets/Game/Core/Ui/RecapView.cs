using System.Collections.Generic;
using System.Globalization;
using Game.Core.Clock;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct RecapTile
    {
        public RecapTile(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }

    public sealed class RecapCard
    {
        public const int WidthPx = 1200;
        public const int HeightPx = 630;

        public RecapCard(string title, string period, string shortPeriod, IReadOnlyList<RecapTile> tiles, string footer)
        {
            Title = title;
            Period = period;
            ShortPeriod = shortPeriod;
            Tiles = tiles;
            Footer = footer;
        }

        public string Title { get; }
        public string Period { get; }
        public string ShortPeriod { get; }
        public IReadOnlyList<RecapTile> Tiles { get; }
        public string Footer { get; }
    }

    public static class RecapView
    {
        public const int TileCount = 6;

        public static string DayButton => Texts.Get("recap.button.day");
        public static string WeekButton => Texts.Get("recap.button.week");
        public static string Share => Texts.Get("recap.share");
        public static string Shared => Texts.Get("recap.shared");
        public static string ShareFailed => Texts.Get("recap.shareFailed");
        public static string Ready(RecapKind kind) => Texts.Get(kind == RecapKind.Week ? "recap.ready.week" : "recap.ready.day");
        public static string Nothing => Texts.Get("recap.nothing");

        public static string Hero(GameLoop loop)
        {
            var tree = loop.ClassTreeDef;
            var name = tree == null ? Texts.Get("recap.novice") : Texts.Pick(tree.NameRu, tree.NameEn);
            return Texts.Format("recap.hero", name, loop.State.HeroLevel);
        }

        public static RecapCard? Card(RecapSystem recap, RecapKind kind, string heroName, string thousandsSeparator = " ")
        {
            if (kind == RecapKind.None)
                return null;
            var russian = Texts.Language == StringTable.DefaultLanguage;
            RecapDay total;
            string title;
            string period;
            string shortPeriod;
            string days;
            if (kind == RecapKind.Week)
            {
                var week = WeekShown(recap);
                total = recap.Week(week, out var played);
                title = Texts.Get("recap.title.week");
                var monday = week * RecapSystem.DaysInWeek - 3;
                period = Date(monday, russian) + " – " + Date(monday + RecapSystem.DaysInWeek - 1, russian);
                shortPeriod = ShortDate(monday) + "–" + ShortDate(monday + RecapSystem.DaysInWeek - 1);
                days = Texts.Format("recap.daysPlayed", played, RecapSystem.DaysInWeek);
            }
            else
            {
                total = DayShown(recap);
                title = Texts.Get("recap.title.day");
                period = Date(total.Day, russian);
                shortPeriod = period;
                days = string.Empty;
            }
            if (total.Empty)
                return null;
            var tiles = new[]
            {
                new RecapTile(Texts.Get("stats.playTime"), StatFormat.Duration(total.PlaySeconds, russian)),
                new RecapTile(Texts.Get("stats.keys"), StatFormat.Count((ulong)total.Keys, thousandsSeparator)),
                new RecapTile(Texts.Get("stats.clicks"), StatFormat.Count((ulong)total.Clicks, thousandsSeparator)),
                new RecapTile(Texts.Get("stats.mouse"), StatFormat.Distance(total.MouseMeters, russian, thousandsSeparator)),
                new RecapTile(Texts.Get("recap.kills"), Texts.Format("recap.killsValue", StatFormat.Count((ulong)System.Math.Max(0, total.Kills), thousandsSeparator), StatFormat.Count((ulong)System.Math.Max(0, total.Bosses), thousandsSeparator))),
                new RecapTile(Texts.Get("recap.stages"), Texts.Format("recap.stagesValue", StatFormat.Count((ulong)System.Math.Max(0, total.Stages), thousandsSeparator), StatFormat.Count((ulong)System.Math.Max(0, total.RecordStage), thousandsSeparator)))
            };
            var footer = heroName.Length > 0 ? Texts.Format("recap.footer", heroName) : Texts.Get("recap.footerPlain");
            if (days.Length > 0)
                footer = days + " · " + footer;
            if (total.Levels > 0 || total.Prestiges > 0)
                footer = Texts.Format("recap.growth", total.Levels, total.Prestiges) + " · " + footer;
            return new RecapCard(title, period, shortPeriod, tiles, footer);
        }

        public static IReadOnlyList<string> Lines(RecapCard card)
        {
            var lines = new List<string>(card.Tiles.Count + 1);
            foreach (var tile in card.Tiles)
                lines.Add(tile.Label + ": " + tile.Value);
            lines.Add(card.Footer);
            return lines;
        }

        public static string Title(RecapCard card) => card.Title + " · " + card.ShortPeriod;

        private static long WeekShown(RecapSystem recap)
        {
            var last = recap.LastDay;
            var current = Content.ChronicleCatalog.WeekOf(recap.State.Day);
            if (last == null)
                return current;
            var lastWeek = Content.ChronicleCatalog.WeekOf(last.Day);
            return lastWeek < current ? lastWeek : current;
        }

        private static RecapDay DayShown(RecapSystem recap) => recap.LastDay ?? recap.Today;

        private static string ShortDate(long day) =>
            new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).AddDays(day).ToString("dd.MM", CultureInfo.InvariantCulture);

        private static string Date(long day, bool russian) =>
            new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).AddDays(day).ToString(russian ? "dd.MM.yyyy" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
