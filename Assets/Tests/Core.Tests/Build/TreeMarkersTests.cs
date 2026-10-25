using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class TreeMarkersTests
    {
        [Test]
        public void OfTwoCrowdedNodesTheHigherPriorityOneStays()
        {
            var markers = new TreeMarkers(new[] { Node("a", 0, 0), Node("b", 0.3, 0) });

            markers.Pick(new[] { 0.2, 0.2 }, new[] { 0, 5 }, 0.0);

            Assert.That(markers.IsShown(0), Is.False);
            Assert.That(markers.IsShown(1), Is.True);
        }

        [Test]
        public void OnEqualPriorityTheEarlierNodeStays()
        {
            var markers = new TreeMarkers(new[] { Node("a", 0, 0), Node("b", 0.3, 0) });

            markers.Pick(new[] { 0.2, 0.2 }, new[] { 3, 3 }, 0.0);

            Assert.That(markers.IsShown(0), Is.True);
            Assert.That(markers.IsShown(1), Is.False);
        }

        [Test]
        public void NodesFarEnoughApartAreAllShown()
        {
            var markers = new TreeMarkers(new[] { Node("a", 0, 0), Node("b", 0.5, 0), Node("c", 0, -0.5) });

            markers.Pick(new[] { 0.2, 0.2, 0.2 }, new[] { 0, 0, 0 }, 0.05);

            Assert.That(Enumerable.Range(0, 3).All(markers.IsShown), Is.True);
        }

        [Test]
        public void StartBeatsOfferedAndOfferedBeatsKeystone()
        {
            var offered = TreeMarkers.PriorityOf(TreeNodeType.Small, true, false);
            var keystone = TreeMarkers.PriorityOf(TreeNodeType.Keystone, false, true);

            Assert.That(TreeMarkers.PriorityOf(TreeNodeType.Start, false, false), Is.GreaterThan(offered));
            Assert.That(offered, Is.GreaterThan(keystone));
            Assert.That(keystone, Is.GreaterThan(TreeMarkers.PriorityOf(TreeNodeType.Notable, false, true)));
        }

        [Test]
        public void AtWorldSizeEveryRealNodeIsShown()
        {
            var nodes = TestWorld.Real.Tree.Nodes;
            var markers = new TreeMarkers(nodes);

            markers.Pick(nodes.Select(node => WorldHalf(node.Type)).ToArray(), new int[nodes.Count], 0.0);

            Assert.That(Enumerable.Range(0, nodes.Count).Count(i => !markers.IsShown(i)), Is.Zero);
        }

        [Test]
        public void FullyZoomedOutTheShownMarkersDoNotOverlapAndCoverTheTree()
        {
            var nodes = TestWorld.Real.Tree.Nodes;
            var markers = new TreeMarkers(nodes);
            const double pixelsPerUnit = 4.0;
            var halves = nodes.Select(node => 6.0 / pixelsPerUnit).ToArray();
            var priorities = nodes.Select(node => TreeMarkers.PriorityOf(node.Type, false, false)).ToArray();

            markers.Pick(halves, priorities, 0.0);

            var shown = Enumerable.Range(0, nodes.Count).Where(markers.IsShown).ToList();
            for (var a = 0; a < shown.Count; a++)
                for (var b = a + 1; b < shown.Count; b++)
                    Assert.That(Distance(nodes[shown[a]], nodes[shown[b]]), Is.GreaterThanOrEqualTo(3.0 - 1e-9));
            foreach (var node in nodes)
                Assert.That(shown.Min(i => Distance(node, nodes[i])), Is.LessThan(3.0), node.Id);
            Assert.That(nodes.Where(node => node.Type == TreeNodeType.Start).All(node => markers.IsShown(IndexOf(nodes, node))), Is.True);
        }

        private static double WorldHalf(TreeNodeType type) => type switch
        {
            TreeNodeType.Notable => 0.3,
            TreeNodeType.Keystone => 0.5,
            TreeNodeType.Mastery => 0.34,
            TreeNodeType.Jewel => 0.3,
            TreeNodeType.Start => 0.9,
            _ => 0.2
        };

        private static double Distance(TreeNodeDef a, TreeNodeDef b) => System.Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static int IndexOf(IReadOnlyList<TreeNodeDef> nodes, TreeNodeDef node)
        {
            for (var i = 0; i < nodes.Count; i++)
                if (ReferenceEquals(nodes[i], node))
                    return i;
            return -1;
        }

        private static TreeNodeDef Node(string id, double x, double y) =>
            new TreeNodeDef(id, TreeNodeType.Small, x, y, id, id, System.Array.Empty<Modifier>(), System.Array.Empty<IReadOnlyList<Modifier>>(), 0, null, false);
    }
}
