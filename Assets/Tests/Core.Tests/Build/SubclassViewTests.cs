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

        private static GameLoop Berserker(params string[] activities)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            foreach (var activity in activities)
                loop.CompleteActivity(activity);
            Assert.That(loop.ChooseSubclass("berserker"), Is.EqualTo(SubclassResult.Chosen));
            return loop;
        }

        private static SubclassDef Path
        {
            get
            {
                Catalog.TryGet("berserker", out var subclass);
                return subclass;
            }
        }

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

        private static TreeNodeDef Node(string id)
        {
            Path.TryGet(id, out var node);
            return node;
        }
    }
}
