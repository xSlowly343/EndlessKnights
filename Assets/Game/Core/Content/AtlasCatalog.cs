using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class AtlasNode
    {
        public AtlasNode(string id, string nameRu, string nameEn, double cost, IReadOnlyList<string> requires, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Cost = cost;
            Requires = requires;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Cost { get; }
        public IReadOnlyList<string> Requires { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class AtlasCatalog
    {
        public const string FileName = "atlas.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, AtlasNode> _byId;

        private AtlasCatalog(List<AtlasNode> all, double chestGold)
        {
            All = all;
            ChestGold = chestGold;
            _byId = new Dictionary<string, AtlasNode>(StringComparer.Ordinal);
            foreach (var node in all)
                _byId.Add(node.Id, node);
        }

        public IReadOnlyList<AtlasNode> All { get; }
        public double ChestGold { get; }

        public bool TryGet(string id, out AtlasNode node) => _byId.TryGetValue(id, out node!);

        public static AtlasCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var all = new List<AtlasNode>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("nodes").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"atlas node '{id}' is listed twice");
                var requires = new List<string>();
                if (item.HasField("requires"))
                {
                    foreach (var parent in item.Field("requires").Items())
                        requires.Add(parent.AsString());
                }
                var name = item.Field("name");
                all.Add(new AtlasNode(id, name.Field("ru").AsString(), name.Field("en").AsString(), item.Field("cost").AsNumber(0, 1e12), requires,
                    ModifierReader.ReadList(item.Field("modifiers"))));
            }
            foreach (var node in all)
            {
                foreach (var parent in node.Requires)
                {
                    if (!ids.Contains(parent))
                        throw root.Field("nodes").Error($"atlas node '{node.Id}' requires unknown node '{parent}'");
                }
            }
            if (!all.Exists(node => node.Requires.Count == 0))
                throw root.Field("nodes").Error("the atlas needs at least one start node");
            return new AtlasCatalog(all, root.Field("chestGold").AsNumber(0, 1e6));
        }
    }
}
