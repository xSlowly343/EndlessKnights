using System.Linq;
using Game.Core.Content.Tree;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class TreeGlyphTests
    {
        [Test]
        public void EveryStatInTheTreeHasItsOwnGlyph()
        {
            var tree = TestWorld.Real.Tree;
            var stats = tree.Nodes
                .SelectMany(node => node.Modifiers.Concat(node.SharedModifiers).Concat(node.Options.SelectMany(option => option)).Concat(node.SharedOptions.SelectMany(option => option)))
                .Select(modifier => modifier.Stat)
                .Distinct()
                .ToList();

            var unmapped = stats.Where(stat => !TreeGlyph.TryOfStat(stat, out _)).Select(stat => stat.Name).ToList();

            Assert.That(unmapped, Is.Empty);
        }

        [Test]
        public void EveryNodeExceptSocketsHasAGlyph()
        {
            var tree = TestWorld.Real.Tree;

            foreach (var node in tree.Nodes.Where(node => node.Type != TreeNodeType.Jewel))
                Assert.That(TreeGlyph.Of(node), Is.Not.Empty, node.Id);
        }

        [Test]
        public void StartsAndKeystonesEachHaveTheirOwnGlyph()
        {
            var tree = TestWorld.Real.Tree;

            foreach (var type in new[] { TreeNodeType.Start, TreeNodeType.Keystone })
            {
                var glyphs = tree.Nodes.Where(node => node.Type == type).Select(TreeGlyph.Of).ToList();
                Assert.That(glyphs.Distinct().Count(), Is.EqualTo(glyphs.Count), type.ToString());
            }
        }
    }
}
