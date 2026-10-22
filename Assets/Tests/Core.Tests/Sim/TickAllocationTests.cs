using System;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TickAllocationTests
    {
        private static void Fight(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                input.PressKeys(1 + tick % 2);
                input.ClickLeft(tick % 3 == 0 ? 1 : 0);
                input.MoveMouse(150);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        private static void RequireAllocationCounter()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            var after = GC.GetAllocatedBytesForCurrentThread();
            if (after - before < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
        }

        private static (GameLoop Loop, FakeInputSource Input) StrongHeroBeforeGuardian(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1000));
            var start = TestWorld.Data.Stages.BiomeLength - 5;
            return (TestWorld.Loop(input, new GameState { Class = heroClass, Stage = start, HighestStage = start }, hero), input);
        }

        [TestCase(HeroClass.Typist)]
        [TestCase(HeroClass.Bard)]
        public void CombatTicksDoNotAllocate(HeroClass heroClass)
        {
            RequireAllocationCounter();
            var (warmUp, warmUpInput) = StrongHeroBeforeGuardian(heroClass);
            Fight(warmUp, warmUpInput, 3000);
            var (loop, input) = StrongHeroBeforeGuardian(heroClass);
            Fight(loop, input, 20);
            var startStage = loop.State.Stage;

            var before = GC.GetAllocatedBytesForCurrentThread();
            Fight(loop, input, 3000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(startStage, Is.LessThan(TestWorld.Data.Stages.BiomeLength));
            Assert.That(loop.State.Stage, Is.GreaterThan(2 * TestWorld.Data.Stages.BiomeLength));
            Assert.That(allocated, Is.EqualTo(0));
        }
    }
}
