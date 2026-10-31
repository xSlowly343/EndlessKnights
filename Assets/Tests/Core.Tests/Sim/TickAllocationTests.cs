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
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 100000));
            var start = TestWorld.Data.Stages.BiomeLength - 5;
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass, Stage = start, HighestStage = start }, hero);
            loop.Skills.Equip(TestWorld.Data.Gems["through_stride"]);
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);
            loop.Skills.Equip(TestWorld.Data.Gems["lull"]);
            return (loop, input);
        }

        private static readonly string[] VikingSkills = { "iron_rim_bash", "no_mercy", "roar_from_the_door", "hatchet_toss", "forge_heat", "anvil", "long_winter" };
        private static readonly string[] MetalheadSkills = { "hearth_song", "louder", "nail_chord", "colossus_lullaby", "whole_hall", "underside_amp", "still_alive" };
        private static readonly string[] RoninSkills = { "thin_cut", "hundred_cuts", "no_rush", "kitchen_rhythm", "what_remains_of_the_debt", "sweep_over_the_queue", "slow_simmer" };
        private static readonly string[] CaptainSkills = { "cannon_by_name" };
        private static readonly string[] PlagueDoctorSkills = { "vial_from_belt" };
        private static readonly string[] MoonshinerSkills = { "fuse_bottle" };
        private static readonly string[] GravediggerSkills = { "call_a_colleague" };

        private static readonly string[] CommonSkills =
        {
            "through_stride", "key_drum", "overheat", "space_hammer", "no_breather", "step_back", "quick_stride", "double_grip", "steady_pace",
            "hurricane", "dash", "lasso", "pendulum_strike", "long_road", "comet_trail", "blade_whirl", "tailwind", "lightning_zigzag",
            "mark", "weak_spot", "execute", "exposure", "boss_hunt", "ricochet", "lucky_coin", "composure", "freeze_frame",
            "lull", "golden_fever", "second_wind", "echo", "caravan_halt", "blade_on_watch", "shoulder_to_shoulder", "prokhors_ledger", "rift_crystal"
        };

        private static (GameLoop Loop, FakeInputSource Input) ClassSkillHero(HeroClass heroClass) =>
            SkillHero(heroClass, ClassSkills(heroClass));

        private static string[] ClassSkills(HeroClass heroClass) =>
            heroClass switch
            {
                HeroClass.Viking => VikingSkills,
                HeroClass.Ronin => RoninSkills,
                HeroClass.Captain => CaptainSkills,
                HeroClass.PlagueDoctor => PlagueDoctorSkills,
                HeroClass.Moonshiner => MoonshinerSkills,
                HeroClass.Gravedigger => GravediggerSkills,
                _ => MetalheadSkills
            };

        private static (GameLoop Loop, FakeInputSource Input) SkillHero(HeroClass heroClass, string[] skills)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1000));
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
            foreach (var skill in skills)
                loop.Skills.Equip(TestWorld.Data.Gems[skill]);
            return (loop, input);
        }

        private static void FightWithEveryInput(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                for (var slot = 0; slot < loop.State.Skills.Count; slot++)
                    loop.RequestCast(slot);
                if (tick % 50 < 40)
                {
                    input.PressKeys(2);
                    input.PressSpace(tick % 4 == 0 ? 1 : 0);
                    input.PressCombo(tick % 5 == 0 ? 1 : 0);
                }
                input.MoveMouse(300);
                input.Flick(tick % 7 == 0 ? 1 : 0);
                input.Circle(0.3f);
                input.Reverse(tick % 3 == 0 ? 1 : 0);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        [Test]
        public void CommonSkillTicksDoNotAllocate()
        {
            RequireAllocationCounter();
            var (warmUp, warmUpInput) = SkillHero(HeroClass.Ronin, CommonSkills);
            FightWithEveryInput(warmUp, warmUpInput, 3000);
            var (loop, input) = SkillHero(HeroClass.Ronin, CommonSkills);
            FightWithEveryInput(loop, input, 2000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            FightWithEveryInput(loop, input, 3000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(loop.State.Stage, Is.GreaterThan(TestWorld.Data.Stages.BiomeLength));
            Assert.That(allocated, Is.EqualTo(0));
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
        [TestCase(HeroClass.Ronin)]
        [TestCase(HeroClass.Metalhead)]
        [TestCase(HeroClass.Captain)]
        [TestCase(HeroClass.PlagueDoctor)]
        [TestCase(HeroClass.Moonshiner)]
        [TestCase(HeroClass.Gravedigger)]
        public void ClassSkillTicksDoNotAllocate(HeroClass heroClass)
        {
            RequireAllocationCounter();
            var (warmUp, warmUpInput) = ClassSkillHero(heroClass);
            FightCastingEverything(warmUp, warmUpInput, 3000);
            var (loop, input) = ClassSkillHero(heroClass);
            FightCastingEverything(loop, input, 1200);

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
