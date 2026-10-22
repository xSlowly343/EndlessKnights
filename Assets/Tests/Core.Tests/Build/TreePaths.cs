using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public static class TreePaths
    {
        public static string[] To(PassiveTree tree, string className, string target)
        {
            var start = tree.StartOf(className)!;
            var previous = new Dictionary<TreeNodeDef, TreeNodeDef> { [start] = start };
            var queue = new Queue<TreeNodeDef>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node.Id == target)
                    break;
                foreach (var next in node.Neighbours)
                {
                    if (previous.ContainsKey(next) || next.Type == TreeNodeType.Start || (next.Locked && next.Id != target))
                        continue;
                    previous[next] = node;
                    queue.Enqueue(next);
                }
            }
            var path = new List<string>();
            for (var node = tree[target]; node != start; node = previous[node])
                path.Add(node.Id);
            path.Reverse();
            return path.ToArray();
        }

        public static string NearestOfType(PassiveTree tree, string className, TreeNodeType type) =>
            tree.Nodes.Where(node => node.Type == type).OrderBy(node => To(tree, className, node.Id).Length).ThenBy(node => node.Id).First().Id;

        public static void Allocate(GameLoop loop, PassiveTree tree, IEnumerable<string> path, string attribute = "strength")
        {
            foreach (var id in path)
            {
                if (loop.State.Tree.IsAllocated(tree, id))
                    continue;
                var result = loop.AllocateNode(id, attribute: tree[id].Type == TreeNodeType.Attribute ? attribute : null);
                Assert.That(result, Is.EqualTo(AllocationResult.Allocated), id);
            }
        }

        public static string[] SubclassPath(SubclassDef subclass, string target)
        {
            var previous = new Dictionary<TreeNodeDef, TreeNodeDef> { [subclass.Start] = subclass.Start };
            var queue = new Queue<TreeNodeDef>();
            queue.Enqueue(subclass.Start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var next in node.Neighbours)
                {
                    if (previous.ContainsKey(next))
                        continue;
                    previous[next] = node;
                    queue.Enqueue(next);
                }
            }
            subclass.TryGet(target, out var end);
            var path = new List<string>();
            for (var node = end; node != subclass.Start; node = previous[node])
                path.Add(node.Id);
            path.Reverse();
            return path.ToArray();
        }
    }
}
