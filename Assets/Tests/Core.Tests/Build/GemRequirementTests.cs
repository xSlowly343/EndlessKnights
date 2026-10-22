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

        private static GameLoop Swordsman()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Swordsman });
            loop.State.Tree.PointsEarned = 20;
            foreach (var id in TreeAllocationTests.ToAttribute)
                Assert.That(loop.AllocateNode(id), Is.EqualTo(AllocationResult.Allocated), id);
            return loop;
        }

        [Test]
        public void GemNeedsEnoughAttributeFromTheTree()
        {
            var gem = GemsWithRequirement()["stenography"];
            var loop = Swordsman();
            loop.State.OwnedGems.Add(gem);

            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            loop.AllocateNode("great_2", attribute: "strength");
            Assert.That(loop.Book.Attribute("strength"), Is.EqualTo(5));
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            loop.AllocateNode("great_1");
            loop.AllocateNode("great_0");
            loop.AllocateNode("great_31");
            loop.AllocateNode("great_30", attribute: "strength");
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void ColoredSupportsAreLimitedByFifteenPointsOfTheirAttribute()
        {
            var loop = Swordsman();

            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(0));
            loop.AllocateNode("great_2", attribute: "strength");
            foreach (var id in new[] { "great_1", "great_0", "great_31" })
                loop.AllocateNode(id);
            loop.AllocateNode("great_30", attribute: "strength");
            foreach (var id in new[] { "great_5", "great_6" })
                loop.AllocateNode(id, attribute: id == "great_6" ? "strength" : null);
            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(1));
            Assert.That(loop.Book.ColoredSupportLimit("dexterity"), Is.EqualTo(0));
        }
    }
}
