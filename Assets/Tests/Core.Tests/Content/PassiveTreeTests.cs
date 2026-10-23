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

        private static readonly string[] RingClasses =
        {
            "ronin", "metalhead", "bannerbearer", "oracle", "amazon", "stormweaver", "captain",
            "gravedigger", "clockmaker", "voodoo", "moonshiner", "plague_doctor", "viking"
        };
        private static readonly string[] Classes = RingClasses.Append("nomad").ToArray();

        [Test]
        public void TreeForFourteenCharactersHasAboutTwentySixHundredNodes()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.Nodes.Count, Is.InRange(2500, 2700));
            Assert.That(Count(tree, TreeNodeType.Start), Is.EqualTo(14));
            Assert.That(Count(tree, TreeNodeType.Keystone), Is.EqualTo(14 + 9));
            Assert.That(Count(tree, TreeNodeType.Notable), Is.GreaterThanOrEqualTo(250));
            Assert.That(Count(tree, TreeNodeType.Mastery), Is.GreaterThanOrEqualTo(80));
            Assert.That(Count(tree, TreeNodeType.Jewel), Is.GreaterThanOrEqualTo(30));
            Assert.That(Count(tree, TreeNodeType.Attribute), Is.GreaterThanOrEqualTo(250));
        }

        [Test]
        public void StartsStandOnOneRingInTheAgreedOrder()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree.StartOf("nomad")!.X, Is.EqualTo(0));
            Assert.That(tree.StartOf("nomad")!.Y, Is.EqualTo(0));
            for (var i = 0; i < RingClasses.Length; i++)
            {
                var start = tree.StartOf(RingClasses[i])!;
                var angle = System.Math.Atan2(start.Y, start.X) * 180 / System.Math.PI;
                var expected = 90 - i * 360.0 / 13;
                Assert.That(System.Math.Sqrt(start.X * start.X + start.Y * start.Y), Is.EqualTo(18).Within(0.01), start.Id);
                Assert.That((angle - expected + 720 + 180) % 360 - 180, Is.EqualTo(0).Within(0.1), start.Id);
            }
        }

        [TestCaseSource(nameof(Classes))]
        public void EveryCharacterHasAZoneOfAboutEightyNodes(string className)
        {
            var tree = PassiveTree.Load(RealFile());
            var zone = tree.Nodes.Where(node => node.ScopeClass == className).ToList();

            Assert.That(zone.Count, Is.InRange(70, 90));
            Assert.That(zone.Count(node => node.Type == TreeNodeType.Start), Is.EqualTo(1));
            Assert.That(zone.Count(node => node.Type == TreeNodeType.Notable), Is.EqualTo(10));
            Assert.That(zone.Count(node => node.Type == TreeNodeType.Mastery), Is.EqualTo(4));
            Assert.That(zone.Count(node => node.Type == TreeNodeType.Keystone), Is.EqualTo(1));
            Assert.That(zone.Count(node => node.Type == TreeNodeType.Jewel), Is.EqualTo(2));
            Assert.That(zone.Count(node => node.Type == TreeNodeType.Attribute), Is.EqualTo(8));
        }

        [Test]
        public void SevenRoleClustersOfAboutThirtyNodes()
        {
            var tree = PassiveTree.Load(RealFile());
            var roles = tree.Nodes.Where(node => node.Scope.StartsWith("role:")).ToList();

            Assert.That(roles.Count(node => node.Type == TreeNodeType.Mastery), Is.EqualTo(7));
            Assert.That(roles.Count, Is.InRange(7 * 28, 7 * 34));
            Assert.That(roles.Select(node => node.Scope).Distinct(), Is.EquivalentTo(new[] { "role:dd", "role:buffer", "role:debuffer", "role:dot", "role:summoner" }));
        }

        [Test]
        public void ZoneNodesAreDualAndOtherNodesAreTheSameForEveryone()
        {
            var tree = PassiveTree.Load(RealFile());

            foreach (var node in tree.Nodes)
            {
                var hasEffect = node.Modifiers.Count > 0 || node.Options.Count > 0;
                if (node.IsDual && hasEffect)
                    Assert.That(node.SharedModifiers.Count > 0 || node.SharedOptions.Count == node.Options.Count && node.Options.Count > 0, Is.True, node.Id);
                if (!node.IsDual)
                    Assert.That(node.SharedModifiers.Count + node.SharedOptions.Count, Is.EqualTo(0), node.Id);
            }
            Assert.That(tree.Nodes.Count(node => node.Scope == "sector:strength"), Is.GreaterThan(tree.Nodes.Count(node => node.Scope == "sector:dexterity")));
            Assert.That(tree.Nodes.Count(node => node.Scope == TreeNodeDef.CommonScope), Is.GreaterThanOrEqualTo(60));
        }

        [Test]
        public void VikingZoneNodeGivesTheVikingItsOwnEffectAndTheRoninTheSharedOne()
        {
            var tree = PassiveTree.Load(RealFile());
            var node = tree["viking_resource_0"];

            Assert.That(node.ModifiersFor("viking"), Is.EqualTo(new[] { new Modifier(new StatId("resourceGain"), ModifierKind.Increased, 0.06) }));
            Assert.That(node.ModifiersFor("ronin"), Is.EqualTo(new[] { new Modifier(new StatId("resourceGain"), ModifierKind.Increased, 0.02) }));
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
        public void ClassKeystonesAndMasteriesKeepTheirShape()
        {
            var tree = PassiveTree.Load(RealFile());

            Assert.That(tree["metalhead_keystone"].Modifiers, Does.Contain(new Modifier(new StatId("auraEffect"), ModifierKind.More, 1.0, StatTags.Aura)));
            Assert.That(tree["ronin_resource_mastery"].Options.Count, Is.EqualTo(3));
            Assert.That(tree["ronin_resource_mastery"].SharedOptions.Count, Is.EqualTo(3));
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
        public void NomadReachesEveryClassRingInAFewSteps()
        {
            var tree = PassiveTree.Load(RealFile());
            var wanderer = tree.StartOf("nomad")!;
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
                Assert.That(depth[tree[className + "_ring_3"]], Is.LessThanOrEqualTo(20), className);
        }

        [Test]
        public void NomadStartSitsInsideAnInnerRing()
        {
            var tree = PassiveTree.Load(RealFile());
            var wanderer = tree.StartOf("nomad")!;

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

        [TestCase("[\"ronin_start\", \"ronin_ring_0\"]", "[\"ronin_start\", \"nowhere\"]", "links[", "unknown node 'nowhere'")]
        [TestCase("[\"ronin_start\", \"ronin_ring_0\"]", "[\"ronin_start\", \"ronin_start\"]", "links[", "linked to itself")]
        [TestCase("[\"ronin_start\", \"ronin_ring_0\"]", "[\"ronin_ring_0\", \"ronin_start\"], [\"ronin_start\", \"ronin_ring_0\"]", "links[", "is listed twice")]
        [TestCase("\"scope\": \"class:metalhead\", \"modifiers\": [{\"stat\": \"auraEffect\", \"kind\": \"increased\", \"value\": 0.1, \"tags\": [\"Aura\"]}, {\"stat\": \"auraDuration\", \"kind\": \"increased\", \"value\": 0.1}], \"class\": \"metalhead\"",
            "\"scope\": \"class:ronin\", \"class\": \"ronin\"", "nodes[", "class 'ronin' has two start nodes")]
        [TestCase("\"class\": \"metalhead\"", "\"class\": \"ronin\"", "nodes[", "a start belongs to its own class, expected 'class:ronin'")]
        [TestCase("\"scope\": \"role:dd\"", "\"scope\": \"role:tank\"", "nodes[", "unknown scope 'role:tank'")]
        [TestCase("\"scope\": \"sector:strength\"", "\"scope\": \"sector:luck\"", "nodes[", "unknown scope 'sector:luck'")]
        [TestCase("\"version\": 2", "\"version\": 1", "version", "version 1 is not supported, expected 2")]
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
            var cut = System.Text.RegularExpressions.Regex.Replace(RealFile(), "    \\[\"[a-z_0-9]+\", \"ronin_keystone\"\\],\\r?\n", "");
            var error = Assert.Throws<ContentException>(() => PassiveTree.Load(cut))!;

            Assert.That(error.Message, Does.Contain("node 'ronin_keystone' cannot be reached"));
        }
    }
}
