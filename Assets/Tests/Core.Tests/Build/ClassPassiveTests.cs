using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassPassiveTests
    {
        private static GameLoop Real(GameState state, StatSheet? hero = null)
        {
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), state, hero ?? new StatSheet());
            loop.Weapons.Unequip(WeaponHand.Right);
            return loop;
        }

        [TestCase(HeroClass.Runedancer, "runedancer_r1c1")]
        [TestCase(HeroClass.Duelist, "duelist_r1c1")]
        [TestCase(HeroClass.Windshot, "windshot_r1c1")]
        [TestCase(HeroClass.Shardcaller, "shardcaller_r1c1")]
        [TestCase(HeroClass.Sandwalker, "sandwalker_r1c1")]
        [TestCase(HeroClass.Minstrel, "minstrel_r1c1")]
        [TestCase(HeroClass.Dreamcatcher, "dreamcatcher_r1c1")]
        [TestCase(HeroClass.Lanternbearer, "lanternbearer_r1c1")]
        [TestCase(HeroClass.Whisperer, "whisperer_r1c1")]
        [TestCase(HeroClass.Clockwright, "clockwright_r1c1")]
        [TestCase(HeroClass.Alchemist, "alchemist_r1c1")]
        [TestCase(HeroClass.Dowser, "dowser_r1c1")]
        [TestCase(HeroClass.Soulbinder, "soulbinder_r1c1")]
        [TestCase(HeroClass.Ghostcaptain, "ghostcaptain_r1c9")]
        [TestCase(HeroClass.Mothcaller, "mothcaller_r1c9")]
        [TestCase(HeroClass.Cartomancer, "cartomancer_r1c9")]
        [TestCase(HeroClass.Herbwitch, "herbwitch_r1c9")]
        [TestCase(HeroClass.Golemancer, "golemancer_r1c9")]
        public void AClassOnTheNewTreeHasNoStartPassiveAndGrowsFromItsNodes(HeroClass heroClass, string node)
        {
            var loop = Real(new GameState { Class = heroClass, HeroLevel = 10 });
            loop.Levels.SyncTreePoints();
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.State.TreePointsSpent, Is.Zero);
            Assert.That(loop.RaiseNode(node), Is.EqualTo(RankResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before + 4).Within(1e-9));
        }

        [Test]
        public void LongSleepRaisesTheOfflineCap()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.OfflineCapHours, ModifierKind.Flat, 1));

            var result = new OfflineCalculator(TestWorld.Data, hero).Simulate(new GameState(), 48 * 3600, BigNumber.FromDouble(50), fightGuardians: false);

            Assert.That(result.Seconds, Is.EqualTo(TestWorld.Data.Combat.Offline.CapSeconds + 3600));
        }
    }
}
