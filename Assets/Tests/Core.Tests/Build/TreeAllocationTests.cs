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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Swordsman });
            loop.State.Tree.PointsEarned = points;
            return loop;
        }

        public static string FirstAttribute => TreePaths.NearestOfType(Tree, "swordsman", TreeNodeType.Attribute);

        public static string[] ToAttribute => TreePaths.To(Tree, "swordsman", FirstAttribute).SkipLast(1).ToArray();

        private static double KeyDamage(GameLoop loop) => loop.Hero.Compute(CombatStats.Damage, BigNumber.FromDouble(100), StatTags.Keyboard).ToDouble();

        [Test]
        public void ClassStartIsTakenForFreeAndOffersItsOwnExits()
        {
            var loop = Typist(1);

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(loop.State.Tree.IsAllocated(Tree, "swordsman_start"), Is.True);
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(offered, Is.EqualTo(new[] { "swordsman_ring_0", "swordsman_ring_1", "swordsman_ring_5" }));
        }

        [Test]
        public void OtherClassExitsAreNotOffered()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("bard_ring_0"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateNode("wanderer_ring_0"), Is.EqualTo(AllocationResult.NotConnected));
        }

        [Test]
        public void AfterANodeItsNeighboursJoinTheOffer()
        {
            var loop = Typist(5);
            loop.AllocateNode("swordsman_ring_1");

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);
            var expected = Tree["swordsman_start"].Neighbours.Concat(Tree["swordsman_ring_1"].Neighbours)
                .Where(node => node.Type != TreeNodeType.Start && !node.Locked && node.Id != "swordsman_ring_1").Select(node => node.Id).Distinct().OrderBy(id => id);

            Assert.That(offered, Is.EqualTo(expected));
            Assert.That(offered, Does.Contain("swordsman_ring_2"));
        }

        [Test]
        public void WandererBeginsAmongItsOwnRing()
        {
            var loop = TestWorld.Loop(state: new GameState { Tree = new TreeAllocation("wanderer") { PointsEarned = 1 } });

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(offered, Is.EqualTo(Enumerable.Range(0, 6).Select(k => "wanderer_ring_" + k)));
        }

        [Test]
        public void NeighbourOfStartCanBeTaken()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("swordsman_ring_1"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.State.Tree.PointsLeft, Is.EqualTo(0));
        }

        [Test]
        public void UnconnectedNodeCannotBeTaken()
        {
            var loop = Typist(10);

            Assert.That(loop.AllocateNode("swordsman_ring_3"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateNode("bard_w1_notable"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
        }

        [Test]
        public void PathGrowsStepByStep()
        {
            var path = TreePaths.To(Tree, "swordsman", "swordsman_w1_notable");
            var loop = Typist(path.Length - 1);

            TreePaths.Allocate(loop, Tree, path.SkipLast(1));
            Assert.That(loop.AllocateNode(path.Last()), Is.EqualTo(AllocationResult.NoPoints));
        }

        [Test]
        public void NodeModifiersGoIntoHeroStats()
        {
            var loop = Typist(3);

            loop.AllocateNode("swordsman_ring_1");
            loop.AllocateNode("swordsman_ring_2");
            loop.AllocateNode("swordsman_ring_3");

            Assert.That(KeyDamage(loop), Is.EqualTo(105).Within(1e-9));
        }

        [Test]
        public void AttributeNodeNeedsAChoiceAndGivesThatAttribute()
        {
            var loop = Typist(ToAttribute.Length + 1);
            foreach (var id in ToAttribute)
                loop.AllocateNode(id);

            Assert.That(loop.AllocateNode(FirstAttribute), Is.EqualTo(AllocationResult.ChoiceRequired));
            Assert.That(loop.AllocateNode(FirstAttribute, attribute: "luck"), Is.EqualTo(AllocationResult.BadChoice));
            Assert.That(loop.AllocateNode(FirstAttribute, attribute: "intelligence"), Is.EqualTo(AllocationResult.Allocated));

            Assert.That(loop.Hero.Compute(PassiveTree.AttributeStat("intelligence"), BigNumber.Zero).ToDouble(), Is.EqualTo(5));
            Assert.That(loop.Hero.Compute(PassiveTree.AttributeStat("strength"), BigNumber.Zero).ToDouble(), Is.EqualTo(0));
        }

        [Test]
        public void PlainNodeRefusesAChoice()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("swordsman_ring_1", attribute: "strength"), Is.EqualTo(AllocationResult.BadChoice));
        }

        [Test]
        public void JewelSocketsAndStartsCannotBeTaken()
        {
            var jewel = TreePaths.NearestOfType(Tree, "swordsman", TreeNodeType.Jewel);
            var loop = Typist(60);
            loop.AllocateNode("swordsman_ring_0");
            TreePaths.Allocate(loop, Tree, TreePaths.To(Tree, "swordsman", jewel).SkipLast(1));

            Assert.That(loop.AllocateNode(jewel), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("wanderer_start"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("no_such_node"), Is.EqualTo(AllocationResult.UnknownNode));
            Assert.That(loop.AllocateNode("swordsman_ring_0"), Is.EqualTo(AllocationResult.AlreadyAllocated));
        }

        [Test]
        public void RoadGoesAroundAnotherClassStart()
        {
            var loop = TestWorld.Loop(state: new GameState { Tree = new TreeAllocation("wanderer") });
            loop.State.Tree.PointsEarned = 40;
            TreePaths.Allocate(loop, Tree, TreePaths.To(Tree, "wanderer", "swordsman_ring_3"));

            Assert.That(loop.AllocateNode("swordsman_start"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("swordsman_ring_2"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateNode("swordsman_ring_1"), Is.EqualTo(AllocationResult.Allocated));
        }

        [Test]
        public void AllocationSurvivesSaveAndStatsComeBack()
        {
            var loop = Typist(ToAttribute.Length + 1);
            foreach (var id in ToAttribute)
                loop.AllocateNode(id);
            loop.AllocateNode(FirstAttribute, attribute: "strength");
            var damage = KeyDamage(loop);
            var text = SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1)));

            var hero = new StatSheet();
            var loaded = TestWorld.Loop(state: SaveCodec.Decode(text, TestWorld.Data).State, hero: hero);

            Assert.That(damage, Is.GreaterThanOrEqualTo(100));
            Assert.That(loaded.State.Tree.PointsSpent, Is.EqualTo(ToAttribute.Length + 1));
            Assert.That(loaded.State.Tree.AttributeChoices[FirstAttribute], Is.EqualTo("strength"));
            Assert.That(KeyDamage(loaded), Is.EqualTo(damage).Within(1e-9));
            Assert.That(hero.Compute(PassiveTree.AttributeStat("strength"), BigNumber.Zero).ToDouble(), Is.EqualTo(5));
        }

        [Test]
        public void NodesCutOffByATreeChangeAreRefundedOnLoad()
        {
            var state = new GameState { Class = HeroClass.Swordsman };
            state.Tree.Restore("swordsman_ring_1", null, null);
            state.Tree.Restore("swordsman_ring_3", null, null);
            state.Tree.Restore("removed_node", null, null);
            state.OtherPreset.Tree.Restore("bard_w1_notable", null, null);

            var loop = TestWorld.Loop(state: state);

            Assert.That(loop.State.Tree.Allocated, Is.EquivalentTo(new[] { "swordsman_ring_1" }));
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
