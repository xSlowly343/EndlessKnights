using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TreeTemplates
    {
        public const string FileName = "trees/templates.json";
        public const int SupportedVersion = 4;

        private readonly Dictionary<string, TreeTemplate> _common;

        private TreeTemplates(RespecRules respec, IReadOnlyList<int> rowThresholds, int nodeCount, int barEvery, IReadOnlyList<int> majorsAt, int majorOptions,
            Dictionary<string, TreeTemplate> common)
        {
            Respec = respec;
            RowThresholds = rowThresholds;
            NodeCount = nodeCount;
            BarEvery = barEvery;
            MajorsAt = majorsAt;
            MajorOptions = majorOptions;
            _common = common;
        }

        public RespecRules Respec { get; }
        public IReadOnlyList<int> RowThresholds { get; }
        public int NodeCount { get; }
        public int BarEvery { get; }
        public IReadOnlyList<int> MajorsAt { get; }
        public int MajorOptions { get; }
        public IReadOnlyCollection<TreeTemplate> Common => _common.Values;

        public bool TryGet(string id, out TreeTemplate template) => _common.TryGetValue(id, out template!);

        public static TreeTemplates Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var rows = ReadRising(root.Field("rows"), "row thresholds");
            if (rows[0] != 0)
                throw root.Field("rows").Error("the first row is open from the start, its threshold is 0");
            var bar = root.Field("bar");
            var majorsAt = ReadRising(bar.Field("majorsAt"), "major talent marks");
            var common = new Dictionary<string, TreeTemplate>(StringComparer.Ordinal);
            var templates = root.Field("templates");
            foreach (var id in templates.FieldNames())
                common[id] = ReadTemplate(templates.Field(id), id);
            return new TreeTemplates(RespecRules.Read(root.Field("respec")), rows, root.Field("nodes").AsInt(1, 1000),
                bar.Field("every").AsInt(1, 1000), majorsAt, bar.Field("majorOptions").AsInt(2, 10), common);
        }

        public static TreeTemplate ReadTemplate(ContentNode node, string id)
        {
            var name = node.Field("name");
            return new TreeTemplate(id, name.Field("ru").AsString(), name.Field("en").AsString(),
                ReadLine(node.Field("a")), ReadLine(node.Field("b")), ReadLine(node.Field("c")));
        }

        public static RankLine ReadLine(ContentNode node)
        {
            var modifiers = node.HasField("modifiers") ? ModifierReader.ReadList(node.Field("modifiers")) : Array.Empty<Modifier>();
            var waits = node.HasField("waits") ? node.Field("waits").AsString() : null;
            var text = node.HasField("text") ? node.Field("text") : (ContentNode?)null;
            if (modifiers.Count == 0 && (waits == null || text == null))
                throw node.Error("a line needs modifiers, or a text and the mechanic it waits for");
            return new RankLine(modifiers, text?.Field("ru").AsString() ?? string.Empty, text?.Field("en").AsString() ?? string.Empty, waits);
        }

        private static List<int> ReadRising(ContentNode node, string what)
        {
            var values = new List<int>();
            foreach (var item in node.Items())
            {
                var value = item.AsInt(0, 100_000);
                if (values.Count > 0 && value <= values[values.Count - 1])
                    throw item.Error($"{what} must rise");
                values.Add(value);
            }
            if (values.Count == 0)
                throw node.Error($"at least one value of {what} is required");
            return values;
        }
    }
}
