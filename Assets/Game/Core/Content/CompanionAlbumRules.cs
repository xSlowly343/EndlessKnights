using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum AlbumBonusKind
    {
        TripSlot,
        FoodExperience,
        NestSlot,
        Title
    }

    public sealed class AlbumBonus
    {
        public AlbumBonus(int stars, AlbumBonusKind kind, double value)
        {
            Stars = stars;
            Kind = kind;
            Value = value;
        }

        public int Stars { get; }
        public AlbumBonusKind Kind { get; }
        public double Value { get; }
    }

    public sealed class CompanionAlbumRules
    {
        public int BaseTrips { get; private set; } = int.MaxValue;
        public int HeartsStar { get; private set; }
        public IReadOnlyList<string> PaidSources { get; private set; } = Array.Empty<string>();
        public string Title { get; private set; } = string.Empty;
        public IReadOnlyList<AlbumBonus> Bonuses { get; private set; } = Array.Empty<AlbumBonus>();

        private static readonly Dictionary<string, AlbumBonusKind> Kinds = new Dictionary<string, AlbumBonusKind>(StringComparer.Ordinal)
        {
            ["tripSlot"] = AlbumBonusKind.TripSlot,
            ["foodExperience"] = AlbumBonusKind.FoodExperience,
            ["nestSlot"] = AlbumBonusKind.NestSlot,
            ["title"] = AlbumBonusKind.Title
        };

        internal static CompanionAlbumRules Load(ContentNode node, int maxHearts)
        {
            var paid = new List<string>();
            foreach (var item in node.Field("paidSources").Items())
                paid.Add(item.AsString());
            var bonuses = new List<AlbumBonus>();
            var last = 0;
            foreach (var item in node.Field("bonuses").Items())
            {
                var stars = item.Field("stars").AsInt(1, 100_000);
                if (stars <= last)
                    throw item.Field("stars").Error("album bonuses must go up in stars");
                last = stars;
                var kind = item.Field("kind").AsString();
                if (!Kinds.TryGetValue(kind, out var parsed))
                    throw item.Field("kind").Error($"unknown album bonus '{kind}'");
                bonuses.Add(new AlbumBonus(stars, parsed, item.Field("value").AsNumber(0, 100)));
            }
            var hearts = node.Field("heartsStar").AsInt(1, 1000);
            if (hearts > maxHearts)
                throw node.Field("heartsStar").Error($"a companion has only {maxHearts} hearts");
            return new CompanionAlbumRules
            {
                BaseTrips = node.Field("baseTrips").AsInt(1, 100),
                HeartsStar = hearts,
                PaidSources = paid,
                Title = node.Field("title").AsString(),
                Bonuses = bonuses
            };
        }
    }

    public sealed class CompanionStripRules
    {
        public double SitAfterSeconds { get; private set; } = 60;
        public double SleepAfterSeconds { get; private set; } = 300;
        public double HopAfterSeconds { get; private set; } = 5;
        public double RunAfterSeconds { get; private set; } = 30;
        public double SeriesPauseSeconds { get; private set; } = 2;
        public int CheerHearts { get; private set; } = 3;
        public double CheerSeconds { get; private set; } = 3;
        public double FlashSeconds { get; private set; } = 2;

        internal static CompanionStripRules Load(ContentNode node)
        {
            var sit = node.Field("sitAfterSeconds").AsNumber(1, 1e6);
            var sleep = node.Field("sleepAfterSeconds").AsNumber(sit, 1e6);
            var hop = node.Field("hopAfterSeconds").AsNumber(0.1, 1e6);
            return new CompanionStripRules
            {
                SitAfterSeconds = sit,
                SleepAfterSeconds = sleep,
                HopAfterSeconds = hop,
                RunAfterSeconds = node.Field("runAfterSeconds").AsNumber(hop, 1e6),
                SeriesPauseSeconds = node.Field("seriesPauseSeconds").AsNumber(0.1, 60),
                CheerHearts = node.Field("cheerHearts").AsInt(0, 100),
                CheerSeconds = node.Field("cheerSeconds").AsNumber(0, 60),
                FlashSeconds = node.Field("flashSeconds").AsNumber(0, 60)
            };
        }
    }
}
