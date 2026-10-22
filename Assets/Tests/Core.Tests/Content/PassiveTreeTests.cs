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
            var error = Assert.Throws<ContentException>(() => PassiveTree.Load(RealFile().Replace("    [\"unstoppable\", \"one_handed\"],\n", "")))!;

            Assert.That(error.Message, Does.Contain("node 'one_handed' cannot be reached"));
        }
    }
}
