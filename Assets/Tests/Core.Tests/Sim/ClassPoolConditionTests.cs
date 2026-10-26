using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ClassPoolConditionTests
    {
        private static double Damage(SkillTestHero hero, StatTags tags) => hero.Loop.Hero.Value(CombatStats.Damage, 100, tags);

        [Test]
        public void HeavyArrowHitsHarderRightAfterTheMouseMoved()
        {
            var still = SkillTestHero.Of(HeroClass.Amazon, "heavy_arrow");
            still.Cast(0);
            var moving = SkillTestHero.Of(HeroClass.Amazon, "heavy_arrow");
            moving.Input.MoveMouse(300);
            moving.Ticks(1);
            moving.Loop.Events.Clear();
            moving.State.Resource = 100;
            moving.Cast(0);

            Assert.That(still.DamageFrom(DamageSource.Skill), Is.EqualTo(still.SkillDamage("heavy_arrow", 10)).Within(1e-6));
            Assert.That(moving.DamageFrom(DamageSource.Skill), Is.EqualTo(moving.SkillDamage("heavy_arrow", 14)).Within(1e-6));
        }

        [Test]
        public void TheMouseCountsAsStillTwoSecondsAfterItStops()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "heavy_arrow");
            amazon.Input.MoveMouse(300);
            amazon.Ticks(1);
            amazon.Ticks(20);
            amazon.Loop.Events.Clear();
            amazon.State.Resource = 100;

            amazon.Cast(0);

            Assert.That(amazon.DamageFrom(DamageSource.Skill), Is.EqualTo(amazon.SkillDamage("heavy_arrow", 10)).Within(1e-6));
        }

        [Test]
        public void DollInThePocketAddsClickDamageWhileACurseHangs()
        {
            var plain = SkillTestHero.Of(HeroClass.Voodoo, "evil_eye");
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "evil_eye", "doll_in_the_pocket");
            voodoo.Ticks(1);
            plain.Ticks(1);
            var plainBefore = Damage(plain, StatTags.Click);
            var before = Damage(voodoo, StatTags.Click) / plainBefore;

            voodoo.Cast(0);
            plain.Cast(0);
            voodoo.Ticks(1);
            plain.Ticks(1);

            Assert.That(Damage(voodoo, StatTags.Click) / Damage(plain, StatTags.Click) - before, Is.EqualTo(10 / plainBefore).Within(1e-9));
        }

        [Test]
        public void SaltyTricorneAddsMouseDamageOnceTwoCannonsStand()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain, "salty_tricorne", "cannon_by_name");
            captain.Ticks(1);
            var before = Damage(captain, StatTags.Mouse);
            captain.Cast(1);
            captain.Ticks(1);
            Assert.That(Damage(captain, StatTags.Mouse), Is.EqualTo(before).Within(1e-6));

            captain.Ticks(50);
            captain.State.Resource = 100;
            captain.Cast(1);
            captain.Ticks(1);

            Assert.That(Damage(captain, StatTags.Mouse) - before, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void SmellMaskAddsPoisonDamageFromTwentyStacks()
        {
            var doctor = SkillTestHero.Of(HeroClass.PlagueDoctor, "smell_mask");
            doctor.Ticks(1);
            var before = Damage(doctor, StatTags.DoT);

            doctor.State.PoisonStacks = 20;
            doctor.Ticks(1);

            Assert.That(Damage(doctor, StatTags.DoT) - before, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void ShaftCallusesAddKeyDamageWhileAnAuraStands()
        {
            var lone = SkillTestHero.Of(HeroClass.Bannerbearer, "shaft_calluses");
            lone.Ticks(2);
            var withAura = SkillTestHero.Of(HeroClass.Bannerbearer, "shaft_calluses", "blueprint");
            withAura.Ticks(2);

            Assert.That(Damage(withAura, StatTags.Keyboard) - Damage(lone, StatTags.Keyboard), Is.EqualTo(25).Within(1e-6));
        }

        [Test]
        public void BlindfoldHabitAddsMouseDamageWhileTheOracleReads()
        {
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "blindfold_habit");
            oracle.Ticks(1);
            var before = Damage(oracle, StatTags.Mouse);

            for (var i = 0; i < 60; i++)
            {
                oracle.Input.MoveMouse(50);
                oracle.Ticks(1);
            }

            Assert.That(oracle.State.Reading, Is.True);
            Assert.That(Damage(oracle, StatTags.Mouse) - before, Is.EqualTo(10).Within(1e-6));
        }
    }
}
