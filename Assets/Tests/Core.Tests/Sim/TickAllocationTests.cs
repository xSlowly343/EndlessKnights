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

        private static readonly string[] RunedancerSkills = { "runedancer_hit_1", "runedancer_strong_1", "runedancer_aura_1", "runedancer_queue_1", "runedancer_auto_1", "runedancer_aura_2", "runedancer_hit_2" };
        private static readonly string[] MinstrelSkills = { "minstrel_hit_1", "minstrel_strong_1", "minstrel_aura_1", "minstrel_queue_1", "minstrel_auto_1", "minstrel_aura_2", "minstrel_hit_2" };
        private static readonly string[] DuelistSkills = { "duelist_hit_1", "duelist_strong_1", "duelist_aura_1", "duelist_queue_1", "duelist_auto_1", "duelist_aura_2", "duelist_hit_2" };
        private static readonly string[] GhostcaptainSkills = { "ghostcaptain_hit_1" };
        private static readonly string[] AlchemistSkills = { "alchemist_hit_1" };
        private static readonly string[] DowserSkills = { "dowser_hit_1" };
        private static readonly string[] SoulbinderSkills = { "soulbinder_hit_2" };

        private static readonly string[] CommonSkills =
        {
            "through_stride", "key_drum", "overheat", "no_breather", "quick_stride", "steady_pace",
            "hurricane", "long_road", "comet_trail", "tailwind",
            "mark", "weak_spot", "execute", "exposure", "boss_hunt", "ricochet", "lucky_coin", "composure", "freeze_frame",
            "lull", "golden_fever", "second_wind", "echo", "caravan_halt", "blade_on_watch", "shoulder_to_shoulder", "prokhors_ledger", "rift_crystal"
        };

        private static (GameLoop Loop, FakeInputSource Input) ClassSkillHero(HeroClass heroClass) =>
            SkillHero(heroClass, ClassSkills(heroClass));

        private static string[] ClassSkills(HeroClass heroClass) =>
            heroClass switch
            {
                HeroClass.Runedancer => RunedancerSkills,
                HeroClass.Duelist => DuelistSkills,
                HeroClass.Ghostcaptain => GhostcaptainSkills,
                HeroClass.Alchemist => AlchemistSkills,
                HeroClass.Dowser => DowserSkills,
                HeroClass.Soulbinder => SoulbinderSkills,
                _ => MinstrelSkills
            };

        private static (GameLoop Loop, FakeInputSource Input) SkillHero(HeroClass heroClass, string[] skills)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1000));
            var loop = TestWorld.Loop(input, TestWorld.WithAllSkillsOpen(new GameState { Class = heroClass }), hero);
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
            var (warmUp, warmUpInput) = SkillHero(HeroClass.Duelist, CommonSkills);
            FightWithEveryInput(warmUp, warmUpInput, 3000);
            var (loop, input) = SkillHero(HeroClass.Duelist, CommonSkills);
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

        [TestCase(HeroClass.Runedancer)]
        [TestCase(HeroClass.Duelist)]
        [TestCase(HeroClass.Minstrel)]
        [TestCase(HeroClass.Ghostcaptain)]
        [TestCase(HeroClass.Alchemist)]
        [TestCase(HeroClass.Dowser)]
        [TestCase(HeroClass.Soulbinder)]
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

        [TestCase(HeroClass.Duelist)]
        [TestCase(HeroClass.Minstrel)]
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
