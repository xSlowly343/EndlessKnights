using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum TreatStat
    {
        None,
        Items,
        Shards,
        Rare,
        Duration
    }

    public sealed class CompanionTreat
    {
        public CompanionTreat(string id, string nameRu, string nameEn, IReadOnlyList<string> ingredients, double experience, TreatStat stat, double value, bool delicacy)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Ingredients = ingredients;
            Experience = experience;
            Stat = stat;
            Value = value;
            Delicacy = delicacy;
        }

        public string Id { get; }
        public string Item => CompanionTreatRules.Prefix + Id;
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<string> Ingredients { get; }
        public double Experience { get; }
        public TreatStat Stat { get; }
        public double Value { get; }
        public bool Delicacy { get; }
    }

    public sealed class CompanionTreatRules
    {
        public const string Prefix = "treat_";

        public IReadOnlyList<CompanionTreat> Treats { get; private set; } = Array.Empty<CompanionTreat>();
        public int DelicacyMaxLevel { get; private set; }
        public int DelicacyDays { get; private set; } = 7;

        public CompanionTreat? Treat(string idOrItem)
        {
            var id = idOrItem.StartsWith(Prefix, StringComparison.Ordinal) ? idOrItem.Substring(Prefix.Length) : idOrItem;
            for (var i = 0; i < Treats.Count; i++)
            {
                if (Treats[i].Id == id)
                    return Treats[i];
            }
            return null;
        }

        private static readonly Dictionary<string, TreatStat> Stats = new Dictionary<string, TreatStat>(StringComparer.Ordinal)
        {
            ["none"] = TreatStat.None,
            ["items"] = TreatStat.Items,
            ["shards"] = TreatStat.Shards,
            ["rare"] = TreatStat.Rare,
            ["duration"] = TreatStat.Duration
        };

        internal static CompanionTreatRules Load(ContentNode node)
        {
            var treats = new List<CompanionTreat>();
            foreach (var item in node.Field("recipes").Items())
            {
                var ingredients = new List<string>();
                foreach (var ingredient in item.Field("ingredients").Items())
                    ingredients.Add(ingredient.AsString());
                if (ingredients.Count == 0)
                    throw item.Field("ingredients").Error("a treat needs ingredients");
                var delicacy = item.HasField("delicacy") && item.Field("delicacy").AsBool();
                var stat = TreatStat.None;
                if (item.HasField("trip"))
                {
                    var name = item.Field("trip").Field("stat").AsString();
                    if (!Stats.TryGetValue(name, out stat))
                        throw item.Field("trip").Field("stat").Error($"unknown treat stat '{name}'");
                }
                var name2 = item.Field("name");
                treats.Add(new CompanionTreat(item.Field("id").AsString(), name2.Field("ru").AsString(), name2.Field("en").AsString(), ingredients,
                    item.HasField("experience") ? item.Field("experience").AsNumber(0, 1e6) : 0, stat,
                    item.HasField("trip") ? item.Field("trip").Field("value").AsNumber(0, 1) : 0, delicacy));
            }
            return new CompanionTreatRules
            {
                Treats = treats,
                DelicacyMaxLevel = node.Field("delicacyMaxLevel").AsInt(1, 1000),
                DelicacyDays = node.Field("delicacyDays").AsInt(1, 100)
            };
        }
    }
}
