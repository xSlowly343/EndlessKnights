using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class ChestDef
    {
        public ChestDef(string id, string nameRu, string nameEn, long price, long ingots, long essence, int perWeek)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Price = price;
            Ingots = ingots;
            Essence = essence;
            PerWeek = perWeek;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long Price { get; }
        public long Ingots { get; }
        public long Essence { get; }
        public int PerWeek { get; }
    }

    public sealed class ChestCatalog
    {
        public const string FileName = "chests.json";
        public const int SupportedVersion = 1;
        public const string Currency = "crystals";

        private readonly Dictionary<string, ChestDef> _byId;

        private ChestCatalog(IReadOnlyList<ChestDef> chests)
        {
            Chests = chests;
            _byId = new Dictionary<string, ChestDef>(StringComparer.Ordinal);
            foreach (var chest in chests)
                _byId[chest.Id] = chest;
        }

        public IReadOnlyList<ChestDef> Chests { get; }

        public bool TryGet(string id, out ChestDef chest) => _byId.TryGetValue(id, out chest!);

        public static ChestCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var currency = root.Field("currency").AsString();
            if (currency != Currency)
                throw root.Field("currency").Error($"chests are sold for '{Currency}' only, not '{currency}'");
            var chests = new List<ChestDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("chests").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"chest '{id}' is listed twice");
                var ingots = node.HasField("ingots") ? node.Field("ingots").AsLong(1, 1_000_000) : 0;
                var essence = node.HasField("essence") ? node.Field("essence").AsLong(1, 1_000_000) : 0;
                if (ingots == 0 && essence == 0)
                    throw node.Error($"chest '{id}' holds nothing: give it ingots or essence");
                var name = node.Field("name");
                chests.Add(new ChestDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), node.Field("price").AsLong(1, 1_000_000),
                    ingots, essence, node.Field("perWeek").AsInt(1, 100)));
            }
            if (chests.Count == 0)
                throw root.Field("chests").Error("the chest shop needs at least one chest");
            return new ChestCatalog(chests);
        }
    }
}
