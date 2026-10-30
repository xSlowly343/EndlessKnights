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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking });
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
        public void NodeNextToTheStartCanBeTakenAndFarNodeIsNotConnected()
        {
            var loop = Berserker("first_guardian");
            var state = loop.State.Ascendancy;

            Assert.That(SubclassView.Status(state, Catalog, Path.Start), Is.EqualTo("Начало Пути"));
            Assert.That(SubclassView.Status(state, Catalog, Node("berserker_b0_1")), Is.EqualTo("Можно взять · 1 очко Пути"));
            Assert.That(SubclassView.Status(state, Catalog, Node("berserker_1")), Is.EqualTo("Нет связи со взятыми узлами"));
            Assert.That(SubclassView.CanTake(state, Catalog, Node("berserker_b0_1")), Is.True);
        }

        [Test]
        public void CheckDoesNotSpendThePoint()
        {
            var loop = Berserker("first_guardian");
            var state = loop.State.Ascendancy;

            state.Check(Catalog, "berserker_b0_1");

            Assert.That(state.PointsLeft(Catalog), Is.EqualTo(1));
            Assert.That(loop.AllocateSubclassNode("berserker_b0_1"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(SubclassView.Status(state, Catalog, Node("berserker_b0_1")), Is.EqualTo("Взят"));
            Assert.That(SubclassView.Status(state, Catalog, Node("berserker_1")), Is.EqualTo("Нет свободных очков"));
        }

        [Test]
        public void CardNamesThePathAndShowsTheNodeStats()
        {
            var loop = Berserker("first_guardian");

            var card = SubclassView.Card(loop.State.Ascendancy, Catalog, Path, Node("berserker_1"), loop.Hero);

            Assert.That(card.Name, Is.EqualTo("Без щита"));
            Assert.That(card.Kind, Does.EndWith("Berserker"));
            Assert.That(card.Stats, Has.Count.EqualTo(Node("berserker_1").Modifiers.Count));
            Assert.That(card.Deltas, Is.Not.Empty);
        }

        [Test]
        public void HeaderCountsPointsAndTakenNodes()
        {
            var loop = Berserker("first_guardian", "first_prestige");
            loop.AllocateSubclassNode("berserker_b0_1");

            Assert.That(SubclassView.Points(loop.State.Ascendancy, Catalog, Path), Is.EqualTo($"Очков Пути: 1 · взято 1 / {Path.Nodes.Count - 1}"));
        }

        [Test]
        public void EveryPathHasABlurbAndAPathSkill()
        {
            Assert.That(Catalog.Subclasses.Where(subclass => subclass.Blurb.Length == 0).Select(subclass => subclass.Id), Is.Empty);
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
