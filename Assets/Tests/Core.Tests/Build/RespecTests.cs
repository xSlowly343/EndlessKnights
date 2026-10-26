using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class RespecTests
    {
        private const long Day = 24 * 3_600_000L;

        private static GameLoop Typist(params string[] path)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
            loop.State.Tree.PointsEarned = 120;
            foreach (var step in path)
            {
                var parts = step.Split('=');
                var result = parts.Length == 2 ? loop.AllocateNode(parts[0], attribute: parts[1]) : loop.AllocateNode(step);
                Assert.That(result, Is.EqualTo(AllocationResult.Allocated), step);
            }
            loop.State.Gold = BigNumber.FromDouble(1e9);
            return loop;
        }

        [Test]
        public void LeafNodeIsRefundedForGoldAndStatsDrop()
        {
            var loop = Typist("ronin_ring_1", "ronin_ring_2", "ronin_ring_3");
            var cost = loop.RefundCost("ronin_ring_3")!.Value;

            Assert.That(loop.RefundNode("ronin_ring_3"), Is.EqualTo(RefundResult.Refunded));

            Assert.That(loop.State.Gold, Is.EqualTo(BigNumber.FromDouble(1e9) - cost));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(2));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void NodeHoldingThePathCannotBeRefunded()
        {
            var path = TreePaths.To(TestWorld.Data.Tree, "ronin", "ronin_resource_7");
            var loop = Typist();
            TreePaths.Allocate(loop, TestWorld.Data.Tree, path.Take(3));

            Assert.That(loop.RefundNode(path[1]), Is.EqualTo(RefundResult.BreaksPath));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(3));
        }

        [Test]
        public void NodeWithAnotherRouteCanBeRefunded()
        {
            var loop = Typist("ronin_ring_1", "ronin_ring_2", "ronin_ring_3", "ronin_ring_4", "ronin_ring_5");

            Assert.That(loop.RefundNode("ronin_ring_2"), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.Tree.IsAllocated(TestWorld.Data.Tree, "ronin_ring_3"), Is.True);
        }

        [Test]
        public void DeeperNodesCostMoreAndAttributesCostHalf()
        {
            var path = TreePaths.To(TestWorld.Data.Tree, "ronin", "ronin_resource_7");
            var toAttribute = TreeAllocationTests.ToAttribute;
            var loop = Typist();
            TreePaths.Allocate(loop, TestWorld.Data.Tree, path);
            TreePaths.Allocate(loop, TestWorld.Data.Tree, toAttribute);
            loop.AllocateNode(TreeAllocationTests.FirstAttribute, attribute: "dexterity");

            var shallow = loop.RefundCost(path[0])!.Value;
            var deep = loop.RefundCost(path[2])!.Value;
            var attribute = loop.RefundCost(TreeAllocationTests.FirstAttribute)!.Value;
            var plainAtSameDepth = TestWorld.Data.Tree.Respec.NodeCost(toAttribute.Length + 1, 1, attribute: false);

            Assert.That(deep, Is.GreaterThan(shallow));
            Assert.That((deep / shallow).ToDouble(), Is.EqualTo(System.Math.Pow(1.3, 2)).Within(1e-9));
            Assert.That((attribute / plainAtSameDepth).ToDouble(), Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void HigherLevelMakesRefundsDearer()
        {
            var loop = Typist("ronin_ring_1");
            var atLevelOne = loop.RefundCost("ronin_ring_1")!.Value;

            loop.State.HeroLevel = 11;

            Assert.That((loop.RefundCost("ronin_ring_1")!.Value / atLevelOne).ToDouble(), Is.EqualTo(System.Math.Pow(1.1, 10)).Within(1e-9));
        }

        [Test]
        public void RefundNeedsEnoughGold()
        {
            var loop = Typist("ronin_ring_1");
            loop.State.Gold = BigNumber.FromDouble(1);

            Assert.That(loop.RefundNode("ronin_ring_1"), Is.EqualTo(RefundResult.NotEnoughGold));
            Assert.That(loop.RefundNode("ronin_ring_3"), Is.EqualTo(RefundResult.NotAllocated));
            Assert.That(loop.RefundNode("ronin_start"), Is.EqualTo(RefundResult.NotAllocated));
        }

        [Test]
        public void FullResetIsFreeOncePerDay()
        {
            var loop = Typist("ronin_ring_1", "ronin_ring_2", "ronin_ring_3");
            var gold = loop.State.Gold;
            var now = 1_760_000_000_000L;

            Assert.That(loop.FreeFullReset(now), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(loop.State.Gold, Is.EqualTo(gold));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100));

            loop.AllocateNode("ronin_ring_1");
            Assert.That(loop.FreeFullReset(now + Day - 1), Is.EqualTo(RefundResult.NotYet));
            Assert.That(loop.FreeFullReset(now + Day), Is.EqualTo(RefundResult.Refunded));
        }

        [Test]
        public void FullResetIsFreeOnceADayAndThenCostsHalfTheRefunds()
        {
            var loop = Typist("ronin_ring_1", "ronin_ring_2");
            var now = 1_760_000_000_000L;
            loop.CompleteActivity(ActivityIds.FirstGuardian);
            loop.ChooseSubclass("kensei");
            var path = loop.Data.Ascendancy.SubclassesOf(loop.State.Tree.StartClass).Single(s => s.Id == "kensei").Start.Neighbours[0];
            loop.AllocateSubclassNode(path.Id);

            Assert.That(loop.FullResetCost(now), Is.EqualTo(BigNumber.Zero));
            Assert.That(loop.FullReset(now), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(loop.State.Ascendancy.IsAllocated(path.Id), Is.True);
            Assert.That(loop.FullReset(now), Is.EqualTo(RefundResult.NotAllocated));

            loop.AllocateNode("ronin_ring_1");
            loop.AllocateNode("ronin_ring_2");
            var half = (loop.RefundCost("ronin_ring_1")!.Value + loop.RefundCost("ronin_ring_2")!.Value) * BigNumber.FromDouble(0.5);
            Assert.That(loop.FullResetCost(now + 1).ToDouble(), Is.EqualTo(half.ToDouble()).Within(1e-6));

            loop.State.Gold = BigNumber.Zero;
            Assert.That(loop.FullReset(now + 1), Is.EqualTo(RefundResult.NotEnoughGold));
            loop.State.Gold = half;
            Assert.That(loop.FullReset(now + 1), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.Gold.ToDouble(), Is.EqualTo(0).Within(1e-6));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
        }

        [Test]
        public void FreeResetTimeIsSaved()
        {
            var loop = Typist("ronin_ring_1");
            loop.FreeFullReset(1_760_000_000_000L);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Tree.LastFreeResetUnixMs, Is.EqualTo(1_760_000_000_000L));
        }
    }
}
