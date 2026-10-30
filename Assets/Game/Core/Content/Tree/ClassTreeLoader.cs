using System;
using System.Collections.Generic;

namespace Game.Core.Content.Tree
{
    public static class ClassTreeLoader
    {
        public const int SupportedVersion = 3;

        public static string FileNameOf(string heroClass) => "trees/" + heroClass + ".json";

        public static PassiveTree Load(TreeTemplates templates, string heroClass, string json)
        {
            var fileName = FileNameOf(heroClass);
            var root = ContentNode.Root(json, fileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var classNode = root.Field("class");
            if (classNode.AsString() != heroClass)
                throw classNode.Error($"the file of '{heroClass}' holds the tree of '{classNode.AsString()}'");
            if (!Contains(templates.Classes, heroClass))
                throw classNode.Error($"class '{heroClass}' has no start and templates in {TreeTemplates.FileName}");

            var nodes = new List<TreeNodeDef>();
            var byId = new Dictionary<string, TreeNodeDef>(StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var prefix = heroClass + "_";
            TreeNodeDef? start = null;
            var list = root.Field("nodes");
            foreach (var item in list.Items())
            {
                var idNode = item.Field("id");
                var id = idNode.AsString();
                if (!id.StartsWith(prefix, StringComparison.Ordinal))
                    throw idNode.Error($"node ids of this tree start with '{prefix}'");
                if (byId.ContainsKey(id))
                    throw idNode.Error($"node id '{id}' is used twice");
                var templateNode = item.Field("t");
                if (!templates.TryGet(heroClass, templateNode.AsString(), out var template))
                    throw templateNode.Error($"unknown template '{templateNode.AsString()}', expected a common template or one of '{heroClass}'");
                var locked = item.HasField("locked") && item.Field("locked").AsBool();
                var node = template.Place(id, item.Field("x").AsNumber(-1000, 1000), item.Field("y").AsNumber(-1000, 1000), locked);
                if (node.Type == TreeNodeType.Start)
                {
                    if (start != null)
                        throw templateNode.Error("a class tree has one start");
                    start = node;
                }
                used.Add(template.Id);
                byId.Add(id, node);
                nodes.Add(node);
            }
            if (start == null)
                throw list.Error("the tree has no start");
            if (nodes.Count < templates.MinNodes || nodes.Count > templates.MaxNodes)
                throw list.Error($"the tree has {nodes.Count} nodes, expected {templates.MinNodes}–{templates.MaxNodes} ({TreeTemplates.FileName} → size)");
            foreach (var own in templates.OwnOf(heroClass))
            {
                if (!used.Contains(own.Id))
                    throw list.Error($"class template '{own.Id}' is not placed on the tree");
            }
            var links = PassiveTree.ReadLinks(root.Field("links"), byId);
            PassiveTree.RequireConnected(root.Field("links"), nodes);
            return new PassiveTree(nodes, templates.Attributes, links, templates.Respec);
        }

        private static bool Contains(IReadOnlyCollection<string> classes, string heroClass)
        {
            foreach (var name in classes)
            {
                if (name == heroClass)
                    return true;
            }
            return false;
        }
    }
}
