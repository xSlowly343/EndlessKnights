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
        private static GameLoop Real(GameState state, StatSheet? hero = null) =>
            new GameLoop(TestWorld.Real, new FakeInputSource(), state, hero ?? new StatSheet());

        [Test]
        public void TypistStartGivesKeyboardDamageAndFocusGrowth()
        {
            var loop = Real(new GameState { Class = HeroClass.Typist });

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(115).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.FocusGain, 1), Is.EqualTo(1.10).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Click), Is.EqualTo(100));
        }

        [Test]
        public void BardStartGivesStrongerLongerAurasAndClickDamage()
        {
            var loop = Real(new GameState { Class = HeroClass.Bard });

            Assert.That(loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura), Is.EqualTo(1.10).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.AuraDuration, 1, StatTags.Aura), Is.EqualTo(1.10).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Click), Is.EqualTo(110).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100));
        }

        [Test]
        public void HermitStartRewardsIdleAndOfflinePlay()
        {
            var loop = Real(new GameState { Tree = new TreeAllocation("hermit") });

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Idle), Is.EqualTo(125).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.OfflineGold, 1), Is.EqualTo(1.15).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.IdleStartSeconds, 0, StatTags.Idle), Is.EqualTo(-5));
        }

        [Test]
        public void OnlyTheOwnStartGivesItsPassive()
        {
            var tree = TestWorld.Real.Tree;
            var typist = new TreeAllocation("typist").Modifiers(tree);

            Assert.That(typist, Is.EquivalentTo(tree.StartOf("typist")!.Modifiers));
        }

        [Test]
        public void EveryClassStartHasItsOwnPassive()
        {
            var starts = TestWorld.Real.Tree.Nodes.Where(node => node.Type == TreeNodeType.Start).ToList();

            Assert.That(starts.All(start => start.Modifiers.Count >= 2), Is.True);
            Assert.That(starts.Select(start => string.Join(",", start.Modifiers.Select(m => m.Stat.Name))).Distinct().Count(), Is.EqualTo(starts.Count));
        }

        [Test]
        public void TreeOffersManyDifferentEffects()
        {
            var stats = TestWorld.Real.Tree.Nodes.SelectMany(node => node.Modifiers.Concat(node.Options.SelectMany(option => option)))
                .Select(modifier => modifier.Stat.Name + "/" + modifier.Tags).Distinct().Count();

            Assert.That(stats, Is.GreaterThanOrEqualTo(25));
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
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Typist }), hero);
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
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var strike = loop.Events[0];
            Assert.That((strike.Flags & HitFlags.ComboStrike) != 0, Is.True);
            Assert.That(strike.Amount.ToDouble() / (TestWorld.Data.Combat.KeyDamage * loop.State.FocusMultiplier), Is.EqualTo(rules.ComboStrikeMultiplier + 0.5).Within(1e-9));
        }
    }
}
