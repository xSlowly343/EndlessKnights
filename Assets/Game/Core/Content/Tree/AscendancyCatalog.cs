using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class AscendancyActivity
    {
        public AscendancyActivity(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class SubclassDef
    {
        private readonly Dictionary<string, TreeNodeDef> _byId;

        public SubclassDef(string id, string heroClass, string nameRu, string nameEn, TreeNodeDef start, IReadOnlyList<TreeNodeDef> nodes, string blurb = "")
        {
            Id = id;
            HeroClass = heroClass;
            NameRu = nameRu;
            NameEn = nameEn;
            Start = start;
            Nodes = nodes;
            Blurb = blurb;
            _byId = new Dictionary<string, TreeNodeDef>(StringComparer.Ordinal);
            foreach (var node in nodes)
                _byId.Add(node.Id, node);
        }

        public string Id { get; }
        public string HeroClass { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public TreeNodeDef Start { get; }
        public IReadOnlyList<TreeNodeDef> Nodes { get; }
        public string Blurb { get; }

        public bool TryGet(string id, out TreeNodeDef node) => _byId.TryGetValue(id, out node!);
    }

    public sealed class AscendancyCatalog
    {
        public const string FileName = "ascendancy.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, TreeNodeType> Types = new Dictionary<string, TreeNodeType>(StringComparer.Ordinal)
        {
            ["start"] = TreeNodeType.Start,
            ["small"] = TreeNodeType.Small,
            ["notable"] = TreeNodeType.Notable,
            ["keystone"] = TreeNodeType.Keystone
        };

        private readonly Dictionary<string, SubclassDef> _byId;

        private AscendancyCatalog(IReadOnlyList<AscendancyActivity> activities, IReadOnlyList<SubclassDef> subclasses, double freeChangeHours, int crystalChangeCost, int freeClassChanges)
        {
            FreeClassChanges = freeClassChanges;
            Activities = activities;
            Subclasses = subclasses;
            FreeChangeHours = freeChangeHours;
            CrystalChangeCost = crystalChangeCost;
            _byId = new Dictionary<string, SubclassDef>(StringComparer.Ordinal);
            foreach (var subclass in subclasses)
                _byId.Add(subclass.Id, subclass);
        }

        public IReadOnlyList<AscendancyActivity> Activities { get; }
        public IReadOnlyList<SubclassDef> Subclasses { get; }
        public double FreeChangeHours { get; }
        public int CrystalChangeCost { get; }
        public int FreeClassChanges { get; }
        public int MaxPoints => Activities.Count;

        public bool TryGet(string id, out SubclassDef subclass) => _byId.TryGetValue(id, out subclass!);

        public bool IsActivity(string id)
        {
            foreach (var activity in Activities)
            {
                if (string.Equals(activity.Id, id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public IReadOnlyList<SubclassDef> SubclassesOf(string heroClass)
        {
            var found = new List<SubclassDef>();
            foreach (var subclass in Subclasses)
            {
                if (string.Equals(subclass.HeroClass, heroClass, StringComparison.Ordinal))
                    found.Add(subclass);
            }
            return found;
        }

        public void RequireKnownClasses()
        {
            foreach (var subclass in Subclasses)
            {
                if (!Sim.HeroClassNames.ByName.ContainsKey(subclass.HeroClass))
                    throw new ContentException(FileName, subclass.Id, $"unknown class '{subclass.HeroClass}'");
            }
        }

        public void RequirePathSkills(Gems.GemCatalog gems, Weapons.WeaponCatalog weapons)
        {
            foreach (var gem in gems.All)
            {
                if (gem.Ascendancy == null)
                    continue;
                var where = "gems." + gem.Id;
                if (!TryGet(gem.Ascendancy, out var path))
                    throw new ContentException(Gems.GemCatalog.FileName, where, $"unknown path '{gem.Ascendancy}', expected a subclass id from {FileName}");
                if (!weapons.TryGetType(gem.Skill!.Weapon!, out var weapon) || Sim.HeroClassNames.Of(weapon.HeroClass) != path.HeroClass)
                    throw new ContentException(Gems.GemCatalog.FileName, where, $"path '{path.Id}' belongs to '{path.HeroClass}', the skill needs the weapon of that class");
            }
        }

        public static AscendancyCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var activities = ReadActivities(root.Field("activities"));
            var change = root.Field("change");
            var subclasses = new List<SubclassDef>();
            var subclassIds = new HashSet<string>(StringComparer.Ordinal);
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("subclasses").Items())
            {
                var subclass = ReadSubclass(item, nodeIds);
                if (!subclassIds.Add(subclass.Id))
                    throw item.Field("id").Error($"subclass id '{subclass.Id}' is used twice");
                subclasses.Add(subclass);
            }
            return new AscendancyCatalog(activities, subclasses,
                change.Field("freeEveryHours").AsNumber(0, 24 * 365),
                change.Field("crystalCost").AsInt(0, 1_000_000),
                change.Field("freeClassChanges").AsInt(0, 100));
        }

        private static List<AscendancyActivity> ReadActivities(ContentNode node)
        {
            var activities = new List<AscendancyActivity>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"activity id '{id}' is used twice");
                var name = item.Field("name");
                activities.Add(new AscendancyActivity(id, name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            if (activities.Count == 0)
                throw node.Error("at least one activity is required, each one gives a point");
            return activities;
        }

        private static SubclassDef ReadSubclass(ContentNode item, HashSet<string> nodeIds)
        {
            var id = item.Field("id").AsString();
            var nodes = new List<TreeNodeDef>();
            var byId = new Dictionary<string, TreeNodeDef>(StringComparer.Ordinal);
            TreeNodeDef? start = null;
            foreach (var nodeItem in item.Field("nodes").Items())
            {
                var node = ReadNode(nodeItem);
                if (!nodeIds.Add(node.Id))
                    throw nodeItem.Field("id").Error($"node id '{node.Id}' is used twice");
                if (node.Type == TreeNodeType.Start)
                {
                    if (start != null)
                        throw nodeItem.Field("type").Error($"subclass '{id}' has two start nodes");
                    start = node;
                }
                byId.Add(node.Id, node);
                nodes.Add(node);
            }
            if (start == null)
                throw item.Field("nodes").Error($"subclass '{id}' needs a start node");
            if (nodes.FindAll(node => node.Type == TreeNodeType.Keystone).Count > 1)
                throw item.Field("nodes").Error($"subclass '{id}' has more than one keystone");

            foreach (var link in item.Field("links").Items())
            {
                var ends = link.Items();
                if (ends.Count != 2)
                    throw link.Error("a link joins exactly two nodes");
                var a = Find(ends[0], byId);
                var b = Find(ends[1], byId);
                if (ReferenceEquals(a, b))
                    throw link.Error($"node '{a.Id}' is linked to itself");
                a.Connect(b);
                b.Connect(a);
            }
            RequireReachable(item.Field("links"), start, nodes);

            var name = item.Field("name");
            var blurb = item.HasField("blurb") ? item.Field("blurb").AsString() : string.Empty;
            return new SubclassDef(id, item.Field("class").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), start, nodes, blurb);
        }

        private static TreeNodeDef ReadNode(ContentNode node)
        {
            var typeNode = node.Field("type");
            if (!Types.TryGetValue(typeNode.AsString(), out var type))
                throw typeNode.Error($"unknown subclass node type '{typeNode.AsString()}', expected start, small, notable or keystone");
            var modifiers = node.HasField("modifiers") ? ModifierReader.ReadList(node.Field("modifiers")) : Array.Empty<Modifier>();
            var locked = node.HasField("locked") && node.Field("locked").AsBool();
            var name = node.Field("name");
            return new TreeNodeDef(
                node.Field("id").AsString(), type,
                node.Field("x").AsNumber(-1000, 1000), node.Field("y").AsNumber(-1000, 1000),
                name.Field("ru").AsString(), name.Field("en").AsString(),
                modifiers, locked, descriptionRu: DescriptionText(node, "ru"), descriptionEn: DescriptionText(node, "en"));
        }

        private static string DescriptionText(ContentNode node, string language) =>
            node.HasField("description") && node.Field("description").HasField(language) ? node.Field("description").Field(language).AsString() : string.Empty;

        private static TreeNodeDef Find(ContentNode id, Dictionary<string, TreeNodeDef> byId)
        {
            if (!byId.TryGetValue(id.AsString(), out var node))
                throw id.Error($"unknown node '{id.AsString()}'");
            return node;
        }

        private static void RequireReachable(ContentNode links, TreeNodeDef start, List<TreeNodeDef> nodes)
        {
            var reached = new HashSet<TreeNodeDef> { start };
            var queue = new Queue<TreeNodeDef>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                foreach (var next in queue.Dequeue().Neighbours)
                {
                    if (reached.Add(next))
                        queue.Enqueue(next);
                }
            }
            foreach (var node in nodes)
            {
                if (!reached.Contains(node))
                    throw links.Error($"node '{node.Id}' cannot be reached from the start '{start.Id}'");
            }
        }
    }
}
