using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ChronistClockworkTests
    {
        private static ClassRules Rules => TestWorld.Data.Combat.Classes;

        private static GameLoop OnBoss(HeroClass heroClass, double resource, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.FromDouble(1e12));
            loop.State.BossTicksLeft = 900;
            loop.State.Resource = resource;
            return loop;
        }

        private static int TimerTicksSpent(GameLoop loop, int ticks)
        {
            var before = loop.State.BossTicksLeft;
            for (var i = 0; i < ticks; i++)
            {
                loop.State.Resource = System.Math.Max(loop.State.Resource, 0);
                loop.Tick();
            }
            return before - loop.State.BossTicksLeft;
        }

        [Test]
        public void AboveFiftyTimeTheBossTimerRunsTenPercentSlower()
        {
            Assert.That(Rules.ClockworkAbove, Is.EqualTo(50));
            Assert.That(Rules.ClockworkSlow, Is.EqualTo(0.1));

            var slow = OnBoss(HeroClass.Clockmaker, 80);
            TimerTicksSpent(slow, 1);

            Assert.That(TimerTicksSpent(slow, 100), Is.EqualTo(90).Within(1));
        }

        [Test]
        public void AtFiftyTimeOrBelowTheTimerRunsAsUsual()
        {
            var loop = OnBoss(HeroClass.Clockmaker, 40);
            TimerTicksSpent(loop, 1);

            Assert.That(TimerTicksSpent(loop, 100), Is.EqualTo(100));
        }

        [Test]
        public void OtherClassesDoNotWindTheClock()
        {
            var loop = OnBoss(HeroClass.Ronin, 80);
            TimerTicksSpent(loop, 1);

            Assert.That(TimerTicksSpent(loop, 100), Is.EqualTo(100));
        }

        [Test]
        public void TogetherWithOtherSlowsTheTimerNeverGoesBelowAQuarter()
        {
            var nearFloor = OnBoss(HeroClass.Clockmaker, 80, new Modifier(CombatStats.BossTimerSpeed, ModifierKind.More, -0.73));
            var belowFloor = OnBoss(HeroClass.Clockmaker, 80, new Modifier(CombatStats.BossTimerSpeed, ModifierKind.More, -0.8));

            nearFloor.Tick();
            nearFloor.Tick();
            belowFloor.Tick();
            belowFloor.Tick();

            Assert.That(nearFloor.State.BossTimerRate, Is.EqualTo(Rules.ClockworkFloor).Within(1e-9));
            Assert.That(belowFloor.State.BossTimerRate, Is.EqualTo(0.2).Within(1e-9), "the clockwork never speeds the timer up");
        }

        [Test]
        public void ATreeLineStartsTheClockworkEarlier()
        {
            var loop = OnBoss(HeroClass.Clockmaker, 47, new Modifier(CombatStats.ClockworkAbove, ModifierKind.Flat, -5));
            loop.Tick();
            loop.Tick();

            Assert.That(loop.State.BossTimerRate, Is.EqualTo(0.9).Within(1e-9));
        }
    }
}
