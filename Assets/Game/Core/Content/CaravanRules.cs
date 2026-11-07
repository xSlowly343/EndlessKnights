using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CaravanStop
    {
        public CaravanStop(string id, string biome, double meters, string nameRu, string nameEn, IReadOnlyList<ModuleMaterial> goods)
        {
            Id = id;
            Biome = biome;
            Meters = meters;
            NameRu = nameRu;
            NameEn = nameEn;
            Goods = goods;
        }

        public string Id { get; }
        public string Biome { get; }
        public double Meters { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<ModuleMaterial> Goods { get; }
    }

    public sealed class CaravanRules
    {
        public const string FileName = "caravan.json";
        public const int SupportedVersion = 1;

        public IReadOnlyList<CaravanStop> Stops { get; private set; } = Array.Empty<CaravanStop>();
        public double LapMeters => Stops.Count == 0 ? 0 : Stops[Stops.Count - 1].Meters;

        public static CaravanRules Load(string json, IReadOnlyList<string> biomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var stops = new List<CaravanStop>();
            foreach (var node in root.Field("stops").Items())
            {
                var id = node.Field("id").AsString();
                var biome = node.Field("biome").AsString();
                var known = false;
                foreach (var item in biomes)
                    known |= item == biome;
                if (!known)
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                var meters = node.Field("meters").AsNumber(1, 10_000_000);
                if (stops.Count > 0 && meters <= stops[stops.Count - 1].Meters)
                    throw node.Field("meters").Error($"stop '{id}' must lie farther along the road than the one before");
                var goods = new List<ModuleMaterial>();
                foreach (var item in node.Field("goods").Items())
                    goods.Add(new ModuleMaterial(item.Field("item").AsString(), item.Field("count").AsInt(1, 10_000)));
                if (goods.Count == 0)
                    throw node.Field("goods").Error($"stop '{id}' has no goods");
                var name = node.Field("name");
                stops.Add(new CaravanStop(id, biome, meters, name.Field("ru").AsString(), name.Field("en").AsString(), goods));
            }
            if (stops.Count == 0)
                throw root.Field("stops").Error("the caravan needs at least one stop");
            return new CaravanRules { Stops = stops };
        }
    }
}
