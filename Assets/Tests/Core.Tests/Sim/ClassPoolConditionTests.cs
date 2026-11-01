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
            var still = SkillTestHero.Of(HeroClass.Windshot, "heavy_arrow");
            still.Cast(0);
            var moving = SkillTestHero.Of(HeroClass.Windshot, "heavy_arrow");
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
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "heavy_arrow");
            windshot.Input.MoveMouse(300);
            windshot.Ticks(1);
            windshot.Ticks(20);
            windshot.Loop.Events.Clear();
            windshot.State.Resource = 100;

            windshot.Cast(0);

            Assert.That(windshot.DamageFrom(DamageSource.Skill), Is.EqualTo(windshot.SkillDamage("heavy_arrow", 10)).Within(1e-6));
        }

        [Test]
        public void ClearDayHitsHarderWhileTheOracleReads()
        {
            var dreamcatcher = SkillTestHero.Of(HeroClass.Dreamcatcher, "clear_day");
            dreamcatcher.Cast(0);
            var plain = dreamcatcher.DamageFrom(DamageSource.Skill);
            dreamcatcher.State.Skills[0].CooldownTicks = 0;
            dreamcatcher.State.Resource = 100;
            for (var i = 0; i < 60; i++)
            {
                dreamcatcher.Input.MoveMouse(50);
                dreamcatcher.Ticks(1);
            }
            dreamcatcher.Loop.Events.Clear();

            dreamcatcher.Cast(0);

            Assert.That(dreamcatcher.State.Reading, Is.True);
            Assert.That(dreamcatcher.DamageFrom(DamageSource.Skill), Is.EqualTo(plain * 1.4).Within(plain * 1e-6));
        }
    }
}
