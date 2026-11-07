using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum TemperStat
    {
        TripTime,
        TripLoot,
        TripItems,
        TripRare,
        FoodExperience,
        FoodCap,
        HomeTraits,
        Bond,
        TraitRanks,
        TraitChangePrice,
        JointLoot,
        SoloLoot,
        LongTripLoot,
        ShortTripTime,
        Count
    }

    public sealed class TemperLine
    {
        public TemperLine(TemperStat stat, double value, int untilHearts)
        {
            Stat = stat;
            Value = value;
            UntilHearts = untilHearts;
        }

        public TemperStat Stat { get; }
        public double Value { get; }
        public int UntilHearts { get; }
    }

    public sealed class CompanionTemper
    {
        public CompanionTemper(string id, string nameRu, string nameEn, IReadOnlyList<TemperLine> plus, IReadOnlyList<TemperLine> minus)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Plus = plus;
            Minus = minus;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<TemperLine> Plus { get; }
        public IReadOnlyList<TemperLine> Minus { get; }
    }

    public sealed class CompanionTemperRules
    {
        public IReadOnlyList<CompanionTemper> Tempers { get; private set; } = Array.Empty<CompanionTemper>();
        public long ChangeSouls { get; private set; }
        public int FreeEveryDays { get; private set; } = 30;
        public int LongTripHours { get; private set; } = 8;

        public CompanionTemper? Temper(string id)
        {
            for (var i = 0; i < Tempers.Count; i++)
            {
                if (Tempers[i].Id == id)
                    return Tempers[i];
            }
            return null;
        }

        public CompanionTemper? Born(string companion, long salt)
        {
            if (Tempers.Count == 0)
                return null;
            var hash = 2166136261u ^ (uint)(salt ^ (salt >> 32)) * 2654435761u;
            for (var i = 0; i < companion.Length; i++)
                hash = (hash ^ companion[i]) * 16777619u;
            return Tempers[(int)(hash % (uint)Tempers.Count)];
        }

        private static readonly Dictionary<string, TemperStat> Stats = new Dictionary<string, TemperStat>(StringComparer.Ordinal)
        {
            ["tripTime"] = TemperStat.TripTime,
            ["tripLoot"] = TemperStat.TripLoot,
            ["tripItems"] = TemperStat.TripItems,
            ["tripRare"] = TemperStat.TripRare,
            ["foodExperience"] = TemperStat.FoodExperience,
            ["foodCap"] = TemperStat.FoodCap,
            ["homeTraits"] = TemperStat.HomeTraits,
            ["bond"] = TemperStat.Bond,
            ["traitRanks"] = TemperStat.TraitRanks,
            ["traitChangePrice"] = TemperStat.TraitChangePrice,
            ["jointLoot"] = TemperStat.JointLoot,
            ["soloLoot"] = TemperStat.SoloLoot,
            ["longTripLoot"] = TemperStat.LongTripLoot,
            ["shortTripTime"] = TemperStat.ShortTripTime
        };

        private static List<TemperLine> Lines(ContentNode node)
        {
            var lines = new List<TemperLine>();
            foreach (var item in node.Items())
            {
                var name = item.Field("stat").AsString();
                if (!Stats.TryGetValue(name, out var stat))
                    throw item.Field("stat").Error($"unknown temper stat '{name}'");
                var until = item.HasField("untilHearts") ? item.Field("untilHearts").AsInt(1, 100) : 0;
                lines.Add(new TemperLine(stat, item.Field("value").AsNumber(-1, 1), until));
            }
            return lines;
        }

        internal static CompanionTemperRules Load(ContentNode node)
        {
            var tempers = new List<CompanionTemper>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Field("list").Items())
            {
                var id = item.Field("id").AsString();
                if (!seen.Add(id))
                    throw item.Field("id").Error($"temper '{id}' is listed twice");
                var name = item.Field("name");
                tempers.Add(new CompanionTemper(id, name.Field("ru").AsString(), name.Field("en").AsString(), Lines(item.Field("plus")), Lines(item.Field("minus"))));
            }
            var change = node.Field("change");
            return new CompanionTemperRules
            {
                Tempers = tempers,
                ChangeSouls = change.Field("souls").AsLong(0, 1_000_000),
                FreeEveryDays = change.Field("freeEveryDays").AsInt(1, 365),
                LongTripHours = node.Field("longTripHours").AsInt(1, 100)
            };
        }
    }
}
