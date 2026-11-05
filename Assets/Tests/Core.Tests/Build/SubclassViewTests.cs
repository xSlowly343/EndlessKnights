using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class SubclassViewTests
    {
        private static AscendancyCatalog Catalog => TestWorld.Data.Ascendancy;

        [Test]
        public void EveryPathHasABlurbAndAPathSkill()
        {
            Assert.That(Catalog.Subclasses.Where(subclass => subclass.BlurbRu.Length == 0 || subclass.BlurbEn.Length == 0).Select(subclass => subclass.Id), Is.Empty);
            Assert.That(Catalog.Subclasses.Where(subclass => SubclassView.PathSkill(TestWorld.Data.Gems, subclass) == null).Select(subclass => subclass.Id), Is.Empty);
        }

        [Test]
        public void LockedTextListsTheActivities()
        {
            Assert.That(SubclassView.Locked(Catalog), Does.Contain("первый страж биома"));
        }

        [Test]
        public void LevelTextShowsTheTraitLevelOutOfFive()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            loop.CompleteActivity("garden");
            loop.CompleteActivity("fishing");
            loop.ChooseSubclass("runedancer_trait_1");

            Assert.That(SubclassView.Level(loop.State.Ascendancy, Catalog), Does.Contain("2").And.Contain("5"));
        }
    }
}
