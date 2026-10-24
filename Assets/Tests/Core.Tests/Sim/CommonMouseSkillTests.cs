using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CommonMouseSkillTests
    {
        private static SkillTestHero Hero(params string[] skills) => SkillTestHero.Of(HeroClass.Ronin, skills);

        private static void Move(SkillTestHero hero, double px, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                hero.Input.MoveMouse((float)px);
                hero.Ticks(1);
            }
        }

        [Test]
        public void HurricaneHitsForEveryHundredPixelsAndTwiceForACircle()
        {
            var hero = Hero("hurricane");
            hero.Cast(0);

            Move(hero, 100, 5);
            hero.Input.Circle(1);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("hurricane", 5 * 0.4 + 2)).Within(1e-6));
        }

        [Test]
        public void HurricaneStopsWhenItEnds()
        {
            var hero = Hero("hurricane");
            hero.Cast(0);
            hero.Ticks(80);
            hero.Loop.Events.Clear();

            Move(hero, 100, 5);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void DashSendsAWaveForEachFlickUpToFive()
        {
            var hero = Hero("dash");
            hero.Cast(0);

            hero.Input.Flick(3);
            hero.Ticks(1);
            hero.Input.Flick(4);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("dash", 5 * 3)).Within(1e-6));
        }

        [Test]
        public void LassoWaitsForACircleThenCarriesOverkillToTheNextEnemy()
        {
            var hero = Hero("lasso");
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.Ticks(1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.OverkillCarry, 0), Is.EqualTo(0));

            hero.Input.Circle(0.6f);
            hero.Ticks(1);
            hero.Input.Circle(0.6f);
            hero.Ticks(1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.OverkillCarry, 0), Is.EqualTo(1));

            hero.Loop.Events.Clear();
            hero.KillCurrentEnemy();

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.DamageFrom(DamageSource.Keyboard)).Within(1e-6));
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.GreaterThan(0));
        }

        [Test]
        public void WithoutLassoOverkillIsLost()
        {
            var hero = Hero("lasso");

            hero.KillCurrentEnemy();

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void PendulumStrikeHitsOnEveryChangeOfDirection()
        {
            var hero = Hero("pendulum_strike");
            hero.Cast(0);

            hero.Input.Reverse(3);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("pendulum_strike", 1.5)).Within(1e-6));
        }

        [Test]
        public void LongRoadStacksDamageFromDistanceOverFiveMinutes()
        {
            var hero = Hero("long_road");

            Move(hero, 1000, 25);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.10).Within(1e-9));

            Move(hero, 1000, 100);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.25).Within(1e-9));

            hero.Ticks(3000);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1));
        }

        [Test]
        public void CometTrailExplodesForTheDistanceCoveredWhenItEnds()
        {
            var hero = Hero("comet_trail");
            hero.Cast(0);

            Move(hero, 500, 10);
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
            hero.Ticks(90);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("comet_trail", 10)).Within(1e-6));
        }

        [Test]
        public void BladeWhirlHitsWithEachOfThreeBladesPerCircle()
        {
            var hero = Hero("blade_whirl");
            hero.Cast(0);

            hero.Input.Circle(2);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("blade_whirl", 6)).Within(1e-6));
        }

        [Test]
        public void WindAtYourBackReservesAndCarriesHalfOfMouseOverkill()
        {
            var hero = Hero("tailwind");
            hero.Ticks(1);
            Assert.That(hero.State.ReservedResource, Is.EqualTo(15));

            hero.KillCurrentEnemy();
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));

            hero.Loop.Events.Clear();
            hero.State.EnemyHp = BigNumber.FromDouble(1e-6);
            Move(hero, 1500, 1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.DamageFrom(DamageSource.Mouse) * 0.5).Within(1e-6));
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.GreaterThan(0));
        }

        [Test]
        public void LightningZigzagWaitsForAFlickThenStrikesTwoEnemies()
        {
            var hero = Hero("lightning_zigzag");
            hero.Cast(0);
            hero.Ticks(1);
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));

            hero.Input.Flick();
            hero.Ticks(1);

            var strike = hero.SkillDamage("lightning_zigzag", 4);
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(strike).Within(1e-6));
            Assert.That(hero.Queued(0), Is.EqualTo(strike).Within(1e-6));
        }
    }
}
