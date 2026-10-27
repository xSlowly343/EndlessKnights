using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StormweaverNativeTests
    {
        private static int SkillHits(SkillTestHero hero)
        {
            var hits = 0;
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                if (hero.Loop.Events[i].Kind == GameEventKind.Damage && hero.Loop.Events[i].Source == DamageSource.Skill)
                    hits++;
            }
            return hits;
        }

        private static int PulseHits(SkillTestHero hero, int ticks, bool mouse)
        {
            hero.State.Resource = 100;
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.Loop.Events.Clear();
            for (var i = 0; i < ticks; i++)
            {
                if (mouse)
                    hero.Input.MoveMouse(1);
                hero.Loop.Tick();
            }
            return SkillHits(hero);
        }

        [TestCase(100, 3)]
        [TestCase(50, 2)]
        public void FingerSparkHitsFourEnemiesAboveSeventyStatic(double statik, int queued)
        {
            var hero = SkillTestHero.Of(HeroClass.Stormweaver, "finger_spark");
            hero.State.Resource = statik;

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));

            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(hero.Queued(i) > 0, Is.EqualTo(i < queued), i.ToString());
        }

        [Test]
        public void CloudCloakStrikesEveryThreeTenthsOfASecondWhileTheMouseMoves()
        {
            Assert.That(PulseHits(SkillTestHero.Of(HeroClass.Stormweaver, "cloud_cloak"), 30, mouse: false), Is.EqualTo(6));
            Assert.That(PulseHits(SkillTestHero.Of(HeroClass.Stormweaver, "cloud_cloak"), 30, mouse: true), Is.EqualTo(10));
        }

        [Test]
        public void EveryLanternHitGivesOneStatic()
        {
            var lantern = SkillTestHero.Of(HeroClass.Stormweaver, "lantern_at_a_distance");
            var plain = SkillTestHero.Of(HeroClass.Stormweaver, "lantern_at_a_distance");
            Assert.That(lantern.Cast(0), Is.EqualTo(CastResult.Cast));
            lantern.State.Resource = 30;
            plain.State.Resource = 30;

            foreach (var hero in new[] { lantern, plain })
            {
                for (var i = 0; i < 10; i++)
                {
                    hero.Input.MoveMouse(300);
                    hero.Loop.Tick();
                }
            }

            Assert.That(lantern.State.Resource - plain.State.Resource, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void PealOfThunderStopsTheBossTimerForThreeSeconds()
        {
            var hero = SkillTestHero.Of(HeroClass.Stormweaver, "peal_of_thunder");
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = 300;

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.Ticks(30);
            Assert.That(hero.State.BossTicksLeft, Is.EqualTo(300));

            hero.Ticks(10);
            Assert.That(hero.State.BossTicksLeft, Is.EqualTo(290));
        }

        [Test]
        public void PealOfThunderDoesNotStopTheTimerOutsideABossFight()
        {
            var hero = SkillTestHero.Of(HeroClass.Stormweaver, "peal_of_thunder");

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(hero.State.BossTimerStopTicks, Is.Zero);
        }

        [Test]
        public void OverloadDuringTheWeatherKeepsSeventyPercentOfTheBar()
        {
            var hero = SkillTestHero.Of(HeroClass.Stormweaver, "weather_over_the_edge");
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.State.Resource = 100;

            hero.Ticks(1);

            Assert.That(hero.Loop.Overload.Discharges, Is.EqualTo(1));
            Assert.That(hero.State.Resource, Is.EqualTo(70).Within(1e-6));
        }

        [Test]
        public void OverloadWithoutTheWeatherEmptiesTheBar()
        {
            var hero = SkillTestHero.Of(HeroClass.Stormweaver, "finger_spark");
            hero.State.Resource = 100;

            hero.Ticks(1);

            Assert.That(hero.Loop.Overload.Discharges, Is.EqualTo(1));
            Assert.That(hero.State.Resource, Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void NoonAndMidnightDoublesDamageAtFullBalance()
        {
            var hero = SkillTestHero.Of(HeroClass.Stormweaver, "noon_and_midnight");

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.Ticks(1);

            Assert.That(hero.Loop.Hero.Value(CombatStats.DamageAtBalance, 0), Is.EqualTo(1).Within(1e-9));
            Assert.That(TestWorld.Data.Gems["noon_and_midnight"].Locked, Is.False);
        }

        [Test]
        public void DustWhirlHitsTheWholeQueueOnceASecondAndFasterWithTheMouse()
        {
            var still = SkillTestHero.Of(HeroClass.Stormweaver, "dust_whirl");
            Assert.That(PulseHits(still, 20, mouse: false), Is.EqualTo(2));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(still.Queued(i), Is.GreaterThan(0));

            Assert.That(PulseHits(SkillTestHero.Of(HeroClass.Stormweaver, "dust_whirl"), 20, mouse: true), Is.EqualTo(4));
            Assert.That(TestWorld.Data.Gems["dust_whirl"].Locked, Is.False);
        }

        private static double FirstKeyHit(SkillTestHero hero)
        {
            hero.Ticks(1);
            hero.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            hero.Loop.Events.Clear();
            hero.Input.PressKeys(1);
            hero.Loop.Tick();
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                var e = hero.Loop.Events[i];
                if (e.Kind == GameEventKind.Damage && e.Source == DamageSource.Keyboard)
                    return e.Amount.ToDouble();
            }
            return 0;
        }

        [Test]
        public void FrozenEnemyTakesTripleDamageFromTheFirstHit()
        {
            var frozen = SkillTestHero.Of(HeroClass.Stormweaver, "rime_sphere");
            var plain = SkillTestHero.Of(HeroClass.Stormweaver, "rime_sphere");
            Assert.That(frozen.Cast(0), Is.EqualTo(CastResult.Cast));
            plain.State.Resource = frozen.State.Resource;

            Assert.That(FirstKeyHit(frozen) / FirstKeyHit(plain), Is.EqualTo(3).Within(1e-6));
        }
    }
}
