using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class ClanTreeNode
    {
        public ClanTreeNode(string id, string nameRu, string nameEn, long cost, IReadOnlyList<string> requires, IReadOnlyList<Modifier> modifiers)
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
        public long Cost { get; }
        public IReadOnlyList<string> Requires { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class ClanTreeCatalog
    {
        public const string FileName = "clan_tree.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, ClanTreeNode> _byId;

        private ClanTreeCatalog(List<ClanTreeNode> all, long minDonation, int offlineHours)
        {
            All = all;
            MinDonation = minDonation;
            OfflineHours = offlineHours;
            _byId = new Dictionary<string, ClanTreeNode>(StringComparer.Ordinal);
            foreach (var node in all)
                _byId.Add(node.Id, node);
        }

        public IReadOnlyList<ClanTreeNode> All { get; }
        public long MinDonation { get; }
        public int OfflineHours { get; }

        public bool TryGet(string id, out ClanTreeNode node) => _byId.TryGetValue(id, out node!);

        public static ClanTreeCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var all = new List<ClanTreeNode>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("nodes").Items())
            {
                var id = item.Field("id").AsString();
                if (id.Length == 0 || !ids.Add(id))
                    throw item.Field("id").Error($"clan tree node '{id}' is empty or listed twice");
                var requires = new List<string>();
                if (item.HasField("requires"))
                {
                    foreach (var parent in item.Field("requires").Items())
                        requires.Add(parent.AsString());
                }
                var name = item.Field("name");
                all.Add(new ClanTreeNode(id, name.Field("ru").AsString(), name.Field("en").AsString(), item.Field("cost").AsLong(1, 1_000_000_000), requires,
                    ModifierReader.ReadList(item.Field("modifiers"))));
            }
            foreach (var node in all)
            {
                foreach (var parent in node.Requires)
                {
                    if (!ids.Contains(parent))
                        throw root.Field("nodes").Error($"clan tree node '{node.Id}' requires unknown node '{parent}'");
                }
            }
            if (!all.Exists(node => node.Requires.Count == 0))
                throw root.Field("nodes").Error("the clan tree needs at least one start node");
            return new ClanTreeCatalog(all, root.Field("minDonation").AsLong(1, 1_000_000), root.Field("offlineHours").AsInt(0, 24 * 30));
        }
    }
}
