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
                if (tick % 37 == 0)
                    input.PressEnter();
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
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass, Stage = start, HighestStage = start }, hero);
            loop.Skills.Equip(TestWorld.Data.Gems["stenography"], TestWorld.Data.Gems["cast_on_timer"]);
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);
            loop.Skills.Equip(TestWorld.Data.Gems["lull"]);
            return (loop, input);
        }

        private static readonly string[] VikingSkills = { "shield_bash", "odins_fury", "battle_cry", "throwing_axe", "blood_harvest", "shield_wall", "ragnarok_roar" };
        private static readonly string[] MetalheadSkills = { "song", "guitar_solo", "power_chord", "ballad", "mosh_pit", "amp_eleven", "stage_dive" };

        private static (GameLoop Loop, FakeInputSource Input) ClassSkillHero(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1000));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            foreach (var skill in heroClass == HeroClass.Viking ? VikingSkills : MetalheadSkills)
                loop.Skills.Equip(TestWorld.Data.Gems[skill]);
            return (loop, input);
        }

        private static void FightCastingEverything(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                for (var slot = 0; slot < loop.State.Skills.Count; slot++)
                    loop.RequestCast(slot);
                input.PressKeys(3);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        [TestCase(HeroClass.Viking)]
        [TestCase(HeroClass.Metalhead)]
        public void ClassSkillTicksDoNotAllocate(HeroClass heroClass)
        {
            RequireAllocationCounter();
            var (warmUp, warmUpInput) = ClassSkillHero(heroClass);
            FightCastingEverything(warmUp, warmUpInput, 3000);
            var (loop, input) = ClassSkillHero(heroClass);
            FightCastingEverything(loop, input, 600);

            var before = GC.GetAllocatedBytesForCurrentThread();
            FightCastingEverything(loop, input, 3000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(loop.State.Stage, Is.GreaterThan(TestWorld.Data.Stages.BiomeLength));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [TestCase(HeroClass.Ronin)]
        [TestCase(HeroClass.Metalhead)]
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
