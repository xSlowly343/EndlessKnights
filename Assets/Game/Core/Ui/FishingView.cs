using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class FishingSpotLine
    {
        public string Biome { get; internal set; } = string.Empty;
        public bool Open { get; internal set; }
        public bool Current { get; internal set; }
        public string Legend { get; internal set; } = string.Empty;
        public double LegendTired { get; internal set; }
        public bool LegendCaught { get; internal set; }
    }

    public sealed class FishAlbumLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Biome { get; internal set; } = string.Empty;
        public bool Legend { get; internal set; }
        public long Count { get; internal set; }
        public double BestCm { get; internal set; }
    }

    public static class FishingView
    {
        public static List<FishingSpotLine> Spots(FishingSystem fishing, bool russian = true)
        {
            var lines = new List<FishingSpotLine>();
            foreach (var spot in fishing.Rules.Spots)
            {
                var left = fishing.LegendLeft(spot.Legend);
                lines.Add(new FishingSpotLine
                {
                    Biome = spot.Biome,
                    Open = fishing.SpotOpen(spot.Biome),
                    Current = spot.Biome == fishing.CurrentSpot,
                    Legend = russian ? spot.Legend.NameRu : spot.Legend.NameEn,
                    LegendTired = 1 - left / spot.Legend.Stamina,
                    LegendCaught = fishing.Record(spot.Legend.Id) != null
                });
            }
            return lines;
        }

        public static List<FishAlbumLine> Album(FishingSystem fishing, bool russian = true)
        {
            var lines = new List<FishAlbumLine>();
            foreach (var spot in fishing.Rules.Spots)
            {
                foreach (var fish in spot.Fish)
                    lines.Add(Line(fishing, fish.Id, russian ? fish.NameRu : fish.NameEn, spot.Biome, false));
                lines.Add(Line(fishing, spot.Legend.Id, russian ? spot.Legend.NameRu : spot.Legend.NameEn, spot.Biome, true));
            }
            foreach (var fish in fishing.Rules.RiftFish)
                lines.Add(Line(fishing, fish.Id, russian ? fish.NameRu : fish.NameEn, string.Empty, false));
            return lines;
        }

        public static double FightProgress(FishingSystem fishing)
        {
            var need = fishing.ReelNeeded;
            return need <= 0 ? 0 : System.Math.Min(1, fishing.State.Reeled / need);
        }

        private static FishAlbumLine Line(FishingSystem fishing, string id, string name, string biome, bool legend)
        {
            var record = fishing.Record(id);
            return new FishAlbumLine { Id = id, Name = record == null ? string.Empty : name, Biome = biome, Legend = legend, Count = record?.Count ?? 0, BestCm = record?.BestCm ?? 0 };
        }
    }
}
