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

        private static readonly string[] Classes = { "swordsman", "bard", "barbarian", "ranger", "sorcerer", "witch", "priest", "alchemist", "wanderer" };
        private static readonly string[] RingClasses = { "swordsman", "bard", "barbarian", "ranger", "sorcerer", "witch", "priest", "alchemist" };

        [Test]
        public void LargeTreeHasHundredsOfNodesOfEveryType()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.Nodes.Count, Is.GreaterThan(1500));
            Assert.That(Count(tree, TreeNodeType.Start), Is.EqualTo(9));
            Assert.That(Count(tree, TreeNodeType.Keystone), Is.EqualTo(24));
            Assert.That(Count(tree, TreeNodeType.Notable), Is.GreaterThanOrEqualTo(150));
            Assert.That(Count(tree, TreeNodeType.Mastery), Is.GreaterThanOrEqualTo(100));
            Assert.That(Count(tree, TreeNodeType.Jewel), Is.EqualTo(24));
            Assert.That(Count(tree, TreeNodeType.Attribute), Is.GreaterThanOrEqualTo(150));
        }

        [Test]
        public void EveryClassHasItsStart()
        {
            var tree = PassiveTree.Load(RealFile());

            foreach (var className in Classes)
                Assert.That(tree.StartOf(className)!.Id, Is.EqualTo(className + "_start"));
            Assert.That(tree.StartOf("typist"), Is.Null);
        }

        [Test]
        public void JewelSocketsAreVisibleButLocked()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.Nodes.Where(node => node.Type == TreeNodeType.Jewel).All(node => node.Locked), Is.True);
            Assert.That(tree.Nodes.Where(node => node.Type != TreeNodeType.Jewel).Any(node => node.Locked), Is.False);
        }

        [Test]
        public void EveryRingClassHasTwoKeystonesAndFourMasteries()
        {
            var tree = PassiveTree.Load(RealFile());

            foreach (var className in RingClasses)
            {
                Assert.That(tree.Nodes.Count(node => node.Type == TreeNodeType.Keystone && node.Id.StartsWith(className + "_")), Is.EqualTo(2), className);
                Assert.That(tree.Nodes.Count(node => node.Type == TreeNodeType.Mastery && node.Id.StartsWith(className + "_")), Is.EqualTo(4), className);
            }
            Assert.That(tree["bard_keystone_1"].Modifiers, Does.Contain(new Modifier(new StatId("auraEffect"), ModifierKind.More, 1.0)));
            Assert.That(tree["swordsman_w1_mastery"].Options.Count, Is.EqualTo(3));
        }

        [Test]
        public void NoNodeGivesExperience()
        {
            var tree = PassiveTree.Load(RealFile());

            var modifiers = tree.Nodes.SelectMany(node => node.Modifiers.Concat(node.Options.SelectMany(option => option)));
            Assert.That(modifiers.Any(modifier => modifier.Stat.Name == "heroXp"), Is.False);
        }

        [TestCaseSource(nameof(Classes))]
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

        [TestCaseSource(nameof(Classes))]
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

        [TestCaseSource(nameof(RingClasses))]
        public void NoRoadFromTheCentreRunsIntoAClassStart(string className)
        {
            var tree = PassiveTree.Load(RealFile());
            var start = tree.StartOf(className)!;
            var startRadius = System.Math.Sqrt(start.X * start.X + start.Y * start.Y);

            foreach (var exit in start.Neighbours)
                Assert.That(System.Math.Sqrt(exit.X * exit.X + exit.Y * exit.Y), Is.GreaterThan(startRadius), exit.Id);
        }

        [Test]
        public void WandererReachesEveryClassRingInAFewSteps()
        {
            var tree = PassiveTree.Load(RealFile());
            var wanderer = tree.StartOf("wanderer")!;
            var depth = new System.Collections.Generic.Dictionary<TreeNodeDef, int> { [wanderer] = 0 };
            var queue = new System.Collections.Generic.Queue<TreeNodeDef>();
            queue.Enqueue(wanderer);
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

            foreach (var className in RingClasses)
                Assert.That(depth[tree[className + "_ring_3"]], Is.LessThanOrEqualTo(14), className);
        }

        [Test]
        public void WandererStartSitsInsideAnInnerRing()
        {
            var tree = PassiveTree.Load(RealFile());
            var wanderer = tree.StartOf("wanderer")!;

            Assert.That(wanderer.Neighbours.Count, Is.EqualTo(6));
            foreach (var ringNode in wanderer.Neighbours)
                Assert.That(ringNode.Neighbours.Count(n => wanderer.Neighbours.Contains(n)), Is.EqualTo(2), ringNode.Id);
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
            Assert.That(tree.Attributes, Is.EqualTo(new[] { "strength", "dexterity", "intelligence" }));
        }

        [TestCase("[\"swordsman_start\", \"swordsman_ring_0\"]", "[\"swordsman_start\", \"nowhere\"]", "links[", "unknown node 'nowhere'")]
        [TestCase("[\"swordsman_start\", \"swordsman_ring_0\"]", "[\"swordsman_start\", \"swordsman_start\"]", "links[", "linked to itself")]
        [TestCase("[\"swordsman_start\", \"swordsman_ring_0\"]", "[\"swordsman_ring_0\", \"swordsman_start\"], [\"swordsman_start\", \"swordsman_ring_0\"]", "links[", "is listed twice")]
        [TestCase("\"class\": \"bard\"", "\"class\": \"swordsman\"", "nodes[", "class 'swordsman' has two start nodes")]
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
            var cut = System.Text.RegularExpressions.Regex.Replace(RealFile(), "    \\[\"[a-z_0-9]+\", \"swordsman_keystone_1\"\\],\\r?\n", "");
            var error = Assert.Throws<ContentException>(() => PassiveTree.Load(cut))!;

            Assert.That(error.Message, Does.Contain("node 'swordsman_keystone_1' cannot be reached"));
        }
    }
}
