using Game.Core.Content.Tree;
using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class GemRequirementTests
    {
        private static GemCatalog GemsWithRequirement() =>
            GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"tags\": [\"Keyboard\", \"Skill\"],", "\"tags\": [\"Keyboard\", \"Skill\"],\n      \"requires\": { \"strength\": 10 },"));

        [Test]
        public void MvpGemsHaveNoRequirementsSoStartingGemsAlwaysFit()
        {
            foreach (var gem in TestWorld.Data.Gems.All)
                Assert.That(gem.Requires, Is.Empty, gem.Id);
        }

        [Test]
        public void RequirementIsReadFromGemsJson()
        {
            var gem = GemsWithRequirement()["stenography"];

            Assert.That(gem.Requires["strength"], Is.EqualTo(10));
        }

        private static PassiveTree Tree => TestWorld.Data.Tree;

        private static GameLoop Swordsman()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
            loop.State.Tree.PointsEarned = 120;
            return loop;
        }

        private static void TakeAttributes(GameLoop loop, int count)
        {
            var nearest = Tree.Nodes.Where(node => node.Type == TreeNodeType.Attribute)
                .OrderBy(node => TreePaths.To(Tree, "ronin", node.Id).Length).ThenBy(node => node.Id).Take(count);
            foreach (var attribute in nearest)
                TreePaths.Allocate(loop, Tree, TreePaths.To(Tree, "ronin", attribute.Id));
        }

        private static int Taken(GameLoop loop) => loop.State.Tree.AttributeChoices.Count;

        [Test]
        public void GemNeedsEnoughAttributeFromTheTree()
        {
            var gem = GemsWithRequirement()["stenography"];
            var loop = Swordsman();
            loop.State.OwnedGems.Add(gem);

            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            TakeAttributes(loop, 1);
            Assert.That(loop.Book.Attribute("strength"), Is.EqualTo(5 * Taken(loop)));
            if (Taken(loop) < 2)
                Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            TakeAttributes(loop, 2);
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void ColoredSupportsAreLimitedByFifteenPointsOfTheirAttribute()
        {
            var loop = Swordsman();

            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(0));
            TakeAttributes(loop, 3);
            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(Taken(loop) * 5 / 15));
            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.GreaterThanOrEqualTo(1));
            Assert.That(loop.Book.ColoredSupportLimit("dexterity"), Is.EqualTo(0));
        }
    }
}
