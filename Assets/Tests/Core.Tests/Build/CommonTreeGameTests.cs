using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class CommonTreeGameTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GameLoop Hero(int level, HeroClass heroClass = HeroClass.Viking)
        {
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState { Class = heroClass, HeroLevel = level }, new StatSheet());
            loop.Levels.SyncTreePoints();
            loop.RebuildTree();
            return loop;
        }

        private static void Raise(GameLoop loop, string node, int times)
        {
            for (var i = 0; i < times; i++)
                Assert.That(loop.RaiseNode(node), Is.EqualTo(RankResult.Done), node);
        }

        [Test]
        public void SlotsOpenAtTheTwentiethAndThirtiethLevelWithAPointPerLevel()
        {
            var loop = Hero(19);
            Assert.That(loop.ChooseCommonTree(0, "blade"), Is.EqualTo(CommonTreeResult.SlotClosed));

            loop = Hero(25);
            Assert.That(loop.ChooseCommonTree(0, "blade"), Is.EqualTo(CommonTreeResult.Chosen));
            Assert.That(loop.CommonTreeAllocation(0)!.PointsEarned, Is.EqualTo(5));
            Assert.That(loop.ChooseCommonTree(1, "calm"), Is.EqualTo(CommonTreeResult.SlotClosed));
            Assert.That(loop.ChooseCommonTree(0, "nope"), Is.EqualTo(CommonTreeResult.UnknownTree));

            loop.State.HeroLevel = 100;
            loop.Levels.SyncTreePoints();
            Assert.That(loop.CommonTreeAllocation(0)!.PointsEarned, Is.EqualTo(70));
            Assert.That(loop.ChooseCommonTree(1, "blade"), Is.EqualTo(CommonTreeResult.TakenInOtherSlot));
            Assert.That(loop.ChooseCommonTree(0, "blade"), Is.EqualTo(CommonTreeResult.SameTree));
            Assert.That(loop.ChooseCommonTree(1, "calm"), Is.EqualTo(CommonTreeResult.Chosen));
            Assert.That(loop.CommonTreeAllocation(1)!.PointsEarned, Is.EqualTo(70));
            Assert.That(loop.State.ClassTree.PointsEarned, Is.EqualTo(loop.State.TreePointsEarned), "the class tree keeps its own points");
        }

        [Test]
        public void CommonTreeNodesAndTalentsAddToTheHero()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(0, "colossus");
            var before = loop.Hero.Value(CombatStats.DamageVsBoss, 1);

            Raise(loop, "colossus_boss", 5);
            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.GreaterThan(before));
            Assert.That(loop.State.TreePointsSpent, Is.Zero, "common points are not class tree points");
            Assert.That(loop.RaiseNode("blade_crit"), Is.EqualTo(RankResult.UnknownNode), "Blade is not in a slot");

            Raise(loop, "colossus_opening_blow", 5);
            Raise(loop, "colossus_gate_warden", 5);
            Raise(loop, "colossus_patience", 5);
            Assert.That(loop.ChooseMajorTalent(20, "colossus_soft_spot"), Is.EqualTo(RankResult.Done));
            var withTalent = loop.Hero.Value(CombatStats.DamageVsBoss, 1);
            Assert.That(loop.ChooseMajorTalent(20, "colossus_long_battle"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.DamageVsBoss, 1), Is.LessThan(withTalent));
            Assert.That(loop.ChooseMajorTalent(45, "colossus_colossus_slayer"), Is.EqualTo(RankResult.MarkNotReached));
        }

        [Test]
        public void ChangingACommonTreeIsFreeBeforeTheFirstLimitAndCostsAFullResetAfter()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(0, "blade");
            Raise(loop, "blade_crit", 3);

            Assert.That(loop.CommonTreeChangeCost(0), Is.EqualTo(BigNumber.Zero));
            Assert.That(loop.ChooseCommonTree(0, "fortune"), Is.EqualTo(CommonTreeResult.Chosen));
            Assert.That(loop.CommonTreeAllocation(0)!.PointsLeft, Is.EqualTo(40), "the points of the slot come back");
            Raise(loop, "fortune_loot", 4);

            loop.State.GatesCleared.Add(10_000);
            var cost = loop.CommonTreeChangeCost(0);
            Assert.That(cost, Is.GreaterThan(BigNumber.Zero));
            Assert.That(cost, Is.EqualTo(loop.CommonTreeResetCost(0)));
            loop.State.Gold = BigNumber.Zero;
            Assert.That(loop.ChooseCommonTree(0, "blade"), Is.EqualTo(CommonTreeResult.NotEnoughGold));
            loop.State.Gold = cost;
            Assert.That(loop.ChooseCommonTree(0, "blade"), Is.EqualTo(CommonTreeResult.Chosen));
            Assert.That(loop.State.Gold, Is.EqualTo(BigNumber.Zero));
            Assert.That(loop.CommonTreeAllocation(0)!.PointsSpent, Is.Zero);
        }

        [Test]
        public void ResetAndFillWorkOnACommonTree()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(0, "calm");
            Raise(loop, "calm_focus", 3);
            Raise(loop, "calm_quiet_hour", 2);

            Assert.That(loop.ResetCommonTree(0), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.CommonTreeAllocation(0)!.PointsSpent, Is.Zero);
            Assert.That(loop.FillTrees(), Is.EqualTo(5));
            Assert.That(loop.CommonTreeAllocation(0)!.RankOf("calm_focus"), Is.EqualTo(3));
            Assert.That(loop.LowerNode("calm_quiet_hour"), Is.EqualTo(RefundResult.Refunded));
        }

        [Test]
        public void ChangingTheClassKeepsTheCommonTrees()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(0, "party");
            Raise(loop, "party_banner", 2);

            Assert.That(loop.ChangeClassFree(HeroClass.Ronin), Is.EqualTo(ClassChangeResult.Changed));

            Assert.That(loop.CommonTreeAllocation(0)!.RankOf("party_banner"), Is.EqualTo(2));
        }

        [Test]
        public void EachPresetKeepsItsOwnCommonTrees()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(0, "blade");
            Raise(loop, "blade_crit", 1);

            loop.SwitchPreset();
            Assert.That(loop.CommonTreeDef(0), Is.Null);
            loop.ChooseCommonTree(0, "agility");
            loop.SwitchPreset();

            Assert.That(loop.CommonTreeDef(0)!.Id, Is.EqualTo("blade"));
            Assert.That(loop.State.OtherPreset.CommonTrees[0]!.TreeId, Is.EqualTo("agility"));
        }

        [Test]
        public void TheSaveKeepsCommonTreesOfBothPresets()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(0, "colossus");
            loop.ChooseCommonTree(1, "fortune");
            Raise(loop, "colossus_boss", 5);
            Raise(loop, "colossus_patience", 5);
            Raise(loop, "colossus_gate_warden", 5);
            Raise(loop, "colossus_opening_blow", 5);
            loop.ChooseMajorTalent(20, "colossus_first_blow");
            loop.SwitchPreset();
            loop.ChooseCommonTree(1, "calm");
            loop.SwitchPreset();

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), TestWorld.Real).State;
            var again = new GameLoop(TestWorld.Real, new FakeInputSource(), loaded, new StatSheet());

            Assert.That(again.CommonTreeAllocation(0)!.RankOf("colossus_boss"), Is.EqualTo(5));
            Assert.That(again.CommonTreeAllocation(0)!.Majors[20], Is.EqualTo("colossus_first_blow"));
            Assert.That(again.CommonTreeDef(1)!.Id, Is.EqualTo("fortune"));
            Assert.That(again.State.OtherPreset.CommonTrees[0], Is.Null);
            Assert.That(again.State.OtherPreset.CommonTrees[1]!.TreeId, Is.EqualTo("calm"));
        }

        [Test]
        public void ALostLevelOrAnUnknownTreeEmptiesTheSlot()
        {
            var loop = Hero(60);
            loop.ChooseCommonTree(1, "blade");
            loop.State.CommonTrees[0] = new ClassTreeAllocation("gone");
            loop.State.HeroLevel = 25;
            loop.RebuildTree();

            Assert.That(loop.CommonTreeDef(0), Is.Null);
            Assert.That(loop.CommonTreeDef(1), Is.Null, "slot 3 opens at level 30");
        }
    }
}
