using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class PassiveTreeTests
    {
        private static string RealFile() => TestData.ReadGameDataFile(PassiveTree.FileName);

        private static int Count(PassiveTree tree, TreeNodeType type) => tree.Nodes.Count(node => node.Type == type);

        [Test]
        public void MvpTreeHasSeventyTwoNodesOfEveryType()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.Nodes.Count, Is.EqualTo(72));
            Assert.That(Count(tree, TreeNodeType.Start), Is.EqualTo(3));
            Assert.That(Count(tree, TreeNodeType.Keystone), Is.EqualTo(2));
            Assert.That(Count(tree, TreeNodeType.Notable), Is.EqualTo(8));
            Assert.That(Count(tree, TreeNodeType.Mastery), Is.EqualTo(2));
            Assert.That(Count(tree, TreeNodeType.Jewel), Is.EqualTo(2));
            Assert.That(Count(tree, TreeNodeType.Attribute), Is.GreaterThanOrEqualTo(9));
            Assert.That(Count(tree, TreeNodeType.Small), Is.GreaterThan(30));
        }

        [Test]
        public void StartsExistForTypistBardAndHermit()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.StartOf("typist")!.Id, Is.EqualTo("typist_start"));
            Assert.That(tree.StartOf("bard")!.Id, Is.EqualTo("bard_start"));
            Assert.That(tree.StartOf("hermit")!.Id, Is.EqualTo("hermit_start"));
            Assert.That(tree.StartOf("whirlwind"), Is.Null);
        }

        [Test]
        public void JewelSocketsAreVisibleButLocked()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.Nodes.Where(node => node.Type == TreeNodeType.Jewel).All(node => node.Locked), Is.True);
            Assert.That(tree.Nodes.Where(node => node.Type != TreeNodeType.Jewel).Any(node => node.Locked), Is.False);
        }

        [Test]
        public void KeystonesAndMasteriesMatchContentDraft()
        {
            var tree = PassiveTree.Load(RealFile());

            var oneHanded = tree["one_handed"];
            Assert.That(oneHanded.Type, Is.EqualTo(TreeNodeType.Keystone));
            Assert.That(oneHanded.Modifiers, Does.Contain(new Modifier(new StatId("damage"), ModifierKind.More, 1.0, StatTags.Keyboard)));
            Assert.That(oneHanded.Modifiers, Does.Contain(new Modifier(new StatId("damage"), ModifierKind.More, -1.0, StatTags.Mouse)));
            Assert.That(tree["pen_mastery"].Options.Count, Is.EqualTo(3));
            Assert.That(tree["melody_mastery"].Options.Count, Is.EqualTo(3));
            Assert.That(tree["only_music"].Type, Is.EqualTo(TreeNodeType.Keystone));
        }

        [TestCase("typist")]
        [TestCase("bard")]
        [TestCase("hermit")]
        public void EveryClassReachesEveryNodeWithoutCrossingAnotherStart(string className)
        {
            var tree = PassiveTree.Load(RealFile());
            var start = tree.StartOf(className)!;
            var reached = new System.Collections.Generic.HashSet<TreeNodeDef> { start };
            var queue = new System.Collections.Generic.Queue<TreeNodeDef>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                foreach (var next in queue.Dequeue().Neighbours)
                {
                    if (next.Type == TreeNodeType.Start || next.Locked || !reached.Add(next))
                        continue;
                    queue.Enqueue(next);
                }
            }

            var missing = tree.Nodes.Where(node => node.Type != TreeNodeType.Start && !node.Locked && !reached.Contains(node)).Select(node => node.Id);
            Assert.That(missing, Is.Empty);
        }

        [TestCase("typist")]
        [TestCase("bard")]
        [TestCase("hermit")]
        public void RoadsRunAroundEveryStart(string className)
        {
            var tree = PassiveTree.Load(RealFile());
            var start = tree.StartOf(className)!;
            var around = start.Neighbours.ToList();
            var reached = new System.Collections.Generic.HashSet<TreeNodeDef> { around[0] };
            var queue = new System.Collections.Generic.Queue<TreeNodeDef>();
            queue.Enqueue(around[0]);
            while (queue.Count > 0)
            {
                foreach (var next in queue.Dequeue().Neighbours)
                {
                    if (around.Contains(next) && reached.Add(next))
                        queue.Enqueue(next);
                }
            }

            Assert.That(reached, Is.EquivalentTo(around), "every exit of a start is linked to the others without the start itself");
        }

        [TestCase("typist")]
        [TestCase("bard")]
        public void NoRoadFromTheCentreRunsIntoAClassStart(string className)
        {
            var tree = PassiveTree.Load(RealFile());
            var start = tree.StartOf(className)!;
            var startRadius = System.Math.Sqrt(start.X * start.X + start.Y * start.Y);

            foreach (var exit in start.Neighbours)
                Assert.That(System.Math.Sqrt(exit.X * exit.X + exit.Y * exit.Y), Is.GreaterThan(startRadius), exit.Id);
        }

        [Test]
        public void HermitReachesTheOtherSectorsInAFewSteps()
        {
            var tree = PassiveTree.Load(RealFile());
            var hermit = tree.StartOf("hermit")!;
            var depth = new System.Collections.Generic.Dictionary<TreeNodeDef, int> { [hermit] = 0 };
            var queue = new System.Collections.Generic.Queue<TreeNodeDef>();
            queue.Enqueue(hermit);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var next in node.Neighbours)
                {
                    if (next.Type == TreeNodeType.Start || depth.ContainsKey(next))
                        continue;
                    depth[next] = depth[node] + 1;
                    queue.Enqueue(next);
                }
            }

            foreach (var exit in new[] { "t_pen_1", "t_combo_1", "t_heavy_1", "b_attr_3", "b_ensemble_1" })
                Assert.That(depth[tree[exit]], Is.LessThanOrEqualTo(4), exit);
        }

        [Test]
        public void HermitStartSitsInsideAnInnerRing()
        {
            var tree = PassiveTree.Load(RealFile());
            var hermit = tree.StartOf("hermit")!;

            Assert.That(hermit.Neighbours.Count, Is.EqualTo(5));
            foreach (var ringNode in hermit.Neighbours)
                Assert.That(ringNode.Neighbours.Count(n => hermit.Neighbours.Contains(n)), Is.EqualTo(2), ringNode.Id);
        }

        [Test]
        public void LinksGoBothWays()
        {
            var tree = PassiveTree.Load(RealFile());

            foreach (var node in tree.Nodes)
            {
                Assert.That(node.Neighbours, Is.Not.Empty, node.Id);
                foreach (var neighbour in node.Neighbours)
                    Assert.That(neighbour.Neighbours, Does.Contain(node), $"{node.Id} — {neighbour.Id}");
            }
        }

        [Test]
        public void AttributeNodesGiveFivePoints()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.Nodes.Where(node => node.Type == TreeNodeType.Attribute).All(node => node.AttributeAmount == 5), Is.True);
            Assert.That(tree.Attributes, Is.EqualTo(new[] { "tempo", "reach", "precision" }));
        }

        [TestCase("[\"t_pen_1\", \"t_pen_2\"]", "[\"t_pen_1\", \"nowhere\"]", "links[", "unknown node 'nowhere'")]
        [TestCase("[\"t_pen_1\", \"t_pen_2\"]", "[\"t_pen_1\", \"t_pen_1\"]", "links[", "linked to itself")]
        [TestCase("[\"t_pen_1\", \"t_pen_2\"]", "[\"t_pen_2\", \"t_pen_1\"], [\"t_pen_1\", \"t_pen_2\"]", "links[", "is listed twice")]
        [TestCase("\"class\": \"bard\"", "\"class\": \"typist\"", "nodes[", "class 'typist' has two start nodes")]
        [TestCase("\"type\": \"keystone\"", "\"type\": \"legendary\"", "nodes[", "unknown node type 'legendary'")]
        public void BrokenTreeIsReportedByPath(string original, string broken, string path, string message)
        {
            var error = Assert.Throws<ContentException>(() => PassiveTree.Load(RealFile().Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain("tree.json: " + path));
            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void TreeMustBeOnePiece()
        {
            var error = Assert.Throws<ContentException>(() => PassiveTree.Load(RealFile().Replace("    [\"t_combo_small\", \"one_handed\"],\n", "")))!;

            Assert.That(error.Message, Does.Contain("node 'one_handed' cannot be reached"));
        }
    }
}
