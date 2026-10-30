using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TreeTemplates
    {
        public const string FileName = "trees/templates.json";
        public const int SupportedVersion = 3;
        public const string StartTemplate = "start";

        private static readonly Dictionary<string, TreeNodeType> Types = new Dictionary<string, TreeNodeType>(StringComparer.Ordinal)
        {
            ["small"] = TreeNodeType.Small,
            ["notable"] = TreeNodeType.Notable,
            ["keystone"] = TreeNodeType.Keystone,
            ["mastery"] = TreeNodeType.Mastery,
            ["jewel"] = TreeNodeType.Jewel,
            ["attribute"] = TreeNodeType.Attribute
        };

        private static readonly HashSet<string> Roles = new HashSet<string>(StringComparer.Ordinal) { "dd", "buffer", "debuffer", "dot", "summoner" };

        private readonly Dictionary<string, TreeTemplate> _common;
        private readonly Dictionary<string, Dictionary<string, TreeTemplate>> _classes;

        private TreeTemplates(IReadOnlyList<string> attributes, RespecRules respec, int minNodes, int maxNodes,
            Dictionary<string, TreeTemplate> common, Dictionary<string, Dictionary<string, TreeTemplate>> classes)
        {
            Attributes = attributes;
            Respec = respec;
            MinNodes = minNodes;
            MaxNodes = maxNodes;
            _common = common;
            _classes = classes;
        }

        public IReadOnlyList<string> Attributes { get; }
        public RespecRules Respec { get; }
        public int MinNodes { get; }
        public int MaxNodes { get; }
        public IReadOnlyCollection<TreeTemplate> Common => _common.Values;
        public IReadOnlyCollection<string> Classes => _classes.Keys;

        public IReadOnlyCollection<TreeTemplate> OwnOf(string heroClass) =>
            _classes.TryGetValue(heroClass, out var own) ? own.Values : (IReadOnlyCollection<TreeTemplate>)Array.Empty<TreeTemplate>();

        public bool TryGet(string heroClass, string id, out TreeTemplate template)
        {
            if (_classes.TryGetValue(heroClass, out var own) && own.TryGetValue(id, out template!))
                return true;
            return _common.TryGetValue(id, out template!);
        }

        public static TreeTemplates Load(string json)
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
            var size = root.Field("size");
            var minNodes = size.Field("min").AsInt(1, 100_000);
            var maxNodes = size.Field("max").AsInt(minNodes, 100_000);

            var common = ReadTemplates(root.Field("templates"), null);
            var classes = new Dictionary<string, Dictionary<string, TreeTemplate>>(StringComparer.Ordinal);
            var classesNode = root.Field("classes");
            foreach (var heroClass in classesNode.FieldNames())
            {
                var entry = classesNode.Field(heroClass);
                var own = ReadTemplates(entry.Field("templates"), heroClass);
                foreach (var id in own.Keys)
                {
                    if (common.ContainsKey(id))
                        throw entry.Field("templates").Field(id).Error($"template '{id}' is already a common template");
                }
                var start = entry.Field("start");
                var name = start.Field("name");
                own[StartTemplate] = new TreeTemplate(StartTemplate, TreeNodeType.Start, StartTemplate, name.Field("ru").AsString(), name.Field("en").AsString(),
                    string.Empty, string.Empty, ModifierReader.ReadList(start.Field("modifiers")), Array.Empty<IReadOnlyList<Modifier>>(), 0, Array.Empty<string>(), heroClass);
                classes[heroClass] = own;
            }
            return new TreeTemplates(attributes, RespecRules.Read(root.Field("respec")), minNodes, maxNodes, common, classes);
        }

        private static Dictionary<string, TreeTemplate> ReadTemplates(ContentNode node, string? heroClass)
        {
            var templates = new Dictionary<string, TreeTemplate>(StringComparer.Ordinal);
            foreach (var id in node.FieldNames())
            {
                if (id == StartTemplate)
                    throw node.Field(id).Error($"'{StartTemplate}' is the class start, not a template");
                templates[id] = ReadTemplate(node.Field(id), id, heroClass);
            }
            return templates;
        }

        private static TreeTemplate ReadTemplate(ContentNode node, string id, string? heroClass)
        {
            var typeNode = node.Field("type");
            if (!Types.TryGetValue(typeNode.AsString(), out var type))
                throw typeNode.Error($"unknown template type '{typeNode.AsString()}'");
            var options = new List<IReadOnlyList<Modifier>>();
            if (type == TreeNodeType.Mastery)
            {
                foreach (var option in node.Field("options").Items())
                    options.Add(ModifierReader.ReadList(option));
                if (options.Count < 2)
                    throw node.Field("options").Error("a mastery needs at least two options");
            }
            var modifiers = node.HasField("modifiers") ? ModifierReader.ReadList(node.Field("modifiers")) : Array.Empty<Modifier>();
            if (modifiers.Count == 0 && (type == TreeNodeType.Small || type == TreeNodeType.Notable || type == TreeNodeType.Keystone))
                throw node.Error($"a {typeNode.AsString()} template needs modifiers");
            var amount = type == TreeNodeType.Attribute ? node.Field("amount").AsNumber(0.01, 1000) : 0;
            var roles = new List<string>();
            if (node.HasField("roles"))
            {
                foreach (var role in node.Field("roles").Items())
                {
                    if (!Roles.Contains(role.AsString()))
                        throw role.Error($"unknown role '{role.AsString()}', expected dd, buffer, debuffer, dot or summoner");
                    roles.Add(role.AsString());
                }
            }
            var theme = node.HasField("theme") ? node.Field("theme").AsString() : typeNode.AsString();
            var name = node.Field("name");
            return new TreeTemplate(id, type, theme, name.Field("ru").AsString(), name.Field("en").AsString(),
                PassiveTree.DescriptionText(node, "ru"), PassiveTree.DescriptionText(node, "en"), modifiers, options, amount, roles, heroClass);
        }
    }
}
