using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public static class ModifierReader
    {
        private static readonly Dictionary<string, ModifierKind> Kinds = new Dictionary<string, ModifierKind>(StringComparer.Ordinal)
        {
            ["flat"] = ModifierKind.Flat,
            ["increased"] = ModifierKind.Increased,
            ["more"] = ModifierKind.More
        };

        public static Modifier Read(ContentNode node)
        {
            var kind = ReadKind(node.Field("kind"));
            var tags = node.HasField("tags") ? ReadTags(node.Field("tags")) : StatTags.None;
            var stat = new StatId(node.Field("stat").AsString());
            if (node.HasField("skill"))
                stat = StatId.ForSkill(stat, node.Field("skill").AsString());
            return new Modifier(stat, kind, node.Field("value").AsNumber(-1e6, 1e6), tags);
        }

        public static ModifierKind ReadKind(ContentNode node)
        {
            var kindName = node.AsString();
            if (!Kinds.TryGetValue(kindName, out var kind))
                throw node.Error($"unknown modifier kind '{kindName}', expected flat, increased or more");
            return kind;
        }

        public static IReadOnlyList<Modifier> ReadList(ContentNode node)
        {
            var modifiers = new List<Modifier>();
            foreach (var item in node.Items())
                modifiers.Add(Read(item));
            return modifiers;
        }

        public static StatTags ReadTags(ContentNode node)
        {
            var tags = StatTags.None;
            foreach (var tagNode in node.Items())
            {
                var tagName = tagNode.AsString();
                if (!Enum.TryParse<StatTags>(tagName, false, out var tag) || tag == StatTags.None)
                    throw tagNode.Error($"unknown tag '{tagName}'");
                tags |= tag;
            }
            return tags;
        }
    }
}
