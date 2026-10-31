using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TreeShape
    {
        public TreeShape(IReadOnlyList<int> rowThresholds, int nodeCount, IReadOnlyList<int> majorsAt, int majorOptions)
        {
            RowThresholds = rowThresholds;
            NodeCount = nodeCount;
            MajorsAt = majorsAt;
            MajorOptions = majorOptions;
        }

        public IReadOnlyList<int> RowThresholds { get; }
        public int NodeCount { get; }
        public IReadOnlyList<int> MajorsAt { get; }
        public int MajorOptions { get; }
    }

    public sealed class CommonTreeSlotRule
    {
        public CommonTreeSlotRule(int level, int pointsPerLevel, int maxPoints)
        {
            Level = level;
            PointsPerLevel = pointsPerLevel;
            MaxPoints = maxPoints;
        }

        public int Level { get; }
        public int PointsPerLevel { get; }
        public int MaxPoints { get; }

        public bool OpenAt(int heroLevel) => heroLevel >= Level;

        public int PointsAt(int heroLevel) => System.Math.Max(0, System.Math.Min(MaxPoints, (heroLevel - Level) * PointsPerLevel));
    }

    public sealed class TreeTemplates
    {
        public const string FileName = "trees/templates.json";
        public const int SupportedVersion = 5;

        private TreeTemplates(RespecRules respec, TreeShape classShape, int passives, int barEvery, IReadOnlyList<string> classes,
            TreeShape commonShape, IReadOnlyList<CommonTreeSlotRule> commonSlots, IReadOnlyList<string> commonTrees)
        {
            Respec = respec;
            ClassShape = classShape;
            Passives = passives;
            BarEvery = barEvery;
            Classes = classes;
            CommonShape = commonShape;
            CommonSlots = commonSlots;
            CommonTrees = commonTrees;
        }

        public RespecRules Respec { get; }
        public TreeShape ClassShape { get; }
        public int Passives { get; }
        public int BarEvery { get; }
        public IReadOnlyList<string> Classes { get; }
        public TreeShape CommonShape { get; }
        public IReadOnlyList<CommonTreeSlotRule> CommonSlots { get; }
        public IReadOnlyList<string> CommonTrees { get; }
        public IReadOnlyCollection<string> MajorTalentsAvoid { get; private set; } = Array.Empty<string>();

        public static TreeTemplates Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var bar = root.Field("bar");
            var classShape = new TreeShape(ReadRows(root.Field("rows")), root.Field("nodes").AsInt(1, 1000),
                ReadRising(bar.Field("majorsAt"), "major talent marks"), bar.Field("majorOptions").AsInt(2, 10));
            var passives = root.Field("passives").AsInt(0, classShape.NodeCount);
            var classes = new List<string>(Sim.HeroClassNames.ByName.Keys);
            classes.AddRange(Sim.HeroClassNames.InDevelopment.Keys);
            var common = root.Field("commonTrees");
            var commonShape = new TreeShape(ReadRows(common.Field("rows")), common.Field("nodes").AsInt(1, 1000),
                ReadRising(common.Field("majorsAt"), "major talent marks"), common.Field("majorOptions").AsInt(2, 10));
            var avoid = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("majorTalentsAvoid").Items())
                avoid.Add(item.AsString());
            return new TreeTemplates(RespecRules.Read(root.Field("respec")), classShape, passives, bar.Field("every").AsInt(1, 1000), classes,
                commonShape, ReadSlots(common.Field("slots")), ReadCommonIds(common.Field("trees"), classes)) { MajorTalentsAvoid = avoid };
        }

        private static List<CommonTreeSlotRule> ReadSlots(ContentNode list)
        {
            var slots = new List<CommonTreeSlotRule>();
            foreach (var item in list.Items())
            {
                var level = item.Field("level").AsInt(1, 10_000);
                if (slots.Count > 0 && level <= slots[slots.Count - 1].Level)
                    throw item.Field("level").Error("common tree slots open at rising hero levels");
                slots.Add(new CommonTreeSlotRule(level, item.Field("pointsPerLevel").AsInt(1, 100), item.Field("maxPoints").AsInt(1, 10_000)));
            }
            return slots;
        }

        private static List<string> ReadCommonIds(ContentNode list, List<string> classes)
        {
            var ids = new List<string>();
            foreach (var item in list.Items())
            {
                var id = item.AsString();
                if (ids.Contains(id) || classes.Contains(id))
                    throw item.Error($"common tree '{id}' is listed twice or shares a class name");
                ids.Add(id);
            }
            return ids;
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
            var once = node.HasField("once") ? ModifierReader.ReadList(node.Field("once")) : Array.Empty<Modifier>();
            if (modifiers.Count + once.Count == 0 && (waits == null || text == null))
                throw node.Error("a line needs modifiers, or a text and the mechanic it waits for");
            return new RankLine(modifiers, text?.Field("ru").AsString() ?? string.Empty, text?.Field("en").AsString() ?? string.Empty, waits, once);
        }

        private static List<int> ReadRows(ContentNode node)
        {
            var rows = ReadRising(node, "row thresholds");
            if (rows[0] != 0)
                throw node.Error("the first row is open from the start, its threshold is 0");
            return rows;
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
