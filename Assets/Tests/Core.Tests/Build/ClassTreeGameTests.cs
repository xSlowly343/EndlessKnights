using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassTreeGameTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GameLoop Huscarl(int level = 40)
        {
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState { Class = HeroClass.Runedancer, HeroLevel = level }, new StatSheet());
            loop.Weapons.Unequip(WeaponHand.Right);
            loop.Levels.SyncTreePoints();
            loop.RebuildTree();
            return loop;
        }

        private static void Raise(GameLoop loop, string node, int times)
        {
            for (var i = 0; i < times; i++)
                Assert.That(loop.RaiseNode("runedancer_" + node), Is.EqualTo(RankResult.Done), node);
        }

        [Test]
        public void HuscarlPlaysTheClassTreeRowByRow()
        {
            var loop = Huscarl();

            Assert.That(loop.ClassTreeDef, Is.Not.Null);
            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.State.TreePointsLeft, Is.EqualTo(loop.State.TreePointsEarned));
            Assert.That(loop.RaiseNode("runedancer_r2c0"), Is.EqualTo(RankResult.RowClosed));
            Raise(loop, "r1c0", 5);
            Assert.That(loop.RaiseNode("runedancer_r2c0"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.State.TreePointsSpent, Is.EqualTo(6));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(106).Within(1e-9), "the fifth level of the first node gives hits 6%, the second row starts with crits");
        }

        [Test]
        public void TakingBackARankIsFreeUntilTheFirstLimitAndCostsGoldAfter()
        {
            var loop = Huscarl();
            Raise(loop, "r1c0", 2);

            Assert.That(loop.ClassTreeResetFree, Is.True);
            Assert.That(loop.LowerCost("runedancer_r1c0"), Is.EqualTo(BigNumber.Zero));
            Assert.That(loop.LowerNode("runedancer_r1c0"), Is.EqualTo(RefundResult.Refunded));

            loop.State.GatesCleared.Add(10_000);
            loop.State.Gold = BigNumber.Zero;
            Assert.That(loop.LowerCost("runedancer_r1c0")!.Value.Sign, Is.GreaterThan(0));
            Assert.That(loop.LowerNode("runedancer_r1c0"), Is.EqualTo(RefundResult.NotEnoughGold));
            loop.State.Gold = BigNumber.FromDouble(1e9);
            Assert.That(loop.LowerNode("runedancer_r1c0"), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.ClassTree.RankOf("runedancer_r1c0"), Is.Zero);
            Assert.That(loop.LowerNode("runedancer_r1c0"), Is.EqualTo(RefundResult.NotAllocated));
        }

        [Test]
        public void AFreeResetAndFillBringTheSameBuildBack()
        {
            var loop = Huscarl();
            Raise(loop, "r1c1", 5);
            Raise(loop, "r2c1", 3);
            Raise(loop, "r1c4", 2);
            var before = loop.State.ClassTree.Ranks.ToDictionary(pair => pair.Key, pair => pair.Value);

            Assert.That(loop.ClassTreeResetCost(), Is.EqualTo(BigNumber.Zero));
            Assert.That(loop.ResetClassTree(), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.TreePointsSpent, Is.Zero);
            Assert.That(loop.FillTrees(), Is.EqualTo(10));

            Assert.That(loop.State.ClassTree.Ranks, Is.EquivalentTo(before));
        }

        [Test]
        public void AMajorTalentOnTheBarWorksFromItsMark()
        {
            var loop = Huscarl(60);
            Raise(loop, "r1c4", 5);
            Raise(loop, "r1c5", 5);
            Raise(loop, "r1c6", 5);
            Raise(loop, "r1c7", 4);

            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m50_2"), Is.EqualTo(RankResult.UnknownMajor), "Cool Head is on the 50 mark");
            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m20_1"), Is.EqualTo(RankResult.MarkNotReached));
            var damage = loop.Hero.Value(CombatStats.Damage, 1);
            Raise(loop, "r1c8", 1);
            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m20_1"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.GreaterThan(damage));
            Assert.That(loop.Hero.Value(TreeStat("classAttribute"), 0), Is.EqualTo(3), "two bar steps: +3 class attribute and +2% Wrath gain");
        }

        [Test]
        public void AMajorTalentChangesOnlyOutsideABossFight()
        {
            var loop = Huscarl(60);
            Raise(loop, "r1c4", 5);
            Raise(loop, "r1c5", 5);
            Raise(loop, "r1c6", 5);
            Raise(loop, "r1c7", 5);
            loop.State.SpawnEnemy(null, Game.Core.Content.EnemyType.Boss, BigNumber.FromDouble(1e9));

            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m20_1"), Is.EqualTo(RankResult.Done), "the first choice is a new talent, like a new rank");
            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m20_1"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m20_2"), Is.EqualTo(RankResult.InBossFight));
            Assert.That(loop.State.ClassTree.Majors[20], Is.EqualTo("runedancer_m20_1"));

            loop.State.SpawnEnemy(null, Game.Core.Content.EnemyType.Normal, BigNumber.FromDouble(1e9));
            Assert.That(loop.ChooseMajorTalent(20, "runedancer_m20_2"), Is.EqualTo(RankResult.Done));
        }

        [Test]
        public void EachPresetKeepsItsOwnClassTree()
        {
            var loop = Huscarl();
            Raise(loop, "r1c0", 3);

            loop.SwitchPreset();
            Assert.That(loop.State.TreePointsSpent, Is.Zero);
            Raise(loop, "r1c1", 2);
            loop.SwitchPreset();

            Assert.That(loop.State.ClassTree.RankOf("runedancer_r1c0"), Is.EqualTo(3));
            Assert.That(loop.State.ClassTree.RankOf("runedancer_r1c1"), Is.Zero);
            Assert.That(loop.State.OtherPreset.ClassTree.RankOf("runedancer_r1c1"), Is.EqualTo(2));
        }

        [Test]
        public void ChangingTheClassGivesTheTreeOfTheNewClassWithAllPoints()
        {
            var loop = Huscarl();
            Raise(loop, "r1c0", 4);

            Assert.That(loop.ChangeClassFree(HeroClass.Ghostcaptain), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.State.ClassTree.TreeId, Is.EqualTo("ghostcaptain"));
            Assert.That(loop.State.ClassTree.PointsSpent, Is.Zero);
            Assert.That(loop.State.TreePointsLeft, Is.EqualTo(loop.State.TreePointsEarned));

            Assert.That(loop.ChangeClassPaid(HeroClass.Runedancer), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.State.ClassTree.PointsSpent, Is.Zero);
            Assert.That(loop.State.TreePointsLeft, Is.EqualTo(loop.State.TreePointsEarned));
        }

        [Test]
        public void TheSaveKeepsRanksTalentsAndThePlan()
        {
            var loop = Huscarl(60);
            Raise(loop, "r1c4", 5);
            Raise(loop, "r1c5", 5);
            Raise(loop, "r1c6", 5);
            Raise(loop, "r1c7", 5);
            loop.ChooseMajorTalent(20, "runedancer_m20_2");

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), TestWorld.Real).State;
            var again = new GameLoop(TestWorld.Real, new FakeInputSource(), loaded, new StatSheet());

            Assert.That(loaded.ClassTree.RankOf("runedancer_r1c4"), Is.EqualTo(5));
            Assert.That(loaded.ClassTree.Majors[20], Is.EqualTo("runedancer_m20_2"));
            Assert.That(loaded.ClassTree.Plan.Count, Is.EqualTo(20));
            Assert.That(again.State.TreePointsSpent, Is.EqualTo(20));
            Assert.That(again.State.TreePointsEarned, Is.EqualTo(loop.State.TreePointsEarned));
            Assert.That(SaveCodec.CurrentVersion, Is.EqualTo(15));
        }

        private static StatId TreeStat(string name) => new StatId(name);
    }
}
