using System.Collections.Generic;
using Game.Core.Content.Tree;

namespace Game.Core.Build
{
    public enum TreeActionKind
    {
        Allocate,
        ChangeMastery,
        Refund
    }

    public readonly struct TreeAction
    {
        public readonly TreeActionKind Kind;
        public readonly string Label;
        public readonly string? Attribute;
        public readonly int? MasteryOption;

        public TreeAction(TreeActionKind kind, string label, string? attribute = null, int? masteryOption = null)
        {
            Kind = kind;
            Label = label;
            Attribute = attribute;
            MasteryOption = masteryOption;
        }

        public static IReadOnlyList<TreeAction> For(PassiveTree tree, TreeAllocation allocation, TreeNodeDef node)
        {
            var actions = new List<TreeAction>();
            if (node.Type == TreeNodeType.Start)
                return actions;

            if (allocation.IsAllocated(tree, node.Id))
            {
                if (node.Type == TreeNodeType.Mastery)
                    AddMasteryOptions(actions, TreeActionKind.ChangeMastery, node, allocation);
                if (allocation.CanRemove(tree, node.Id))
                    actions.Add(new TreeAction(TreeActionKind.Refund, "Вернуть"));
                return actions;
            }

            var check = allocation.Check(tree, node.Id);
            if (check != AllocationResult.Allocated && check != AllocationResult.ChoiceRequired)
                return actions;

            if (node.Type == TreeNodeType.Attribute)
            {
                foreach (var attribute in tree.Attributes)
                    actions.Add(new TreeAction(TreeActionKind.Allocate, TreeNodeText.AttributeName(attribute), attribute));
            }
            else if (node.Type == TreeNodeType.Mastery)
                AddMasteryOptions(actions, TreeActionKind.Allocate, node, allocation);
            else
                actions.Add(new TreeAction(TreeActionKind.Allocate, "Взять"));
            return actions;
        }

        private static void AddMasteryOptions(List<TreeAction> actions, TreeActionKind kind, TreeNodeDef node, TreeAllocation allocation)
        {
            var hasChoice = allocation.MasteryChoices.TryGetValue(node.Id, out var chosen);
            for (var i = 0; i < node.Options.Count; i++)
                if (kind == TreeActionKind.Allocate || !hasChoice || chosen != i)
                    actions.Add(new TreeAction(kind, "Вариант " + (i + 1), masteryOption: i));
        }
    }
}
