using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public static class ClassTreeLoader
    {
        public const int SupportedVersion = 4;

        public static string FileNameOf(string heroClass) => "trees/" + heroClass + ".json";

        public static ClassTree Load(TreeTemplates templates, string heroClass, string json)
        {
            var root = ContentNode.Root(json, FileNameOf(heroClass));
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var classNode = root.Field("class");
            if (classNode.AsString() != heroClass)
                throw classNode.Error($"the file of '{heroClass}' holds the tree of '{classNode.AsString()}'");

            var nodes = ReadNodes(root.Field("nodes"), templates, heroClass);
            var passives = new List<IReadOnlyList<Modifier>>();
            foreach (var item in root.Field("barPassives").Items())
                passives.Add(ModifierReader.ReadList(item));
            if (passives.Count == 0)
                throw root.Field("barPassives").Error("the mastery bar needs at least one passive");
            var majors = ReadMajors(root.Field("majors"), templates, heroClass);
            return new ClassTree(heroClass, nodes, templates.RowThresholds, templates.BarEvery, passives, majors, templates.Respec);
        }

        private static List<ClassTreeNode> ReadNodes(ContentNode list, TreeTemplates templates, string heroClass)
        {
            var nodes = new List<ClassTreeNode>();
            var byId = new Dictionary<string, ClassTreeNode>(StringComparer.Ordinal);
            var places = new HashSet<(int, int)>();
            var rowsUsed = new HashSet<int>();
            var prefix = heroClass + "_";
            foreach (var item in list.Items())
            {
                var idNode = item.Field("id");
                var id = idNode.AsString();
                if (!id.StartsWith(prefix, StringComparison.Ordinal))
                    throw idNode.Error($"node ids of this tree start with '{prefix}'");
                if (byId.ContainsKey(id))
                    throw idNode.Error($"node id '{id}' is used twice");
                var row = item.Field("row").AsInt(1, templates.RowThresholds.Count);
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
                var (template, common) = Template(item, templates, id);
                var node = new ClassTreeNode(id, row, column, parentId, template, common);
                byId.Add(id, node);
                nodes.Add(node);
                rowsUsed.Add(row);
            }
            if (nodes.Count != templates.NodeCount)
                throw list.Error($"the tree has {nodes.Count} nodes, expected {templates.NodeCount} ({TreeTemplates.FileName} → nodes)");
            for (var row = 1; row <= templates.RowThresholds.Count; row++)
            {
                if (!rowsUsed.Contains(row))
                    throw list.Error($"row {row} has no nodes");
            }
            return nodes;
        }

        private static (TreeTemplate, bool) Template(ContentNode item, TreeTemplates templates, string id)
        {
            if (!item.HasField("t"))
                return (TreeTemplates.ReadTemplate(item, id), false);
            var templateNode = item.Field("t");
            if (!templates.TryGet(templateNode.AsString(), out var template))
                throw templateNode.Error($"unknown common template '{templateNode.AsString()}'");
            return (template, true);
        }

        private static List<MajorTalentSlot> ReadMajors(ContentNode list, TreeTemplates templates, string heroClass)
        {
            var slots = new List<MajorTalentSlot>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var items = list.Items();
            if (items.Count != templates.MajorsAt.Count)
                throw list.Error($"expected {templates.MajorsAt.Count} major talent marks: {string.Join(", ", templates.MajorsAt)}");
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var at = item.Field("at").AsInt(0, 100_000);
                if (at != templates.MajorsAt[i])
                    throw item.Field("at").Error($"mark {i + 1} is at {templates.MajorsAt[i]} points");
                var options = new List<MajorTalentDef>();
                foreach (var option in item.Field("options").Items())
                {
                    var id = option.Field("id").AsString();
                    if (!id.StartsWith(heroClass + "_", StringComparison.Ordinal) || !ids.Add(id))
                        throw option.Field("id").Error($"major talent id '{id}' must be new and start with '{heroClass}_'");
                    var name = option.Field("name");
                    var line = TreeTemplates.ReadLine(option);
                    options.Add(new MajorTalentDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), line.TextRu, line.TextEn, line.Modifiers, line.Waits));
                }
                if (options.Count != templates.MajorOptions)
                    throw item.Field("options").Error($"a mark offers {templates.MajorOptions} major talents");
                slots.Add(new MajorTalentSlot(at, options));
            }
            return slots;
        }
    }
}
