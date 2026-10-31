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
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState { Class = HeroClass.Viking, HeroLevel = level }, new StatSheet());
            loop.Weapons.Unequip(WeaponHand.Right);
            loop.Levels.SyncTreePoints();
            loop.RebuildTree();
            return loop;
        }

        private static void Raise(GameLoop loop, string node, int times)
        {
            for (var i = 0; i < times; i++)
                Assert.That(loop.RaiseNode("viking_" + node), Is.EqualTo(RankResult.Done), node);
        }

        [Test]
        public void HuscarlPlaysTheNewTreeAndTheOldOneIsClosed()
        {
            var loop = Huscarl();

            Assert.That(loop.ClassTreeDef, Is.Not.Null);
            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.State.TreePointsLeft, Is.EqualTo(loop.State.Tree.PointsEarned));
            var oldNode = TestWorld.Real.Tree.Nodes.First(node => node.Type == TreeNodeType.Small);
            Assert.That(loop.AllocateNode(oldNode.Id), Is.EqualTo(AllocationResult.Locked));
            Assert.That(loop.RaiseNode("viking_heavy_swing"), Is.EqualTo(RankResult.RowClosed));
            Raise(loop, "axe", 5);
            Assert.That(loop.RaiseNode("viking_heavy_swing"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.State.TreePointsSpent, Is.EqualTo(6));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(118).Within(1e-9), "axe ×3 lines 12% and the swing 6%");
        }

        [Test]
        public void TakingBackARankIsFreeUntilTheFirstLimitAndCostsGoldAfter()
        {
            var loop = Huscarl();
            Raise(loop, "axe", 2);

            Assert.That(loop.ClassTreeResetFree, Is.True);
            Assert.That(loop.LowerCost("viking_axe"), Is.EqualTo(BigNumber.Zero));
            Assert.That(loop.LowerNode("viking_axe"), Is.EqualTo(RefundResult.Refunded));

            loop.State.GatesCleared.Add(10_000);
            loop.State.Gold = BigNumber.Zero;
            Assert.That(loop.LowerCost("viking_axe")!.Value.Sign, Is.GreaterThan(0));
            Assert.That(loop.LowerNode("viking_axe"), Is.EqualTo(RefundResult.NotEnoughGold));
            loop.State.Gold = BigNumber.FromDouble(1e9);
            Assert.That(loop.LowerNode("viking_axe"), Is.EqualTo(RefundResult.Refunded));
            Assert.That(loop.State.ClassTree.RankOf("viking_axe"), Is.Zero);
            Assert.That(loop.LowerNode("viking_axe"), Is.EqualTo(RefundResult.NotAllocated));
        }

        [Test]
        public void AFreeResetAndFillBringTheSameBuildBack()
        {
            var loop = Huscarl();
            Raise(loop, "wrath", 5);
            Raise(loop, "blood_boils", 3);
            Raise(loop, "tempered_eye", 2);
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
            Raise(loop, "tempered_eye", 5);
            Raise(loop, "battle_trance", 5);
            Raise(loop, "hirdman_spoils", 5);
            Raise(loop, "challenge", 4);

            Assert.That(loop.ChooseMajorTalent(20, "viking_cool_head"), Is.EqualTo(RankResult.UnknownMajor), "Cool Head is on the 50 mark");
            Assert.That(loop.ChooseMajorTalent(20, "viking_smoulder"), Is.EqualTo(RankResult.MarkNotReached));
            var resource = loop.Hero.Value(CombatStats.ResourceDecay, 1);
            Raise(loop, "war_cry", 1);
            Assert.That(loop.ChooseMajorTalent(20, "viking_smoulder"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.ResourceDecay, 1), Is.LessThan(resource));
            Assert.That(loop.Hero.Value(TreeStat("strength"), 0), Is.EqualTo(3), "two bar steps: +3 Strength and +2% Wrath gain");
        }

        [Test]
        public void AMajorTalentChangesOnlyOutsideABossFight()
        {
            var loop = Huscarl(60);
            Raise(loop, "tempered_eye", 5);
            Raise(loop, "battle_trance", 5);
            Raise(loop, "hirdman_spoils", 5);
            Raise(loop, "challenge", 5);
            loop.State.SpawnEnemy(null, Game.Core.Content.EnemyType.Boss, BigNumber.FromDouble(1e9));

            Assert.That(loop.ChooseMajorTalent(20, "viking_smoulder"), Is.EqualTo(RankResult.Done), "the first choice is a new talent, like a new rank");
            Assert.That(loop.ChooseMajorTalent(20, "viking_smoulder"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.ChooseMajorTalent(20, "viking_warm_up"), Is.EqualTo(RankResult.InBossFight));
            Assert.That(loop.State.ClassTree.Majors[20], Is.EqualTo("viking_smoulder"));

            loop.State.SpawnEnemy(null, Game.Core.Content.EnemyType.Normal, BigNumber.FromDouble(1e9));
            Assert.That(loop.ChooseMajorTalent(20, "viking_warm_up"), Is.EqualTo(RankResult.Done));
        }

        [Test]
        public void EachPresetKeepsItsOwnClassTree()
        {
            var loop = Huscarl();
            Raise(loop, "axe", 3);

            loop.SwitchPreset();
            Assert.That(loop.State.TreePointsSpent, Is.Zero);
            Raise(loop, "wrath", 2);
            loop.SwitchPreset();

            Assert.That(loop.State.ClassTree.RankOf("viking_axe"), Is.EqualTo(3));
            Assert.That(loop.State.ClassTree.RankOf("viking_wrath"), Is.Zero);
            Assert.That(loop.State.OtherPreset.ClassTree.RankOf("viking_wrath"), Is.EqualTo(2));
        }

        [Test]
        public void ChangingTheClassGivesTheTreeOfTheNewClassWithAllPoints()
        {
            var loop = Huscarl();
            Raise(loop, "axe", 4);

            Assert.That(loop.ChangeClassFree(HeroClass.Captain), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.ClassTreeActive, Is.False, "Sea Wolf still plays the old tree");
            Assert.That(loop.State.TreePointsLeft, Is.EqualTo(loop.State.Tree.PointsEarned));

            Assert.That(loop.ChangeClassPaid(HeroClass.Viking), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.State.ClassTree.PointsSpent, Is.Zero);
            Assert.That(loop.State.TreePointsLeft, Is.EqualTo(loop.State.Tree.PointsEarned));
        }

        [Test]
        public void TheSaveKeepsRanksTalentsAndThePlanAndOldTreeNodesAreRefunded()
        {
            var loop = Huscarl(60);
            Raise(loop, "tempered_eye", 5);
            Raise(loop, "battle_trance", 5);
            Raise(loop, "hirdman_spoils", 5);
            Raise(loop, "challenge", 5);
            loop.ChooseMajorTalent(20, "viking_warm_up");
            loop.State.Tree.Restore(TestWorld.Real.Tree.Nodes.First(node => node.Type == TreeNodeType.Small).Id, null, null);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), TestWorld.Real).State;
            var again = new GameLoop(TestWorld.Real, new FakeInputSource(), loaded, new StatSheet());

            Assert.That(loaded.ClassTree.RankOf("viking_tempered_eye"), Is.EqualTo(5));
            Assert.That(loaded.ClassTree.Majors[20], Is.EqualTo("viking_warm_up"));
            Assert.That(loaded.ClassTree.Plan.Count, Is.EqualTo(20));
            Assert.That(again.State.Tree.PointsSpent, Is.Zero, "a node of the old tree is dropped and its point comes back");
            Assert.That(SaveCodec.CurrentVersion, Is.EqualTo(13));
        }

        private static StatId TreeStat(string name) => new StatId(name);
    }
}
