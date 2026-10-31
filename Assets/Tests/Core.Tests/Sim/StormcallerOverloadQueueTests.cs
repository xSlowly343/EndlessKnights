using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StormcallerOverloadQueueTests
    {
        private static ClassResource Static => TestWorld.Data.Combat.Resources.For("stormweaver");
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;
        private static double Max => TestWorld.Data.Combat.Resources.Max;

        private static GameLoop Stormweaver(EnemyType enemy, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = HeroClass.Stormweaver }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, enemy, BigNumber.FromDouble(1e6));
            loop.State.BossTicksLeft = 100_000;
            loop.State.ClearQueue();
            loop.Events.Clear();
            return loop;
        }

        [Test]
        public void TheDischargeHitsTheEnemyAndTheNextTwoInTheQueue()
        {
            Assert.That(TestWorld.Data.Combat.Classes.OverloadQueueTargets, Is.EqualTo(2));
            var loop = Stormweaver(EnemyType.Normal);
            loop.State.Resource = Max;

            loop.Tick();

            var strike = KeyDamage * Static.OverloadMultiplier;
            Assert.That(loop.Overload.Discharges, Is.EqualTo(1));
            Assert.That(loop.State.QueueDamage[0].ToDouble(), Is.EqualTo(strike).Within(1e-6));
            Assert.That(loop.State.QueueDamage[1].ToDouble(), Is.EqualTo(strike).Within(1e-6));
            if (loop.State.QueueDamage.Length > 2)
                Assert.That(loop.State.QueueDamage[2].ToDouble(), Is.Zero);
        }

        [Test]
        public void OverloadAtFromTheTreeDischargesBeforeTheBarIsFull()
        {
            var loop = Stormweaver(EnemyType.Normal, new Modifier(CombatStats.OverloadAt, ModifierKind.Flat, -5));
            loop.State.Resource = Max - 5;

            loop.Tick();

            Assert.That(loop.Overload.Discharges, Is.EqualTo(1));
        }

        [Test]
        public void OverloadIsStrongerOnABossAndAlwaysCritsWithTheTree()
        {
            var boss = Stormweaver(EnemyType.Boss, new Modifier(CombatStats.OverloadBoss, ModifierKind.Flat, 0.2));
            boss.State.Resource = Max;
            var hp = boss.State.EnemyHp;
            boss.Tick();
            Assert.That((hp - boss.State.EnemyHp).ToDouble() / (KeyDamage * Static.OverloadMultiplier), Is.EqualTo(1.2).Within(0.05));

            var crit = Stormweaver(EnemyType.Normal, new Modifier(CombatStats.OverloadCrit, ModifierKind.Flat, 1));
            crit.State.Resource = Max;
            crit.Tick();
            Assert.That(crit.State.Crits, Is.EqualTo(1));
        }
    }
}
