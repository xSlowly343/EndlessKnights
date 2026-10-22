using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BossTimerTests
    {
        private static int TimerTicks => (int)System.Math.Round(TestWorld.Data.Stages.BossTimerSeconds * GameLoop.TicksPerSecond);

        private static (GameLoop Loop, FakeInputSource Input, StatSheet Hero) LoopAtBoss()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            var strong = new Modifier(CombatStats.Damage, ModifierKind.More, 1e12);
            hero.Add(strong);
            var loop = TestWorld.Loop(input, hero: hero);
            for (var i = 0; i < TestWorld.Data.Stages.EnemiesPerStage; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            hero.Remove(strong);
            loop.Events.Clear();
            return (loop, input, hero);
        }

        private static void Idle(GameLoop loop, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                loop.Tick();
        }

        [Test]
        public void BossStartsWithFullTimer()
        {
            var (loop, _, _) = LoopAtBoss();

            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(loop.State.BossSecondsLeft, Is.EqualTo(TestWorld.Data.Stages.BossTimerSeconds).Within(0.11));
        }

        [Test]
        public void TimerRunsOutAndStageGoesToFarming()
        {
            var (loop, _, _) = LoopAtBoss();
            var ticksLeft = loop.State.BossTicksLeft;

            Idle(loop, ticksLeft - 1);
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(loop.Events.CountOf(GameEventKind.BossFailed), Is.EqualTo(0));

            Idle(loop, 1);
            Assert.That(loop.Events.CountOf(GameEventKind.BossFailed), Is.EqualTo(1));
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Normal));
            Assert.That(loop.State.Farming, Is.True);
            Assert.That(loop.State.Stage, Is.EqualTo(1));
            Assert.That(loop.State.BossFailsInARow, Is.EqualTo(1));
        }

        [Test]
        public void FarmingKillsRetryTheBossAfterConfiguredCount()
        {
            var (loop, input, hero) = LoopAtBoss();
            Idle(loop, loop.State.BossTicksLeft);
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));

            for (var i = 0; i < TestWorld.Data.Stages.BossRetryAfterKills - 1; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Normal));
            Assert.That(loop.State.KillsOnStage, Is.EqualTo(TestWorld.Data.Stages.EnemiesPerStage));

            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(loop.State.Farming, Is.False);
            Assert.That(loop.State.BossTicksLeft, Is.EqualTo(TimerTicks));
        }

        [Test]
        public void ManualRetryStartsBossAtOnce()
        {
            var (loop, _, _) = LoopAtBoss();
            Idle(loop, loop.State.BossTicksLeft);

            Assert.That(loop.Stages.RequestBossFight(), Is.True);
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(loop.Stages.RequestBossFight(), Is.False);
        }

        [Test]
        public void ManualRetryIsRefusedWhenNotFarming()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Stages.RequestBossFight(), Is.False);
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Normal));
        }

        [Test]
        public void KillingBossInTimeResetsFailCounter()
        {
            var (loop, input, hero) = LoopAtBoss();
            Idle(loop, loop.State.BossTicksLeft);
            loop.Stages.RequestBossFight();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Stage, Is.EqualTo(2));
            Assert.That(loop.State.BossFailsInARow, Is.EqualTo(0));
            Assert.That(loop.State.Farming, Is.False);
        }

        [Test]
        public void TimerDoesNotRunOnNormalEnemies()
        {
            var loop = TestWorld.Loop();

            Idle(loop, 2000);

            Assert.That(loop.Events.CountOf(GameEventKind.BossFailed), Is.EqualTo(0));
            Assert.That(loop.State.BossTicksLeft, Is.EqualTo(0));
        }
    }
}
