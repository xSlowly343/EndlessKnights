using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CombatSystemTests
    {
        private sealed class KillCounter : IKillHandler
        {
            public int Kills;
            public void OnEnemyKilled() => Kills++;
        }

        private static (GameLoop Loop, FakeInputSource Input) LoopWithEnemy(double hp, StatSheet? hero = null)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(hp));
            loop.Events.Clear();
            return (loop, input);
        }

        [Test]
        public void KeyPressDamagesEnemy()
        {
            var (loop, input) = LoopWithEnemy(1000);

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.EnemyHp.ToDouble(), Is.EqualTo(1000 - TestWorld.Data.Combat.KeyDamage).Within(1e-9));
            Assert.That(loop.Events.CountOf(GameEventKind.Damage), Is.EqualTo(1));
            Assert.That(loop.Events[0].Source, Is.EqualTo(DamageSource.Keyboard));
        }

        [Test]
        public void ClicksAndMouseHitWithTheirOwnDamage()
        {
            var (loop, input) = LoopWithEnemy(1000);
            var rules = TestWorld.Data.Combat;

            input.ClickLeft();
            input.MoveMouse((float)(rules.MousePxPerHit * 2));
            loop.Tick();

            var expected = 1000 - rules.ClickDamage - 2 * rules.MouseDamage;
            Assert.That(loop.State.EnemyHp.ToDouble(), Is.EqualTo(expected).Within(1e-9));
            Assert.That(loop.Events.Count, Is.EqualTo(3));
        }

        [Test]
        public void MouseDistanceCarriesOverBetweenTicks()
        {
            var (loop, input) = LoopWithEnemy(1000);
            var pxPerHit = TestWorld.Data.Combat.MousePxPerHit;

            input.MoveMouse((float)(pxPerHit * 0.6));
            loop.Tick();
            Assert.That(loop.Events.Count, Is.EqualTo(0));

            input.MoveMouse((float)(pxPerHit * 0.6));
            loop.Tick();
            Assert.That(loop.Events.Count, Is.EqualTo(1));
            Assert.That(loop.State.MouseCarryPx, Is.EqualTo(pxPerHit * 0.2).Within(1e-6));
        }

        [Test]
        public void RightAndMiddleClicksAreNotHits()
        {
            var (loop, input) = LoopWithEnemy(1000);

            input.ClickRight(3);
            input.ClickMiddle(2);
            loop.Tick();

            Assert.That(loop.Events.Count, Is.EqualTo(0));
        }

        [Test]
        public void ModifiersFromStatSheetApplyByTag()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1.0, StatTags.Keyboard));
            var (loop, input) = LoopWithEnemy(1000, hero);

            input.PressKeys(1);
            input.ClickLeft();
            loop.Tick();

            Assert.That(loop.Events[0].Amount.ToDouble(), Is.EqualTo(TestWorld.Data.Combat.KeyDamage * 2).Within(1e-9));
            Assert.That(loop.Events[1].Amount.ToDouble(), Is.EqualTo(TestWorld.Data.Combat.ClickDamage).Within(1e-9));
        }

        [Test]
        public void EnemyDiesAtZeroAndKillIsReported()
        {
            var (loop, input) = LoopWithEnemy(TestWorld.Data.Combat.KeyDamage * 2.5);
            var kills = new KillCounter();
            loop.Combat.OnKill(kills);

            input.PressKeys(5);
            loop.Tick();

            Assert.That(loop.State.EnemyAlive, Is.False);
            Assert.That(loop.State.EnemyHp, Is.EqualTo(BigNumber.Zero));
            Assert.That(kills.Kills, Is.EqualTo(1));
            Assert.That(loop.Events.CountOf(GameEventKind.Damage), Is.EqualTo(3));
        }

        [Test]
        public void CappedKeysGiveFractionalHitsThatAddUp()
        {
            var (loop, input) = LoopWithEnemy(1e9);

            for (var tick = 0; tick < 100; tick++)
            {
                input.PressKeys(2);
                loop.Tick();
            }

            var hitsInLastNineSeconds = 0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Tick >= 10)
                    hitsInLastNineSeconds++;
            }
            Assert.That(hitsInLastNineSeconds / 9.0, Is.EqualTo(System.Math.Sqrt(8 * 15)).Within(0.15));
        }
    }
}
