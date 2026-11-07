using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class NestTrophy
    {
        public NestTrophy(string collection, string companion)
        {
            Collection = collection;
            Companion = companion;
        }

        public string Collection { get; }
        public string Companion { get; }
    }

    public sealed class NestRules
    {
        public string Building { get; private set; } = string.Empty;
        public double Chance { get; private set; }
        public double CooldownSeconds { get; private set; }
        public double HatchSeconds { get; private set; }
        public string Coal { get; private set; } = string.Empty;
        public double CoalSeconds { get; private set; }
        public double CoalSecondsPerDay { get; private set; }
        public double FriendWarmSeconds { get; private set; }
        public double SparkExperience { get; private set; }
        public int SparksForChoice { get; private set; }
        public IReadOnlyList<NestTrophy> Trophies { get; private set; } = Array.Empty<NestTrophy>();
        public string HeartCompanion { get; private set; } = string.Empty;
        public int ShopOffers { get; private set; }
        public long ShopPrice { get; private set; }
        public int ShopDays { get; private set; }
        public IReadOnlyList<string> ShopSources { get; private set; } = Array.Empty<string>();

        internal static NestRules Load(ContentNode node)
        {
            const double hour = 3600;
            var trophies = new List<NestTrophy>();
            foreach (var item in node.Field("trophies").Items())
                trophies.Add(new NestTrophy(item.Field("collection").AsString(), item.Field("companion").AsString()));
            var shop = node.Field("shop");
            var sources = new List<string>();
            foreach (var item in shop.Field("sources").Items())
                sources.Add(item.AsString());
            return new NestRules
            {
                Building = node.Field("building").AsString(),
                Chance = node.Field("chance").AsNumber(0, 1),
                CooldownSeconds = node.Field("cooldownDays").AsNumber(0, 365) * 86400,
                HatchSeconds = node.Field("hatchHours").AsNumber(0, 10_000) * hour,
                Coal = node.Field("coal").AsString(),
                CoalSeconds = node.Field("coalHours").AsNumber(0, 100) * hour,
                CoalSecondsPerDay = node.Field("coalHoursPerDay").AsNumber(0, 100) * hour,
                FriendWarmSeconds = node.Field("friendWarmHours").AsNumber(0, 100) * hour,
                SparkExperience = node.Field("sparkExperience").AsNumber(0, 1e9),
                SparksForChoice = node.Field("sparksForChoice").AsInt(1, 1000),
                Trophies = trophies,
                HeartCompanion = node.Field("heart").Field("companion").AsString(),
                ShopOffers = shop.Field("offers").AsInt(0, 20),
                ShopPrice = shop.Field("price").AsLong(0),
                ShopDays = shop.Field("days").AsInt(1, 365),
                ShopSources = sources
            };
        }
    }
}
