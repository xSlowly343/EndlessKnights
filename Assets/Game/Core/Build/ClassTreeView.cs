using System.Collections.Generic;
using Game.Core.Content.Tree;

namespace Game.Core.Build
{
    public readonly struct NextRank
    {
        public NextRank(int rank, RankLine line, bool strengthens)
        {
            Rank = rank;
            Line = line;
            Strengthens = strengthens;
        }

        public int Rank { get; }
        public RankLine Line { get; }
        public bool Strengthens { get; }
    }

    public static class ClassTreeView
    {
        public static HashSet<string> Chain(ClassTree tree, string nodeId)
        {
            var chain = new HashSet<string>();
            if (!tree.TryGet(nodeId, out var node))
                return chain;
            for (var current = node; current != null; current = current.Parent)
                chain.Add(current.Id);
            var grew = true;
            while (grew)
            {
                grew = false;
                foreach (var other in tree.Nodes)
                {
                    if (other.Parent != null && !chain.Contains(other.Id) && IsBelowInChain(other.Parent, node, chain))
                    {
                        chain.Add(other.Id);
                        grew = true;
                    }
                }
            }
            return chain;
        }

        public static NextRank? Next(ClassTree tree, ClassTreeAllocation allocation, string nodeId)
        {
            if (!tree.TryGet(nodeId, out var node))
                return null;
            var rank = allocation.RankOf(nodeId) + 1;
            if (rank > TreeTemplate.Ranks)
                return null;
            var template = node.Template;
            return rank switch
            {
                3 => new NextRank(rank, template.Second, false),
                5 => new NextRank(rank, template.Third, false),
                _ => new NextRank(rank, template.First, rank > 1)
            };
        }

        private static bool IsBelowInChain(ClassTreeNode parent, ClassTreeNode hovered, HashSet<string> chain)
        {
            if (parent == hovered)
                return true;
            if (!chain.Contains(parent.Id))
                return false;
            for (var current = hovered.Parent; current != null; current = current.Parent)
            {
                if (current == parent)
                    return false;
            }
            return true;
        }
    }
}
