using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum CompanionStat
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
        TripSouls,
        OwnBiomeLoot,
        OwnTraits,
        None,
        Count
    }

    public sealed class CompanionLine
    {
        public CompanionLine(CompanionStat stat, double value, int untilHearts, string waits)
        {
            Stat = stat;
            Value = value;
            UntilHearts = untilHearts;
            Waits = waits;
        }

        public CompanionStat Stat { get; }
        public double Value { get; }
        public int UntilHearts { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0 && Stat != CompanionStat.None;
    }

    public static class CompanionLines
    {
        private static readonly Dictionary<string, CompanionStat> Stats = new Dictionary<string, CompanionStat>(StringComparer.Ordinal)
        {
            ["tripTime"] = CompanionStat.TripTime,
            ["tripLoot"] = CompanionStat.TripLoot,
            ["tripItems"] = CompanionStat.TripItems,
            ["tripRare"] = CompanionStat.TripRare,
            ["foodExperience"] = CompanionStat.FoodExperience,
            ["foodCap"] = CompanionStat.FoodCap,
            ["homeTraits"] = CompanionStat.HomeTraits,
            ["bond"] = CompanionStat.Bond,
            ["traitRanks"] = CompanionStat.TraitRanks,
            ["traitChangePrice"] = CompanionStat.TraitChangePrice,
            ["jointLoot"] = CompanionStat.JointLoot,
            ["soloLoot"] = CompanionStat.SoloLoot,
            ["longTripLoot"] = CompanionStat.LongTripLoot,
            ["shortTripTime"] = CompanionStat.ShortTripTime,
            ["tripSouls"] = CompanionStat.TripSouls,
            ["ownBiomeLoot"] = CompanionStat.OwnBiomeLoot,
            ["ownTraits"] = CompanionStat.OwnTraits,
            ["none"] = CompanionStat.None
        };

        internal static List<CompanionLine> Read(ContentNode node)
        {
            var lines = new List<CompanionLine>();
            foreach (var item in node.Items())
            {
                var name = item.Field("stat").AsString();
                if (!Stats.TryGetValue(name, out var stat))
                    throw item.Field("stat").Error($"unknown companion stat '{name}'");
                var until = item.HasField("untilHearts") ? item.Field("untilHearts").AsInt(1, 100) : 0;
                var waits = item.HasField("waits") ? item.Field("waits").AsString() : string.Empty;
                var value = item.HasField("value") ? item.Field("value").AsNumber(-1, 10) : 0;
                lines.Add(new CompanionLine(stat, value, until, waits));
            }
            return lines;
        }
    }
}
