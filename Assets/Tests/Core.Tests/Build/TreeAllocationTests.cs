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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
            loop.State.Tree.PointsEarned = points;
            return loop;
        }

        public static string FirstAttribute => TreePaths.NearestOfType(Tree, "ronin", TreeNodeType.Attribute);

        public static string[] ToAttribute => TreePaths.To(Tree, "ronin", FirstAttribute).SkipLast(1).ToArray();

        private static double KeyDamage(GameLoop loop) => loop.Hero.Compute(CombatStats.Damage, BigNumber.FromDouble(100), StatTags.Keyboard).ToDouble();

        [Test]
        public void ClassStartIsTakenForFreeAndOffersItsOwnExits()
        {
            var loop = Typist(1);

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(loop.State.Tree.IsAllocated(Tree, "ronin_start"), Is.True);
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(offered, Is.EqualTo(new[] { "ronin_ring_0", "ronin_ring_1", "ronin_ring_5" }));
        }

        [Test]
        public void OtherClassExitsAreNotOffered()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("metalhead_ring_0"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateNode("nomad_ring_0"), Is.EqualTo(AllocationResult.NotConnected));
        }

        [Test]
        public void AfterANodeItsNeighboursJoinTheOffer()
        {
            var loop = Typist(5);
            loop.AllocateNode("ronin_ring_1");

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);
            var expected = Tree["ronin_start"].Neighbours.Concat(Tree["ronin_ring_1"].Neighbours)
                .Where(node => node.Type != TreeNodeType.Start && !node.Locked && node.Id != "ronin_ring_1").Select(node => node.Id).Distinct().OrderBy(id => id);

            Assert.That(offered, Is.EqualTo(expected));
            Assert.That(offered, Does.Contain("ronin_ring_2"));
        }

        [Test]
        public void WandererBeginsAmongItsOwnRing()
        {
            var loop = TestWorld.Loop(state: new GameState { Tree = new TreeAllocation("nomad") { PointsEarned = 1 } });

            var offered = loop.State.Tree.Allocatable(Tree).Select(node => node.Id).OrderBy(id => id);

            Assert.That(offered, Is.EqualTo(Enumerable.Range(0, 6).Select(k => "nomad_ring_" + k)));
        }

        [Test]
        public void NeighbourOfStartCanBeTaken()
        {
            var loop = Typist(1);

            Assert.That(loop.AllocateNode("ronin_ring_1"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.State.Tree.PointsLeft, Is.EqualTo(0));
        }

        [Test]
        public void UnconnectedNodeCannotBeTaken()
        {
            var loop = Typist(10);

            Assert.That(loop.AllocateNode("ronin_ring_3"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateNode("metalhead_skills_3"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
        }

        [Test]
        public void PathGrowsStepByStep()
        {
            var path = TreePaths.To(Tree, "ronin", "ronin_resource_7");
            var loop = Typist(path.Length - 1);

            TreePaths.Allocate(loop, Tree, path.SkipLast(1));
            Assert.That(loop.AllocateNode(path.Last()), Is.EqualTo(AllocationResult.NoPoints));
        }

        [Test]
        public void NodeModifiersGoIntoHeroStats()
        {
            var loop = Typist(3);

            loop.AllocateNode("ronin_ring_1");
            loop.AllocateNode("ronin_ring_2");
            loop.AllocateNode("ronin_ring_3");

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Skill), Is.EqualTo(108).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.CritChance, 1, StatTags.Keyboard), Is.EqualTo(1.04).Within(1e-9));
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

            Assert.That(loop.AllocateNode("ronin_ring_1", attribute: "strength"), Is.EqualTo(AllocationResult.BadChoice));
        }

        [Test]
        public void JewelSocketsAndStartsCannotBeTaken()
        {
            var jewel = TreePaths.NearestOfType(Tree, "ronin", TreeNodeType.Jewel);
            var loop = Typist(60);
            loop.AllocateNode("ronin_ring_0");
            TreePaths.Allocate(loop, Tree, TreePaths.To(Tree, "ronin", jewel).SkipLast(1));

            Assert.That(loop.AllocateNode(jewel), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("nomad_start"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("no_such_node"), Is.EqualTo(AllocationResult.UnknownNode));
            Assert.That(loop.AllocateNode("ronin_ring_0"), Is.EqualTo(AllocationResult.AlreadyAllocated));
        }

        [Test]
        public void RoadGoesAroundAnotherClassStart()
        {
            var loop = TestWorld.Loop(state: new GameState { Tree = new TreeAllocation("nomad") });
            loop.State.Tree.PointsEarned = 40;
            TreePaths.Allocate(loop, Tree, TreePaths.To(Tree, "nomad", "ronin_ring_3"));

            Assert.That(loop.AllocateNode("ronin_start"), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.AllocateNode("ronin_ring_2"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateNode("ronin_ring_1"), Is.EqualTo(AllocationResult.Allocated));
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
            var state = new GameState { Class = HeroClass.Ronin };
            state.Tree.Restore("ronin_ring_1", null, null);
            state.Tree.Restore("ronin_ring_3", null, null);
            state.Tree.Restore("removed_node", null, null);
            state.OtherPreset.Tree.Restore("metalhead_w1_notable", null, null);

            var loop = TestWorld.Loop(state: state);

            Assert.That(loop.State.Tree.Allocated, Is.EquivalentTo(new[] { "ronin_ring_1" }));
            Assert.That(loop.State.OtherPreset.Tree.Allocated, Is.Empty);
        }

        [Test]
        public void OldSavesGetAnEmptyTreeOfTheirClass()
        {
            var save = SaveCodec.Decode(Content.TestData.ReadSaveFixture("save_v1.json"), TestWorld.Data);

            Assert.That(save.State.Tree.StartClass, Is.EqualTo("metalhead"));
            Assert.That(save.State.Tree.PointsSpent, Is.EqualTo(0));
        }

        [Test]
        public void VikingZoneNodeGivesTheVikingAndTheRoninTheSameEffect()
        {
            var viking = new TreeAllocation("viking");
            var ronin = new TreeAllocation("ronin");
            viking.Restore("viking_resource_0", null, null);
            ronin.Restore("viking_resource_0", null, null);

            Assert.That(ronin.Modifiers(Tree).Where(m => m.Stat.Equals(CombatStats.ResourceGain)).Sum(m => m.Value), Is.EqualTo(0.06).Within(1e-9));
            Assert.That(viking.Modifiers(Tree).Where(m => m.Stat.Equals(CombatStats.ResourceGain)).Sum(m => m.Value), Is.EqualTo(0.06).Within(1e-9));
        }

        [Test]
        public void ZoneMasteryGivesTheSameOptionToEveryCharacter()
        {
            var node = Tree["viking_skills_mastery"];
            var viking = new TreeAllocation("viking");
            var ronin = new TreeAllocation("ronin");
            viking.Restore(node.Id, null, 0);
            ronin.Restore(node.Id, null, 0);

            Assert.That(viking.Modifiers(Tree), Is.EqualTo(node.Options[0]));
            Assert.That(ronin.Modifiers(Tree), Is.EqualTo(node.Options[0]));
        }

        [Test]
        public void ZoneOfAnotherCharacterCanBeReachedAndTaken()
        {
            var path = TreePaths.To(Tree, "ronin", "viking_resource_0");
            var loop = Typist(path.Length);

            TreePaths.Allocate(loop, Tree, path);

            Assert.That(loop.State.Tree.IsAllocated(Tree, "viking_resource_0"), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.ResourceGain, 1), Is.GreaterThan(1.0));
        }
    }
}
