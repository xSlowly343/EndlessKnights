using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Stats;
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
            var gem = GemsWithRequirement()["through_stride"];

            Assert.That(gem.Requires["strength"], Is.EqualTo(10));
        }

        private static GameLoop Swordsman() => TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });

        private static void AddStrength(GameLoop loop, double amount) =>
            loop.Hero.Add(new Modifier(HeroAttributes.Stat("strength"), ModifierKind.Flat, amount));

        [Test]
        public void GemNeedsEnoughAttributeFromTheTree()
        {
            var gem = GemsWithRequirement()["through_stride"];
            var loop = Swordsman();
            loop.State.OwnedGems.Add(gem);

            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            AddStrength(loop, 5);
            Assert.That(loop.Book.Attribute("strength"), Is.EqualTo(5));
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.AttributesTooLow));

            AddStrength(loop, 5);
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void ColoredSupportsAreLimitedByFifteenPointsOfTheirAttribute()
        {
            var loop = Swordsman();

            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(0));
            AddStrength(loop, 15);
            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(1));
            AddStrength(loop, 14);
            Assert.That(loop.Book.ColoredSupportLimit("strength"), Is.EqualTo(1));
            Assert.That(loop.Book.ColoredSupportLimit("dexterity"), Is.EqualTo(0));
        }
    }
}
