using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public static class TreeNodeText
    {
        public static IReadOnlyList<string> Describe(PassiveTree tree, TreeAllocation allocation, TreeNodeDef node, BigNumber? refundCost, NumberFormatSettings format)
        {
            var lines = new List<string> { node.NameRu + " · " + TypeLabel(node) };
            switch (node.Type)
            {
                case TreeNodeType.Attribute:
                    lines.Add(AttributeLine(allocation, node));
                    break;
                case TreeNodeType.Mastery:
                    AddOptions(lines, allocation, node);
                    break;
                default:
                    AddModifiers(lines, allocation, node);
                    break;
            }
            lines.Add(Status(tree, allocation, node, refundCost, format));
            return lines;
        }

        public static string TypeLabel(TreeNodeDef node) => node.Type switch
        {
            TreeNodeType.Small => "малый узел",
            TreeNodeType.Notable => "значимый узел",
            TreeNodeType.Keystone => "ключевой узел",
            TreeNodeType.Mastery => "мастерство",
            TreeNodeType.Jewel => "гнездо самоцвета",
            TreeNodeType.Attribute => "атрибут",
            _ => "старт класса"
        };

        public static string AttributeName(string attribute) => ModifierText.Label(new StatId(attribute));

        private static string AttributeLine(TreeAllocation allocation, TreeNodeDef node)
        {
            var amount = "+" + node.AttributeAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return allocation.AttributeChoices.TryGetValue(node.Id, out var chosen)
                ? $"{amount} {AttributeName(chosen)}"
                : $"{amount} к выбранному атрибуту";
        }

        public const string ActiveMark = "> ";
        public const string OwnPrefix = "Для класса: ";
        public const string SharedPrefix = "Для остальных: ";

        private static void AddModifiers(List<string> lines, TreeAllocation allocation, TreeNodeDef node)
        {
            if (!node.IsDual)
            {
                foreach (var modifier in node.Modifiers)
                    lines.Add(ModifierText.Describe(modifier));
                return;
            }
            var own = node.IsOwnFor(allocation.StartClass);
            lines.Add(DualLine(own, OwnPrefix, node.Modifiers));
            lines.Add(DualLine(!own, SharedPrefix, node.SharedModifiers));
        }

        private static string DualLine(bool active, string prefix, IReadOnlyList<Modifier> modifiers) =>
            (active ? ActiveMark : string.Empty) + prefix + Join(modifiers);

        private static string Join(IReadOnlyList<Modifier> modifiers)
        {
            var parts = new List<string>();
            foreach (var modifier in modifiers)
                parts.Add(ModifierText.Describe(modifier));
            return parts.Count == 0 ? "ничего" : string.Join(", ", parts);
        }

        private static IReadOnlyList<Modifier> SharedOption(TreeNodeDef node, int option) =>
            option < node.SharedOptions.Count ? node.SharedOptions[option] : System.Array.Empty<Modifier>();

        private static void AddOptions(List<string> lines, TreeAllocation allocation, TreeNodeDef node)
        {
            allocation.MasteryChoices.TryGetValue(node.Id, out var chosen);
            var taken = allocation.MasteryChoices.ContainsKey(node.Id);
            var own = node.IsOwnFor(allocation.StartClass);
            for (var i = 0; i < node.Options.Count; i++)
            {
                var mark = taken && chosen == i ? ActiveMark : string.Empty;
                lines.Add($"{mark}{i + 1}) {Join(node.OptionFor(i, allocation.StartClass))}");
                if (node.IsDual)
                    lines.Add("   " + (own ? SharedPrefix + Join(SharedOption(node, i)) : OwnPrefix + Join(node.Options[i])));
            }
        }

        private static string Status(PassiveTree tree, TreeAllocation allocation, TreeNodeDef node, BigNumber? refundCost, NumberFormatSettings format)
        {
            if (node.Type == TreeNodeType.Start)
                return node.StartClass == allocation.StartClass ? "Твой старт" : "Старт другого класса";
            if (allocation.IsAllocated(tree, node.Id))
            {
                if (refundCost == null)
                    return "Взят";
                return allocation.CanRemove(tree, node.Id)
                    ? "Взят · вернуть за " + NumberFormatter.Format(refundCost.Value, format) + " золота"
                    : "Взят · держит путь к другим узлам";
            }
            return allocation.Check(tree, node.Id) switch
            {
                AllocationResult.Locked => "Закрыт",
                AllocationResult.NoPoints => "Нет свободных очков",
                AllocationResult.NotConnected => "Нет связи со взятыми узлами",
                _ => "Можно взять"
            };
        }
    }
}
