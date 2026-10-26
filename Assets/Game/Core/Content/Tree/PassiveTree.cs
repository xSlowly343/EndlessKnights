using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class PassiveTree
    {
        public const string FileName = "tree.json";
        public const int SupportedVersion = 2;

        private static readonly Dictionary<string, TreeNodeType> Types = new Dictionary<string, TreeNodeType>(StringComparer.Ordinal)
        {
            ["small"] = TreeNodeType.Small,
            ["notable"] = TreeNodeType.Notable,
            ["keystone"] = TreeNodeType.Keystone,
            ["mastery"] = TreeNodeType.Mastery,
            ["jewel"] = TreeNodeType.Jewel,
            ["attribute"] = TreeNodeType.Attribute,
            ["start"] = TreeNodeType.Start
        };

        private static readonly HashSet<string> Roles = new HashSet<string>(StringComparer.Ordinal) { "dd", "buffer", "debuffer", "dot", "summoner" };
        private static readonly string[] SharedEffectFields = { "sharedModifiers", "sharedOptions" };

        private readonly Dictionary<string, TreeNodeDef> _byId;
        private readonly Dictionary<string, TreeNodeDef> _startByClass;

        private PassiveTree(List<TreeNodeDef> nodes, IReadOnlyList<string> attributes, int linkCount, RespecRules respec)
        {
            Nodes = nodes;
            Attributes = attributes;
            LinkCount = linkCount;
            Respec = respec;
            _byId = new Dictionary<string, TreeNodeDef>(StringComparer.Ordinal);
            _startByClass = new Dictionary<string, TreeNodeDef>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                _byId.Add(node.Id, node);
                if (node.StartClass != null)
                    _startByClass.Add(node.StartClass, node);
            }
        }

        public IReadOnlyList<TreeNodeDef> Nodes { get; }
        public IReadOnlyList<string> Attributes { get; }
        public int LinkCount { get; }
        public RespecRules Respec { get; }

        public TreeNodeDef this[string id] => _byId[id];

        public bool TryGet(string id, out TreeNodeDef node) => _byId.TryGetValue(id, out node!);

        public TreeNodeDef? StartOf(string className) => _startByClass.TryGetValue(className, out var start) ? start : null;

        public static bool IsStartExit(TreeNodeDef node)
        {
            foreach (var neighbour in node.Neighbours)
            {
                if (neighbour.Type == TreeNodeType.Start)
                    return true;
            }
            return false;
        }

        public int DepthFromNearestStart(TreeNodeDef target)
        {
            var depth = new Dictionary<TreeNodeDef, int>();
            var queue = new Queue<TreeNodeDef>();
            foreach (var node in Nodes)
            {
                if (node.Type != TreeNodeType.Start)
                    continue;
                depth[node] = 0;
                queue.Enqueue(node);
            }
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (ReferenceEquals(node, target))
                    return depth[node];
                foreach (var next in node.Neighbours)
                {
                    if (depth.ContainsKey(next) || next.Type == TreeNodeType.Start)
                        continue;
                    depth[next] = depth[node] + 1;
                    queue.Enqueue(next);
                }
            }
            return -1;
        }

        public int DepthFrom(TreeNodeDef start, TreeNodeDef target)
        {
            var depth = new Dictionary<TreeNodeDef, int> { [start] = 0 };
            var queue = new Queue<TreeNodeDef>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (ReferenceEquals(node, target))
                    return depth[node];
                foreach (var next in node.Neighbours)
                {
                    if (depth.ContainsKey(next) || next.Type == TreeNodeType.Start)
                        continue;
                    depth[next] = depth[node] + 1;
                    queue.Enqueue(next);
                }
            }
            return -1;
        }

        public static StatId AttributeStat(string attribute) => new StatId(attribute);

        public static PassiveTree Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var attributes = new List<string>();
            foreach (var attribute in root.Field("attributes").Items())
                attributes.Add(attribute.AsString());
            if (attributes.Count == 0)
                throw root.Field("attributes").Error("at least one attribute is required");

            var nodes = new List<TreeNodeDef>();
            var byId = new Dictionary<string, TreeNodeDef>(StringComparer.Ordinal);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("nodes").Items())
            {
                var node = ReadNode(item, attributes);
                if (byId.ContainsKey(node.Id))
                    throw item.Field("id").Error($"node id '{node.Id}' is used twice");
                if (node.StartClass != null && !classes.Add(node.StartClass))
                    throw item.Field("class").Error($"class '{node.StartClass}' has two start nodes");
                byId.Add(node.Id, node);
                nodes.Add(node);
            }

            RequireStartForEveryZone(root.Field("nodes"), nodes, classes);
            var links = ReadLinks(root.Field("links"), byId);
            RequireConnected(root.Field("links"), nodes);
            return new PassiveTree(nodes, attributes, links, RespecRules.Read(root.Field("respec")));
        }

        private static TreeNodeDef ReadNode(ContentNode node, List<string> attributes)
        {
            var typeNode = node.Field("type");
            if (!Types.TryGetValue(typeNode.AsString(), out var type))
                throw typeNode.Error($"unknown node type '{typeNode.AsString()}'");

            var modifiers = node.HasField("modifiers") ? ModifierReader.ReadList(node.Field("modifiers")) : Array.Empty<Modifier>();
            var options = new List<IReadOnlyList<Modifier>>();
            if (type == TreeNodeType.Mastery)
            {
                foreach (var option in node.Field("options").Items())
                    options.Add(ModifierReader.ReadList(option));
                if (options.Count < 2)
                    throw node.Field("options").Error("a mastery needs at least two options");
            }

            var amount = type == TreeNodeType.Attribute ? node.Field("amount").AsNumber(0.01, 1000) : 0;
            var startClass = type == TreeNodeType.Start ? node.Field("class").AsString() : null;
            var locked = node.HasField("locked") && node.Field("locked").AsBool();
            var scope = ReadScope(node.Field("scope"), attributes);
            if (startClass != null && scope != TreeNodeDef.ClassScopePrefix + startClass)
                throw node.Field("scope").Error($"a start belongs to its own class, expected '{TreeNodeDef.ClassScopePrefix}{startClass}'");
            RejectSharedEffects(node);
            var name = node.Field("name");
            return new TreeNodeDef(
                node.Field("id").AsString(), type,
                node.Field("x").AsNumber(-1000, 1000), node.Field("y").AsNumber(-1000, 1000),
                name.Field("ru").AsString(), name.Field("en").AsString(),
                modifiers, options, amount, startClass, locked, scope);
        }

        private static void RequireStartForEveryZone(ContentNode list, List<TreeNodeDef> nodes, HashSet<string> classes)
        {
            foreach (var node in nodes)
            {
                if (node.ScopeClass != null && !classes.Contains(node.ScopeClass))
                    throw list.Error($"node '{node.Id}' belongs to class '{node.ScopeClass}', which has no start on the tree");
            }
        }

        private static string ReadScope(ContentNode scope, List<string> attributes)
        {
            var text = scope.AsString();
            if (text == TreeNodeDef.CommonScope)
                return text;
            var colon = text.IndexOf(':');
            var kind = colon < 0 ? text : text.Substring(0, colon);
            var value = colon < 0 ? string.Empty : text.Substring(colon + 1);
            var valid = kind switch
            {
                "class" => value.Length > 0,
                "role" => Roles.Contains(value),
                "sector" => attributes.Contains(value),
                _ => false
            };
            if (!valid)
                throw scope.Error($"unknown scope '{text}', expected common, class:<class>, role:<role> or sector:<attribute>");
            return text;
        }

        private static void RejectSharedEffects(ContentNode node)
        {
            foreach (var field in SharedEffectFields)
            {
                if (node.HasField(field))
                    throw node.Field(field).Error("a node gives the same effect to every class, put it into 'modifiers' or 'options'");
            }
        }

        private static int ReadLinks(ContentNode links, Dictionary<string, TreeNodeDef> byId)
        {
            var seen = new HashSet<(string, string)>();
            var count = 0;
            foreach (var link in links.Items())
            {
                var ends = link.Items();
                if (ends.Count != 2)
                    throw link.Error("a link joins exactly two nodes");
                var a = Find(ends[0], byId);
                var b = Find(ends[1], byId);
                if (ReferenceEquals(a, b))
                    throw link.Error($"node '{a.Id}' is linked to itself");
                var key = string.CompareOrdinal(a.Id, b.Id) < 0 ? (a.Id, b.Id) : (b.Id, a.Id);
                if (!seen.Add(key))
                    throw link.Error($"link {a.Id} — {b.Id} is listed twice");
                a.Connect(b);
                b.Connect(a);
                count++;
            }
            return count;
        }

        private static TreeNodeDef Find(ContentNode id, Dictionary<string, TreeNodeDef> byId)
        {
            if (!byId.TryGetValue(id.AsString(), out var node))
                throw id.Error($"unknown node '{id.AsString()}'");
            return node;
        }

        private static void RequireConnected(ContentNode links, List<TreeNodeDef> nodes)
        {
            if (nodes.Count == 0)
                return;
            var reached = new HashSet<TreeNodeDef> { nodes[0] };
            var queue = new Queue<TreeNodeDef>();
            queue.Enqueue(nodes[0]);
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
                    throw links.Error($"node '{node.Id}' cannot be reached from '{nodes[0].Id}', the tree must be one piece");
            }
        }
    }
}
