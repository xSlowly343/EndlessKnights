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

        [TestCase(HeroClass.Duelist, "duelist_katana")]
        [TestCase(HeroClass.Windshot, "windshot_short_bow")]
        [TestCase(HeroClass.Shardcaller, "shardcaller_storm_rod")]
        [TestCase(HeroClass.Sandwalker, "sandwalker_scimitar")]
        [TestCase(HeroClass.Minstrel, "minstrel_electric_guitar")]
        [TestCase(HeroClass.Dreamcatcher, "dreamcatcher_holy_orb")]
        [TestCase(HeroClass.Lanternbearer, "lanternbearer_war_standard")]
        [TestCase(HeroClass.Whisperer, "whisperer_whisperer_doll")]
        [TestCase(HeroClass.Clockwright, "clockwright_pendulum")]
        [TestCase(HeroClass.Alchemist, "alchemist_syringe_cane")]
        [TestCase(HeroClass.Dowser, "dowser_burner")]
        [TestCase(HeroClass.Soulbinder, "soulbinder_shovel")]
        [TestCase(HeroClass.Ghostcaptain, "ghostcaptain_pistol")]
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
        public void HuscarlOnTheNewTreeHasNoStartPassiveAndGrowsFromItsNodes()
        {
            var loop = Real(new GameState { Class = HeroClass.Runedancer, HeroLevel = 10 });
            loop.Levels.SyncTreePoints();

            Assert.That(loop.State.ClassTreeActive, Is.True);
            Assert.That(loop.Hero.Value(CombatStats.ResourceGain, 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100).Within(1e-9));
            Assert.That(loop.RaiseNode("runedancer_axe"), Is.EqualTo(RankResult.Done));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(104).Within(1e-9));
        }

        [Test]
        public void LongSleepRaisesTheOfflineCap()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.OfflineCapHours, ModifierKind.Flat, 1));

            var result = new OfflineCalculator(TestWorld.Data, hero).Simulate(new GameState(), 48 * 3600, BigNumber.FromDouble(50), fightGuardians: false);

            Assert.That(result.Seconds, Is.EqualTo(TestWorld.Data.Combat.Offline.CapSeconds + 3600));
        }

        [Test]
        public void StreakNodesStretchTheComboAndHardenItsStrike()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.ComboStrikeMultiplier, ModifierKind.Flat, 0.5));
            hero.Add(new Modifier(CombatStats.ComboTimeoutSeconds, ModifierKind.Flat, 0.5));
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Duelist }, hero);
            loop.State.SpawnEnemy(null, Game.Core.Content.EnemyType.Normal, BigNumber.Create(1, 40));
            var rules = TestWorld.Data.Combat.Classes;

            for (var i = 0; i < 3; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            for (var i = 0; i < (rules.ComboTimeoutSeconds + 0.4) * GameLoop.TicksPerSecond; i++)
                loop.Tick();
            Assert.That(loop.State.Combo, Is.EqualTo(3));

            loop.State.Combo = rules.ComboStrikeEvery - 1;
            loop.State.ComboTowardStrike = rules.ComboStrikeEvery - 1;
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var strike = loop.Events[0];
            Assert.That((strike.Flags & HitFlags.ComboStrike) != 0, Is.True);
            Assert.That(strike.Amount.ToDouble() / (TestWorld.Data.Combat.KeyDamage * loop.State.FocusMultiplier), Is.EqualTo(rules.ComboStrikeMultiplier + 0.5).Within(1e-9));
        }
    }
}
