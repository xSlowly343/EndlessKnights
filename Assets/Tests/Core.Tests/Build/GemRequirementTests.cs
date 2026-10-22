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
            GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"tags\": [\"Keyboard\", \"Skill\"],", "\"tags\": [\"Keyboard\", \"Skill\"],\n      \"requires\": { \"tempo\": 10 },"));

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

            Assert.That(gem.Requires["tempo"], Is.EqualTo(10));
        }

        [Test]
        public void GemNeedsEnoughAttributeFromTheTree()
        {
            var gem = GemsWithRequirement()["stenography"];
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Typist });
            loop.State.Tree.PointsEarned = 10;
            loop.State.OwnedGems.Add(gem);

            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            loop.AllocateNode("t_pen_1");
            loop.AllocateNode("t_attr_1", attribute: "tempo");
            Assert.That(loop.Book.Attribute("tempo"), Is.EqualTo(5));
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            loop.AllocateNode("t_pen_2");
            loop.AllocateNode("t_attr_2", attribute: "tempo");
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void ColoredSupportsAreLimitedByFifteenPointsOfTheirAttribute()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Typist });
            loop.State.Tree.PointsEarned = 10;

            Assert.That(loop.Book.ColoredSupportLimit("tempo"), Is.EqualTo(0));
            loop.AllocateNode("t_pen_1");
            loop.AllocateNode("t_attr_1", attribute: "tempo");
            loop.AllocateNode("t_pen_2");
            loop.AllocateNode("t_attr_2", attribute: "tempo");
            loop.AllocateNode("t_heavy_1");
            loop.AllocateNode("t_heavy_2");
            loop.AllocateNode("t_attr_3", attribute: "tempo");
            Assert.That(loop.Book.ColoredSupportLimit("tempo"), Is.EqualTo(1));
            Assert.That(loop.Book.ColoredSupportLimit("reach"), Is.EqualTo(0));
        }
    }
}
