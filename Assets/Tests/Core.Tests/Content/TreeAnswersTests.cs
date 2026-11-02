using System.Linq;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class TreeAnswersTests
    {
        private static readonly string[] HoldLines = { "resourceHoldPause", "resourceDecaySlow", "resourceHoldOnce" };

        [Test]
        public void NoTreeLineWaitsForResourceHolding()
        {
            var data = GameData.Load(TestData.ReadGameDataFile);
            foreach (var tree in data.ClassTrees.Values.Concat(data.CommonTrees.Values))
            {
                var waits = tree.Nodes.SelectMany(node => new[] { node.Template.First, node.Template.Second, node.Template.Third }).Select(line => line.Waits);
                Assert.That(waits.Where(name => HoldLines.Contains(name)), Is.Empty, tree.Id);
            }
        }

        [Test]
        public void MajorTalentsMayChangeMinionsAndPassiveResource()
        {
            var avoid = TestWorld.Data.TreeTemplates.MajorTalentsAvoid;

            Assert.That(avoid.Intersect(new[] { "resourcePerSecond", "minionCount" }), Is.Empty);
        }
    }
}
