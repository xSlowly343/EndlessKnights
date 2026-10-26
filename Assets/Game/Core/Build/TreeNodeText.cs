using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Build
{
    public static class TreeNodeText
    {
        public static IReadOnlyList<string> Describe(PassiveTree tree, TreeAllocation allocation, TreeNodeDef node, BigNumber? refundCost, NumberFormatSettings format)
        {
            var lines = new List<string> { Texts.Pick(node.NameRu, node.NameEn) + " · " + TypeLabel(node) };
            switch (node.Type)
            {
                case TreeNodeType.Attribute:
                    lines.Add(AttributeLine(allocation, node));
                    break;
                case TreeNodeType.Mastery:
                    AddOptions(lines, allocation, node);
                    break;
                default:
                    AddModifiers(lines, node);
                    break;
            }
            lines.Add(Status(tree, allocation, node, refundCost, format));
            return lines;
        }

        public static string TypeLabel(TreeNodeDef node) => node.Type switch
        {
            TreeNodeType.Small => Texts.Get("tree.type.small"),
            TreeNodeType.Notable => Texts.Get("tree.type.notable"),
            TreeNodeType.Keystone => Texts.Get("tree.type.keystone"),
            TreeNodeType.Mastery => Texts.Get("tree.type.mastery"),
            TreeNodeType.Jewel => Texts.Get("tree.type.jewel"),
            TreeNodeType.Attribute => Texts.Get("tree.type.attribute"),
            _ => Texts.Get("tree.type.start")
        };

        public static string AttributeName(string attribute) => ModifierText.Label(new StatId(attribute));

        private static string AttributeLine(TreeAllocation allocation, TreeNodeDef node)
        {
            var amount = "+" + node.AttributeAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return allocation.AttributeChoices.TryGetValue(node.Id, out var chosen)
                ? $"{amount} {AttributeName(chosen)}"
                : Texts.Format("tree.toChosenAttribute", amount);
        }

        public const string ActiveMark = "> ";

        private static void AddModifiers(List<string> lines, TreeNodeDef node)
        {
            foreach (var modifier in node.Modifiers)
                lines.Add(ModifierText.Describe(modifier));
        }

        private static string Join(IReadOnlyList<Modifier> modifiers)
        {
            var parts = new List<string>();
            foreach (var modifier in modifiers)
                parts.Add(ModifierText.Describe(modifier));
            return parts.Count == 0 ? Texts.Get("tree.nothing") : string.Join(", ", parts);
        }

        private static void AddOptions(List<string> lines, TreeAllocation allocation, TreeNodeDef node)
        {
            allocation.MasteryChoices.TryGetValue(node.Id, out var chosen);
            var taken = allocation.MasteryChoices.ContainsKey(node.Id);
            for (var i = 0; i < node.Options.Count; i++)
            {
                var mark = taken && chosen == i ? ActiveMark : string.Empty;
                lines.Add($"{mark}{i + 1}) {Join(node.Options[i])}");
            }
        }

        private static string Status(PassiveTree tree, TreeAllocation allocation, TreeNodeDef node, BigNumber? refundCost, NumberFormatSettings format)
        {
            if (node.Type == TreeNodeType.Start)
                return node.StartClass == allocation.StartClass ? Texts.Get("tree.status.ownStart") : Texts.Get("tree.status.otherStart");
            if (allocation.IsAllocated(tree, node.Id))
            {
                if (refundCost == null)
                    return Texts.Get("tree.status.taken");
                return allocation.CanRemove(tree, node.Id)
                    ? Texts.Format("tree.status.refundFor", NumberFormatter.Format(refundCost.Value, format))
                    : Texts.Get("tree.status.holdsPath");
            }
            return allocation.Check(tree, node.Id) switch
            {
                AllocationResult.Locked => Texts.Get("tree.status.locked"),
                AllocationResult.NoPoints => Texts.Get("tree.status.noPoints"),
                AllocationResult.NotConnected => Texts.Get("tree.status.notConnected"),
                _ => Texts.Get("tree.status.canTake")
            };
        }
    }
}
