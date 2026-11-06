using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class GardenBedLine
    {
        public int Index { get; internal set; }
        public string Plant { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public double Progress { get; internal set; }
        public bool Ready { get; internal set; }
    }

    public sealed class GardenSeedLine
    {
        public string Plant { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool Starter { get; internal set; }
        public long Seeds { get; internal set; }
    }

    public static class GardenView
    {
        public static List<GardenBedLine> Beds(GardenSystem garden, bool russian = true)
        {
            var lines = new List<GardenBedLine>();
            var beds = garden.State.Beds;
            for (var i = 0; i < garden.OpenBeds; i++)
            {
                var plant = garden.Rules.Plant(beds[i].Plant);
                lines.Add(new GardenBedLine
                {
                    Index = i,
                    Plant = beds[i].Plant,
                    Name = plant == null ? string.Empty : russian ? plant.NameRu : plant.NameEn,
                    Progress = plant == null ? 0 : System.Math.Min(1, beds[i].Grown / plant.GrowSeconds),
                    Ready = beds[i].Ready
                });
            }
            return lines;
        }

        public static List<GardenSeedLine> Seeds(GardenSystem garden, CampSystem camp, bool russian = true)
        {
            var lines = new List<GardenSeedLine>();
            foreach (var plant in garden.Rules.Plants)
            {
                var seeds = camp.Count(plant.Seed);
                if (!plant.Starter && seeds == 0)
                    continue;
                lines.Add(new GardenSeedLine { Plant = plant.Id, Name = russian ? plant.NameRu : plant.NameEn, Starter = plant.Starter, Seeds = seeds });
            }
            return lines;
        }
    }
}
