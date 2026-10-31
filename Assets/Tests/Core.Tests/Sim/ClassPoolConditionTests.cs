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
        public void ClearDayHitsHarderWhileTheOracleReads()
        {
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "clear_day");
            oracle.Cast(0);
            var plain = oracle.DamageFrom(DamageSource.Skill);
            oracle.State.Skills[0].CooldownTicks = 0;
            oracle.State.Resource = 100;
            for (var i = 0; i < 60; i++)
            {
                oracle.Input.MoveMouse(50);
                oracle.Ticks(1);
            }
            oracle.Loop.Events.Clear();

            oracle.Cast(0);

            Assert.That(oracle.State.Reading, Is.True);
            Assert.That(oracle.DamageFrom(DamageSource.Skill), Is.EqualTo(plain * 1.4).Within(plain * 1e-6));
        }
    }
}
