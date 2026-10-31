using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public static class ClassTreeLoader
    {
        public const int SupportedVersion = 6;

        public static string FileNameOf(string heroClass) => "trees/" + heroClass + ".json";

        public static string CommonFileNameOf(string treeId) => "trees/common/" + treeId + ".json";

        public static ClassTree Load(TreeTemplates templates, string heroClass, string json)
        {
            var root = Root(json, FileNameOf(heroClass), "class", heroClass);
            var shape = templates.ClassShape;
            var nodes = ReadNodes(root.Field("nodes"), shape, heroClass);
            var passives = 0;
            foreach (var node in nodes)
            {
                if (node.Passive)
                    passives++;
            }
            if (passives != templates.Passives)
                throw root.Field("nodes").Error($"the tree has {passives} class passives, expected {templates.Passives} ({TreeTemplates.FileName} → passives)");
            var barPassives = new List<IReadOnlyList<Modifier>>();
            foreach (var item in root.Field("barPassives").Items())
                barPassives.Add(ModifierReader.ReadList(item));
            if (barPassives.Count == 0)
                throw root.Field("barPassives").Error("the mastery bar needs at least one passive");
            var majors = ReadMajors(root.Field("majors"), shape, heroClass, nodes, templates.MajorTalentsAvoid);
            return new ClassTree(heroClass, false, nodes, shape.RowThresholds, templates.BarEvery, barPassives, majors, templates.Respec)
            {
                Attribute = ClassAttributeDef.Read(root.Field("attribute")),
            };
        }

        public static ClassTree LoadCommon(TreeTemplates templates, string treeId, string json)
        {
            var root = Root(json, CommonFileNameOf(treeId), "tree", treeId);
            var shape = templates.CommonShape;
            var nodes = ReadNodes(root.Field("nodes"), shape, treeId);
            foreach (var item in root.Field("nodes").Items())
            {
                if (item.HasField("passive"))
                    throw item.Field("passive").Error("a common tree has no class passives");
            }
            var majors = ReadMajors(root.Field("majors"), shape, treeId, nodes, templates.MajorTalentsAvoid);
            var name = root.Field("name");
            var about = root.Field("about");
            return new ClassTree(treeId, true, nodes, shape.RowThresholds, 0, Array.Empty<IReadOnlyList<Modifier>>(), majors, templates.Respec)
            {
                NameRu = name.Field("ru").AsString(),
                NameEn = name.Field("en").AsString(),
                AboutRu = about.Field("ru").AsString(),
                AboutEn = about.Field("en").AsString(),
            };
        }

        private static ContentNode Root(string json, string fileName, string idField, string id)
        {
            var root = ContentNode.Root(json, fileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var idNode = root.Field(idField);
            if (idNode.AsString() != id)
                throw idNode.Error($"the file of '{id}' holds the tree of '{idNode.AsString()}'");
            return root;
        }

        private static List<ClassTreeNode> ReadNodes(ContentNode list, TreeShape shape, string treeId)
        {
            var nodes = new List<ClassTreeNode>();
            var byId = new Dictionary<string, ClassTreeNode>(StringComparer.Ordinal);
            var places = new HashSet<(int, int)>();
            var rowsUsed = new HashSet<int>();
            var prefix = treeId + "_";
            foreach (var item in list.Items())
            {
                var idNode = item.Field("id");
                var id = idNode.AsString();
                if (!id.StartsWith(prefix, StringComparison.Ordinal))
                    throw idNode.Error($"node ids of this tree start with '{prefix}'");
                if (byId.ContainsKey(id))
                    throw idNode.Error($"node id '{id}' is used twice");
                var row = item.Field("row").AsInt(1, shape.RowThresholds.Count);
                var column = item.Field("column").AsInt(0, 100);
                if (!places.Add((row, column)))
                    throw item.Field("column").Error($"row {row} already has a node in column {column}");
                var parentId = item.HasField("parent") ? item.Field("parent").AsString() : null;
                if (parentId != null)
                {
                    if (!byId.TryGetValue(parentId, out var parent))
                        throw item.Field("parent").Error($"parent '{parentId}' must be listed earlier");
                    if (parent.Row >= row)
                        throw item.Field("parent").Error($"parent '{parentId}' is in row {parent.Row}, it must be in a lower row than {row}");
                }
                var passive = item.HasField("passive") && item.Field("passive").AsBool();
                var node = new ClassTreeNode(id, row, column, parentId, TreeTemplates.ReadTemplate(item, id), passive);
                byId.Add(id, node);
                nodes.Add(node);
                rowsUsed.Add(row);
            }
            if (nodes.Count != shape.NodeCount)
                throw list.Error($"the tree has {nodes.Count} nodes, expected {shape.NodeCount} ({TreeTemplates.FileName})");
            for (var row = 1; row <= shape.RowThresholds.Count; row++)
            {
                if (!rowsUsed.Contains(row))
                    throw list.Error($"row {row} has no nodes");
            }
            return nodes;
        }

        private static List<MajorTalentSlot> ReadMajors(ContentNode list, TreeShape shape, string treeId, List<ClassTreeNode> nodes, IReadOnlyCollection<string> avoid)
        {
            var slots = new List<MajorTalentSlot>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
                ids.Add(node.Id);
            var items = list.Items();
            if (items.Count != shape.MajorsAt.Count)
                throw list.Error($"expected {shape.MajorsAt.Count} major talent marks: {string.Join(", ", shape.MajorsAt)}");
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var at = item.Field("at").AsInt(0, 100_000);
                if (at != shape.MajorsAt[i])
                    throw item.Field("at").Error($"mark {i + 1} is at {shape.MajorsAt[i]} points");
                var options = new List<MajorTalentDef>();
                foreach (var option in item.Field("options").Items())
                {
                    var id = option.Field("id").AsString();
                    if (!id.StartsWith(treeId + "_", StringComparison.Ordinal) || !ids.Add(id))
                        throw option.Field("id").Error($"major talent id '{id}' must differ from other talents and nodes and start with '{treeId}_'");
                    var name = option.Field("name");
                    var line = TreeTemplates.ReadLine(option);
                    foreach (var modifier in line.Modifiers)
                    {
                        if (avoid.Contains(modifier.Stat.Name))
                            throw option.Field("modifiers").Error($"major talent '{id}' uses '{modifier.Stat.Name}': it belongs to class features and paths ({TreeTemplates.FileName} → majorTalentsAvoid)");
                    }
                    options.Add(new MajorTalentDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), line.TextRu, line.TextEn, line.Modifiers, line.Waits));
                }
                if (options.Count != shape.MajorOptions)
                    throw item.Field("options").Error($"a mark offers {shape.MajorOptions} major talents");
                slots.Add(new MajorTalentSlot(at, options));
            }
            return slots;
        }
    }
}
