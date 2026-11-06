using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PierceTests
    {
        private static (GameLoop Loop, FakeInputSource Input) Hero(double damageMore, GameData? data = null)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, damageMore));
            var loop = new GameLoop(data ?? TestWorld.WithPierce, input, new GameState(), hero);
            loop.Tick();
            loop.Events.Clear();
            return (loop, input);
        }

        private static int KillsFromOneHit(GameLoop loop, FakeInputSource input)
        {
            input.PressKeys(1);
            loop.Tick();
            var kills = loop.Events.CountOf(GameEventKind.EnemyKilled);
            loop.Events.Clear();
            return kills;
        }

        [Test]
        public void EveryClassPiercesWithTheBaseShare()
        {
            Assert.That(TestWorld.Real.Combat.BasePierce, Is.EqualTo(0.5));
        }

        [Test]
        public void AHugeHitGoesThroughEveryEnemyAndEveryWaveButStopsAtTheBoss()
        {
            var (loop, input) = Hero(1e12);
            var enemies = TestWorld.WithPierce.Stages.EnemiesOnStage(1);
            Assert.That(KillsFromOneHit(loop, input), Is.EqualTo(enemies), "all four waves fall to one hit");
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss), "the overflow does not touch the boss");
            Assert.That(loop.State.EnemyHp, Is.EqualTo(loop.State.EnemyMaxHp));
            Assert.That(loop.State.PierceDamage.Sign, Is.Zero);
        }

        [Test]
        public void WithoutPierceOneHitKillsOneEnemy()
        {
            var (loop, input) = Hero(1e12, TestWorld.Data);
            Assert.That(KillsFromOneHit(loop, input), Is.EqualTo(1));
        }

        [Test]
        public void TheOverflowWoundsTheNextEnemy()
        {
            var (loop, input) = Hero(0);
            var full = loop.State.EnemyHp;
            input.PressKeys(1);
            loop.Tick();
            var hit = full - loop.State.EnemyHp;
            Assume.That(hit.Sign, Is.GreaterThan(0));
            Assume.That(loop.State.EnemyAlive, Is.True);
            loop.State.EnemyHp = hit * 0.4;
            loop.Events.Clear();
            Assert.That(KillsFromOneHit(loop, input), Is.EqualTo(1));
            var taken = (loop.State.EnemyMaxHp - loop.State.EnemyHp) / hit;
            Assert.That(taken.ToDouble(), Is.EqualTo(0.6 * TestWorld.WithPierce.Combat.BasePierce).Within(0.05), "the next enemy takes the base share of what was left of the hit");
        }
    }
}
