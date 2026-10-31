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

        [TestCase("ronin", "ronin_one_blade")]
        [TestCase("nomad", "nomad_best_at_nothing")]
        [TestCase("ronin", "ronin_thousand_steps")]
        [TestCase("viking", "viking_ancestor_shore")]
        public void FormerCompromiseVerticesHaveNoDownside(string heroClass, string nodeId)
        {
            var node = TestWorld.Data.ClassTreeOf(heroClass)!.Nodes.Single(candidate => candidate.Id == nodeId);
            for (var rank = 1; rank <= 5; rank++)
            {
                var sheet = new StatSheet();
                foreach (var modifier in node.ModifiersAt(rank))
                    sheet.Add(modifier);
                Assert.That(sheet.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.GreaterThanOrEqualTo(1), $"{nodeId} rank {rank}");
                Assert.That(sheet.Value(CombatStats.SkillCooldown, 1), Is.LessThanOrEqualTo(1), $"{nodeId} rank {rank}");
            }
        }

        [Test]
        public void MajorTalentsMayChangeMinionsAndPassiveResource()
        {
            var avoid = TestWorld.Data.TreeTemplates.MajorTalentsAvoid;

            Assert.That(avoid, Has.None.AnyOf("resourcePerSecond", "minionCount", "cannonCount"));
        }
    }
}
