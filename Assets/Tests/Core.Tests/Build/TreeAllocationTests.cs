using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class TreeAllocationTests
    {
        private static PassiveTree Tree => TestWorld.Data.Tree;

        private static GameLoop Typist(int points)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Typist });
            loop.State.Tree.PointsEarned = points;
            return loop;
        }

        private static double KeyDamage(GameLoop loop) => loop.Hero.Compute(CombatStats.Damage, BigNumber.FromDouble(100), StatTags.Keyboard).ToDouble();

        [Test]
        public void ClassStartIsTakenForFreeAndOffersItsOwnExits()
        {
            var loop = Typist(1);

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(loop.State.Tree.IsAllocated(Tree, "typist_start"), Is.True);
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(offered, Is.EqualTo(new[] { "t_combo_1", "t_heavy_1", "t_pen_1" }));
        }

        [Test]
        public void OtherClassExitsAreNotOffered()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("b_ensemble_1"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateNode("c_patience"), Is.EqualTo(AllocationResult.NotConnected));
        }

        [Test]
        public void AfterANodeItsNeighboursJoinTheOffer()
        {
            var loop = Typist(5);
            loop.AllocateNode("t_combo_1");

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(offered, Is.EqualTo(new[] { "t_combo_2", "t_heavy_1", "t_pen_1" }));
        }

        [Test]
        public void HermitBeginsAmongItsOwnNodes()
        {
            var loop = TestWorld.Loop(state: new GameState { Tree = new TreeAllocation("hermit") { PointsEarned = 1 } });

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(offered, Is.EqualTo(new[] { "c_hearth", "c_meditation", "c_patience", "c_silence_1", "c_stillness" }));
        }

        [Test]
        public void NeighbourOfStartCanBeTaken()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("t_pen_1"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.State.Tree.PointsLeft, Is.EqualTo(0));
        }

        [Test]
        public void UnconnectedNodeCannotBeTaken()
        {
            var loop = Typist(10);

            Assert.That(loop.AllocateNode("t_pen_3"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateNode("chorus"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
        }

        [Test]
        public void PathGrowsStepByStep()
        {
            var loop = Typist(5);

            foreach (var id in new[] { "t_pen_1", "t_pen_2", "fluent_writing", "t_pen_3", "t_pen_4" })
                Assert.That(loop.AllocateNode(id), Is.EqualTo(AllocationResult.Allocated), id);
            Assert.That(loop.AllocateNode("pen_mastery", masteryOption: 0), Is.EqualTo(AllocationResult.NoPoints));
        }

        [Test]
        public void NodeModifiersGoIntoHeroStats()
        {
            var loop = Typist(2);

            loop.AllocateNode("t_pen_1");
            loop.AllocateNode("t_pen_2");

            Assert.That(KeyDamage(loop), Is.EqualTo(110).Within(1e-9));
        }

        [Test]
        public void AttributeNodeNeedsAChoiceAndGivesThatAttribute()
        {
            var loop = Typist(3);

            loop.AllocateNode("t_pen_1");
            Assert.That(loop.AllocateNode("t_attr_1"), Is.EqualTo(AllocationResult.ChoiceRequired));
            Assert.That(loop.AllocateNode("t_attr_1", attribute: "luck"), Is.EqualTo(AllocationResult.BadChoice));
            Assert.That(loop.AllocateNode("t_attr_1", attribute: "precision"), Is.EqualTo(AllocationResult.Allocated));

            Assert.That(loop.Hero.Compute(PassiveTree.AttributeStat("precision"), BigNumber.Zero).ToDouble(), Is.EqualTo(5));
            Assert.That(loop.Hero.Compute(PassiveTree.AttributeStat("tempo"), BigNumber.Zero).ToDouble(), Is.EqualTo(0));
        }

        [Test]
        public void PlainNodeRefusesAChoice()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("t_pen_1", attribute: "tempo"), Is.EqualTo(AllocationResult.BadChoice));
        }

        [Test]
        public void JewelSocketsAndStartsCannotBeTaken()
        {
            var loop = Typist(40);
            foreach (var id in new[] { "t_combo_1", "t_combo_2", "t_combo_3", "t_combo_4", "t_combo_small", "unstoppable" })
                loop.AllocateNode(id);

            Assert.That(loop.AllocateNode("t_jewel"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("hermit_start"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("no_such_node"), Is.EqualTo(AllocationResult.UnknownNode));
            Assert.That(loop.AllocateNode("unstoppable"), Is.EqualTo(AllocationResult.AlreadyAllocated));
        }

        [Test]
        public void InnerRingGoesAroundTheHermitStart()
        {
            var loop = Typist(5);

            loop.AllocateNode("t_pen_1");
            loop.AllocateNode("t_attr_1", attribute: "tempo");
            loop.AllocateNode("c_patience");

            Assert.That(loop.AllocateNode("hermit_start"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("c_meditation"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateNode("c_hearth"), Is.EqualTo(AllocationResult.Allocated));
        }

        [Test]
        public void AllocationSurvivesSaveAndStatsComeBack()
        {
            var loop = Typist(3);
            loop.AllocateNode("t_pen_1");
            loop.AllocateNode("t_pen_2");
            loop.AllocateNode("t_attr_1", attribute: "tempo");
            var text = SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1)));

            var hero = new StatSheet();
            var loaded = TestWorld.Loop(state: SaveCodec.Decode(text, TestWorld.Data).State, hero: hero);

            Assert.That(loaded.State.Tree.PointsSpent, Is.EqualTo(3));
            Assert.That(loaded.State.Tree.AttributeChoices["t_attr_1"], Is.EqualTo("tempo"));
            Assert.That(KeyDamage(loaded), Is.EqualTo(110).Within(1e-9));
            Assert.That(hero.Compute(PassiveTree.AttributeStat("tempo"), BigNumber.Zero).ToDouble(), Is.EqualTo(5));
        }

        [Test]
        public void NodesCutOffByATreeChangeAreRefundedOnLoad()
        {
            var state = new GameState { Class = HeroClass.Typist };
            state.Tree.Restore("t_pen_1", null, null);
            state.Tree.Restore("t_pen_3", null, null);
            state.Tree.Restore("removed_node", null, null);
            state.OtherPreset.Tree.Restore("chorus", null, null);

            var loop = TestWorld.Loop(state: state);

            Assert.That(loop.State.Tree.Allocated, Is.EquivalentTo(new[] { "t_pen_1" }));
            Assert.That(loop.State.OtherPreset.Tree.Allocated, Is.Empty);
        }

        [Test]
        public void OldSavesGetAnEmptyTreeOfTheirClass()
        {
            var save = SaveCodec.Decode(Content.TestData.ReadSaveFixture("save_v1.json"), TestWorld.Data);

            Assert.That(save.State.Tree.StartClass, Is.EqualTo("bard"));
            Assert.That(save.State.Tree.PointsSpent, Is.EqualTo(0));
        }
    }
}
