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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Typist });
            loop.State.Tree.PointsEarned = 46;
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
            var loop = Typist("t_pen_1", "t_pen_2");
            var cost = loop.RefundCost("t_pen_2")!.Value;

            Assert.That(loop.RefundNode("t_pen_2"), Is.EqualTo(RefundResult.Refunded));

            Assert.That(loop.State.Gold, Is.EqualTo(BigNumber.FromDouble(1e9) - cost));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(1));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(106).Within(1e-9));
        }

        [Test]
        public void NodeHoldingThePathCannotBeRefunded()
        {
            var loop = Typist("t_pen_1", "t_pen_2", "t_pen_3");

            Assert.That(loop.RefundNode("t_pen_2"), Is.EqualTo(RefundResult.BreaksPath));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(3));
        }

        [Test]
        public void NodeWithAnotherRouteCanBeRefunded()
        {
            var loop = Typist("t_pen_1", "t_pen_2", "t_pen_3", "t_pen_4", "t_combo_1", "t_combo_2", "t_combo_3", "t_combo_4", "t_attr_2=tempo");

            Assert.That(loop.RefundNode("t_pen_4"), Is.EqualTo(RefundResult.Refunded));
        }

        [Test]
        public void DeeperNodesCostMoreAndAttributesCostHalf()
        {
            var loop = Typist("t_attr_1=tempo", "t_pen_1", "t_pen_2", "t_pen_3", "t_pen_4", "t_combo_1", "t_combo_2", "t_combo_3", "t_combo_4", "t_attr_2=reach");

            var shallow = loop.RefundCost("t_pen_1")!.Value;
            var deep = loop.RefundCost("t_pen_4")!.Value;
            var attribute = loop.RefundCost("t_attr_2")!.Value;
            var sameDepthPlain = loop.RefundCost("t_combo_4")!.Value;

            Assert.That(deep, Is.GreaterThan(shallow));
            Assert.That((deep / shallow).ToDouble(), Is.EqualTo(System.Math.Pow(1.3, 3)).Within(1e-9));
            Assert.That((attribute / sameDepthPlain).ToDouble(), Is.EqualTo(0.5 * 1.3).Within(1e-9));
        }

        [Test]
        public void HigherLevelMakesRefundsDearer()
        {
            var loop = Typist("t_pen_1");
            var atLevelOne = loop.RefundCost("t_pen_1")!.Value;

            loop.State.HeroLevel = 11;

            Assert.That((loop.RefundCost("t_pen_1")!.Value / atLevelOne).ToDouble(), Is.EqualTo(System.Math.Pow(1.1, 10)).Within(1e-9));
        }

        [Test]
        public void RefundNeedsEnoughGold()
        {
            var loop = Typist("t_pen_1");
            loop.State.Gold = BigNumber.FromDouble(1);

            Assert.That(loop.RefundNode("t_pen_1"), Is.EqualTo(RefundResult.NotEnoughGold));
            Assert.That(loop.RefundNode("t_pen_3"), Is.EqualTo(RefundResult.NotAllocated));
            Assert.That(loop.RefundNode("typist_start"), Is.EqualTo(RefundResult.NotAllocated));
        }

        [Test]
        public void FullResetIsFreeOncePerDay()
        {
            var loop = Typist("t_pen_1", "t_pen_2", "t_attr_1=tempo");
            var gold = loop.State.Gold;
            var now = 1_760_000_000_000L;

            Assert.That(loop.FreeFullReset(now), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(loop.State.Gold, Is.EqualTo(gold));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100));

            loop.AllocateNode("t_pen_1");
            Assert.That(loop.FreeFullReset(now + Day - 1), Is.EqualTo(RefundResult.NotYet));
            Assert.That(loop.FreeFullReset(now + Day), Is.EqualTo(RefundResult.Refunded));
        }

        [Test]
        public void FreeResetTimeIsSaved()
        {
            var loop = Typist("t_pen_1");
            loop.FreeFullReset(1_760_000_000_000L);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Tree.LastFreeResetUnixMs, Is.EqualTo(1_760_000_000_000L));
        }
    }
}
