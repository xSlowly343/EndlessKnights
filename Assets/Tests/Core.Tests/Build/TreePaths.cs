using System.Collections.Generic;
using Game.Core.Content.Tree;

namespace Game.Core.Tests.Build
{
    public static class TreePaths
    {
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
