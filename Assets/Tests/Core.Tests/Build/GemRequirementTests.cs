using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class GemRequirementTests
    {
        [Test]
        public void SkillsJsonRejectsAttributeRequirements()
        {
            var json = TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"tags\": [\"Keyboard\", \"Skill\"],", "\"tags\": [\"Keyboard\", \"Skill\"],\n      \"requires\": { \"strength\": 10 },");

            Assert.Throws<ContentException>(() => GemCatalog.Load(json));
        }

        [Test]
        public void ASkillNeedsNoAttribute()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
            var gem = TestWorld.Data.Gems["through_stride"];
            loop.State.OwnedGems.Add(gem);

            Assert.That(loop.Book.ClassAttribute, Is.EqualTo(0));
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.Done).Or.EqualTo(BookResult.AlreadyInBook));
        }
    }
}
