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
                    foreach (var modifier in node.Modifiers)
                        lines.Add(ModifierText.Describe(modifier));
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

        private static void AddOptions(List<string> lines, TreeAllocation allocation, TreeNodeDef node)
        {
            allocation.MasteryChoices.TryGetValue(node.Id, out var chosen);
            var taken = allocation.MasteryChoices.ContainsKey(node.Id);
            for (var i = 0; i < node.Options.Count; i++)
            {
                var parts = new List<string>();
                foreach (var modifier in node.Options[i])
                    parts.Add(ModifierText.Describe(modifier));
                var mark = taken && chosen == i ? "> " : string.Empty;
                lines.Add($"{mark}{i + 1}) {string.Join(", ", parts)}");
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
