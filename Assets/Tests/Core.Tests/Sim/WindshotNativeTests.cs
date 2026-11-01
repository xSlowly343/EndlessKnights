using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WindshotNativeTests
    {
        private const double Arrow = 5;
        private static double QuiverPerSecond => TestWorld.Data.Combat.Resources.For("windshot").PerSecond;

        private static void MoveMouse(SkillTestHero hero, double px, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                hero.Input.MoveMouse((float)(px / ticks));
                hero.Loop.Tick();
            }
        }

        private static int HitsOfDownpour(SkillTestHero windshot)
        {
            windshot.State.Resource = 100;
            Assert.That(windshot.Cast(0), Is.EqualTo(CastResult.Cast));
            return windshot.State.Skills[0].HitsLeft;
        }

        [Test]
        public void RoadArrowComesBackOnACrit()
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "arrow_from_the_road");
            windshot.Loop.Hero.Add(new Modifier(CombatStats.CritEvery, ModifierKind.Flat, 1));
            windshot.State.Resource = Arrow;

            Assert.That(windshot.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(windshot.State.Resource, Is.EqualTo(Arrow).Within(1e-9));
        }

        [Test]
        public void AnArrowIsSpentWithoutACrit()
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "reed_arrow");
            windshot.State.Resource = Arrow;

            Assert.That(windshot.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(windshot.State.Resource, Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void AnotherHeroWithTheBowGetsNoArrowBack()
        {
            var runedancer = SkillTestHero.Borrowing(HeroClass.Runedancer, HeroClass.Windshot, "arrow_from_the_road");
            runedancer.Loop.Hero.Add(new Modifier(CombatStats.CritEvery, ModifierKind.Flat, 1));
            runedancer.State.Resource = 100;

            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(runedancer.State.Resource, Is.LessThan(100));
            Assert.That(runedancer.Loop.Skills.IsNative(0), Is.False);
        }

        [Test]
        public void DownpourShootsEightArrowsWithoutTheMouse()
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "tropical_downpour");

            Assert.That(HitsOfDownpour(windshot), Is.EqualTo(8));
        }

        [Test]
        public void DownpourAddsAnArrowForEveryThousandPixelsOfTheLastTenSeconds()
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "tropical_downpour");
            MoveMouse(windshot, 5500, 20);

            Assert.That(HitsOfDownpour(windshot), Is.EqualTo(13));
        }

        [Test]
        public void DownpourStopsAtSixteenArrows()
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "tropical_downpour");
            MoveMouse(windshot, 30000, 30);

            Assert.That(HitsOfDownpour(windshot), Is.EqualTo(16));
        }

        [Test]
        public void MouseOlderThanTenSecondsDoesNotCountForTheDownpour()
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "tropical_downpour");
            MoveMouse(windshot, 5000, 10);
            windshot.Ticks(10 * GameLoop.TicksPerSecond);

            Assert.That(HitsOfDownpour(windshot), Is.EqualTo(8));
        }

        private static double SeedRelease(double mousePx)
        {
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "seed_arrow");
            Assert.That(windshot.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = windshot.State.Skills[0];
            var activeTicks = slot.ActiveTicks;
            for (var i = 0; i < activeTicks - 1; i++)
            {
                if (i % 2 == 0)
                    windshot.Input.PressKeys(1);
                if (i < 10)
                    windshot.Input.MoveMouse((float)(mousePx / 10));
                windshot.Loop.Tick();
            }
            windshot.Loop.Events.Clear();
            var stored = slot.Stored.ToDouble();
            windshot.Loop.Tick();
            Assert.That(slot.Active, Is.False);
            return windshot.DamageFrom(DamageSource.Skill) / stored;
        }

        [Test]
        public void SeedArrowGrowsTenPercentForEveryFiveHundredPixelsBeforeItBursts()
        {
            var still = SeedRelease(0);

            Assert.That(still, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(SeedRelease(1200), Is.EqualTo(0.5 * 1.2).Within(1e-9));
        }

        [Test]
        public void PoisonVineSapGivesHalfAnArrowShareForEveryPoisonTick()
        {
            var plain = SkillTestHero.Of(HeroClass.Windshot, "reed_arrow");
            var windshot = SkillTestHero.Of(HeroClass.Windshot, "poison_vine_sap");
            foreach (var hero in new[] { plain, windshot })
            {
                hero.State.Resource = 0;
                hero.State.PoisonStacks = 5;
                hero.Ticks(10 * GameLoop.TicksPerSecond + 1);
            }

            Assert.That(plain.State.Resource, Is.EqualTo(QuiverPerSecond * (10 + GameLoop.TickSeconds)).Within(1e-6));
            Assert.That(windshot.State.Resource - plain.State.Resource, Is.EqualTo(10 * 0.5).Within(1e-6));
            Assert.That(windshot.Loop.Skills.Dots.PoisonTicks, Is.EqualTo(10));
        }
    }
}
