using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public sealed class TreeAllocation
    {
        private readonly HashSet<string> _allocated = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _masteries = new Dictionary<string, int>(StringComparer.Ordinal);

        public TreeAllocation(string startClass)
        {
            StartClass = startClass;
        }

        public string StartClass { get; }
        public int PointsEarned { get; set; }
        public IReadOnlyCollection<string> Allocated => _allocated;
        public IReadOnlyDictionary<string, string> AttributeChoices => _attributes;
        public IReadOnlyDictionary<string, int> MasteryChoices => _masteries;
        public int PointsSpent => _allocated.Count;
        public int PointsLeft => PointsEarned - PointsSpent;

        public bool IsAllocated(PassiveTree tree, string nodeId) =>
            _allocated.Contains(nodeId) || string.Equals(tree.StartOf(StartClass)?.Id, nodeId, StringComparison.Ordinal);

        public AllocationResult Check(PassiveTree tree, string nodeId, string? attribute = null, int? masteryOption = null)
        {
            if (!tree.TryGet(nodeId, out var node))
                return AllocationResult.UnknownNode;
            if (IsAllocated(tree, nodeId))
                return AllocationResult.AlreadyAllocated;
            if (node.Locked || node.Type == TreeNodeType.Start)
                return AllocationResult.Locked;
            if (PointsLeft <= 0)
                return AllocationResult.NoPoints;
            if (!TouchesAllocated(tree, node))
                return AllocationResult.NotConnected;
            return CheckChoice(tree, node, attribute, masteryOption);
        }

        public AllocationResult Allocate(PassiveTree tree, string nodeId, string? attribute = null, int? masteryOption = null)
        {
            var result = Check(tree, nodeId, attribute, masteryOption);
            if (result != AllocationResult.Allocated)
                return result;
            _allocated.Add(nodeId);
            if (attribute != null)
                _attributes[nodeId] = attribute;
            if (masteryOption.HasValue)
                _masteries[nodeId] = masteryOption.Value;
            return AllocationResult.Allocated;
        }

        public bool ChangeMastery(PassiveTree tree, string nodeId, int option)
        {
            if (!_allocated.Contains(nodeId) || !tree.TryGet(nodeId, out var node) || node.Type != TreeNodeType.Mastery)
                return false;
            if (option < 0 || option >= node.Options.Count)
                return false;
            _masteries[nodeId] = option;
            return true;
        }

        public void Restore(string nodeId, string? attribute, int? masteryOption)
        {
            _allocated.Add(nodeId);
            if (attribute != null)
                _attributes[nodeId] = attribute;
            if (masteryOption.HasValue)
                _masteries[nodeId] = masteryOption.Value;
        }

        public IReadOnlyList<Modifier> Modifiers(PassiveTree tree)
        {
            var modifiers = new List<Modifier>();
            foreach (var id in _allocated)
            {
                if (!tree.TryGet(id, out var node))
                    continue;
                modifiers.AddRange(node.Modifiers);
                if (node.Type == TreeNodeType.Attribute && _attributes.TryGetValue(id, out var attribute))
                    modifiers.Add(new Modifier(PassiveTree.AttributeStat(attribute), ModifierKind.Flat, node.AttributeAmount));
                if (node.Type == TreeNodeType.Mastery && _masteries.TryGetValue(id, out var option) && option >= 0 && option < node.Options.Count)
                    modifiers.AddRange(node.Options[option]);
            }
            return modifiers;
        }

        private bool TouchesAllocated(PassiveTree tree, TreeNodeDef node)
        {
            foreach (var neighbour in node.Neighbours)
            {
                if (IsAllocated(tree, neighbour.Id))
                    return true;
            }
            return false;
        }

        private static AllocationResult CheckChoice(PassiveTree tree, TreeNodeDef node, string? attribute, int? masteryOption)
        {
            if (node.Type == TreeNodeType.Attribute)
            {
                if (attribute == null)
                    return AllocationResult.ChoiceRequired;
                foreach (var known in tree.Attributes)
                {
                    if (string.Equals(known, attribute, StringComparison.Ordinal))
                        return AllocationResult.Allocated;
                }
                return AllocationResult.BadChoice;
            }
            if (node.Type == TreeNodeType.Mastery)
            {
                if (!masteryOption.HasValue)
                    return AllocationResult.ChoiceRequired;
                return masteryOption.Value >= 0 && masteryOption.Value < node.Options.Count ? AllocationResult.Allocated : AllocationResult.BadChoice;
            }
            return attribute == null && !masteryOption.HasValue ? AllocationResult.Allocated : AllocationResult.BadChoice;
        }
    }
}
