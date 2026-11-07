using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class FishDef
    {
        public FishDef(string id, string nameRu, string nameEn, double weight, double minCm, double maxCm, double reelMeters)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Weight = weight;
            MinCm = minCm;
            MaxCm = maxCm;
            ReelMeters = reelMeters;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Weight { get; }
        public double MinCm { get; }
        public double MaxCm { get; }
        public double ReelMeters { get; }
    }

    public sealed class LegendFish
    {
        public LegendFish(string id, string nameRu, string nameEn, double cm, double stamina)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Cm = cm;
            Stamina = stamina;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Cm { get; }
        public double Stamina { get; }
    }

    public sealed class FishingSpot
    {
        public FishingSpot(string biome, IReadOnlyList<FishDef> fish, LegendFish legend)
        {
            Biome = biome;
            Fish = fish;
            Legend = legend;
        }

        public string Biome { get; }
        public IReadOnlyList<FishDef> Fish { get; }
        public LegendFish Legend { get; }
    }

    public sealed class FishingRules
    {
        public const string FileName = "fishing.json";
        public const int SupportedVersion = 1;

        public string Building { get; private set; } = string.Empty;
        public double BiteMinSeconds { get; private set; }
        public double BiteMaxSeconds { get; private set; }
        public double BiteWindowSeconds { get; private set; }
        public double FightSeconds { get; private set; }
        public double BottleChance { get; private set; }
        public double BottleReelMeters { get; private set; }
        public long BottleShards { get; private set; }
        public double BottleDust { get; private set; }
        public double LegendChance { get; private set; }
        public double RiftChance { get; private set; }
        public double TogetherLegendChance { get; private set; }
        public double TogetherBottleChance { get; private set; }
        public IReadOnlyList<FishingSpot> Spots { get; private set; } = Array.Empty<FishingSpot>();
        public IReadOnlyList<FishDef> RiftFish { get; private set; } = Array.Empty<FishDef>();

        public FishingSpot? Spot(string biome)
        {
            for (var i = 0; i < Spots.Count; i++)
            {
                if (Spots[i].Biome == biome)
                    return Spots[i];
            }
            return null;
        }

        public FishDef? Fish(string id)
        {
            for (var i = 0; i < Spots.Count; i++)
            {
                var fish = Spots[i].Fish;
                for (var j = 0; j < fish.Count; j++)
                {
                    if (fish[j].Id == id)
                        return fish[j];
                }
            }
            for (var i = 0; i < RiftFish.Count; i++)
            {
                if (RiftFish[i].Id == id)
                    return RiftFish[i];
            }
            return null;
        }

        public LegendFish? Legend(string id)
        {
            for (var i = 0; i < Spots.Count; i++)
            {
                if (Spots[i].Legend.Id == id)
                    return Spots[i].Legend;
            }
            return null;
        }

        public static FishingRules Load(string json, CampRules camp, IReadOnlyList<string> biomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var building = root.Field("building").AsString();
            if (camp.Building(building) == null)
                throw root.Field("building").Error($"unknown building '{building}'");
            var bite = root.Field("biteSeconds");
            var bottle = root.Field("bottle");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var spots = new List<FishingSpot>();
            foreach (var node in root.Field("spots").Items())
            {
                var biome = node.Field("biome").AsString();
                if (!Contains(biomes, biome))
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                var legendNode = node.Field("legend");
                var legendName = legendNode.Field("name");
                var legend = new LegendFish(Unique(ids, legendNode.Field("id")), legendName.Field("ru").AsString(), legendName.Field("en").AsString(),
                    legendNode.Field("cm").AsNumber(1, 10_000), legendNode.Field("stamina").AsNumber(1, 10_000));
                spots.Add(new FishingSpot(biome, ReadFish(node.Field("fish"), ids), legend));
            }
            return new FishingRules
            {
                Building = building,
                BiteMinSeconds = bite.Field("min").AsNumber(1, 3600),
                BiteMaxSeconds = bite.Field("max").AsNumber(bite.Field("min").AsNumber(1, 3600), 3600),
                BiteWindowSeconds = root.Field("biteWindowSeconds").AsNumber(0.5, 60),
                FightSeconds = root.Field("fightSeconds").AsNumber(1, 600),
                BottleChance = bottle.Field("chance").AsNumber(0, 1),
                BottleReelMeters = bottle.Field("reelMeters").AsNumber(0.1, 1000),
                BottleShards = bottle.Field("shards").AsLong(0, 1_000_000),
                BottleDust = bottle.Field("dust").AsNumber(0, 1_000_000),
                LegendChance = root.Field("legendChance").AsNumber(0, 1),
                RiftChance = root.Field("riftChance").AsNumber(0, 1),
                TogetherLegendChance = root.Field("together").Field("legendChance").AsNumber(0, 0.5),
                TogetherBottleChance = root.Field("together").Field("bottleChance").AsNumber(0, 0.5),
                Spots = spots,
                RiftFish = ReadFish(root.Field("riftFish"), ids)
            };
        }

        private static List<FishDef> ReadFish(ContentNode list, HashSet<string> ids)
        {
            var fish = new List<FishDef>();
            foreach (var node in list.Items())
            {
                var name = node.Field("name");
                var min = node.Field("minCm").AsNumber(1, 10_000);
                fish.Add(new FishDef(Unique(ids, node.Field("id")), name.Field("ru").AsString(), name.Field("en").AsString(), node.Field("weight").AsNumber(0.001, 1000),
                    min, node.Field("maxCm").AsNumber(min, 10_000), node.Field("reelMeters").AsNumber(0.1, 1000)));
            }
            if (fish.Count == 0)
                throw list.Error("needs at least one fish");
            return fish;
        }

        private static string Unique(HashSet<string> ids, ContentNode node)
        {
            var id = node.AsString();
            if (!ids.Add(id))
                throw node.Error($"duplicate fish '{id}'");
            return id;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }
}
