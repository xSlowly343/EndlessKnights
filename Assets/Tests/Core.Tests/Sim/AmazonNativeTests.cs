using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AmazonNativeTests
    {
        private const double Arrow = 5;
        private static double QuiverPerSecond => TestWorld.Data.Combat.Resources.For("amazon").PerSecond;

        private static void MoveMouse(SkillTestHero hero, double px, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                hero.Input.MoveMouse((float)(px / ticks));
                hero.Loop.Tick();
            }
        }

        private static int HitsOfDownpour(SkillTestHero amazon)
        {
            amazon.State.Resource = 100;
            Assert.That(amazon.Cast(0), Is.EqualTo(CastResult.Cast));
            return amazon.State.Skills[0].HitsLeft;
        }

        [Test]
        public void RoadArrowComesBackOnACrit()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "arrow_from_the_road");
            amazon.Loop.Hero.Add(new Modifier(CombatStats.CritEvery, ModifierKind.Flat, 1));
            amazon.State.Resource = Arrow;

            Assert.That(amazon.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(amazon.State.Resource, Is.EqualTo(Arrow).Within(1e-9));
        }

        [Test]
        public void AnArrowIsSpentWithoutACrit()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "reed_arrow");
            amazon.State.Resource = Arrow;

            Assert.That(amazon.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(amazon.State.Resource, Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void AnotherHeroWithTheBowGetsNoArrowBack()
        {
            var viking = SkillTestHero.Borrowing(HeroClass.Viking, HeroClass.Amazon, "arrow_from_the_road");
            viking.Loop.Hero.Add(new Modifier(CombatStats.CritEvery, ModifierKind.Flat, 1));
            viking.State.Resource = 100;

            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(viking.State.Resource, Is.LessThan(100));
            Assert.That(viking.Loop.Skills.IsNative(0), Is.False);
        }

        [Test]
        public void DownpourShootsEightArrowsWithoutTheMouse()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "tropical_downpour");

            Assert.That(HitsOfDownpour(amazon), Is.EqualTo(8));
        }

        [Test]
        public void DownpourAddsAnArrowForEveryThousandPixelsOfTheLastTenSeconds()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "tropical_downpour");
            MoveMouse(amazon, 5500, 20);

            Assert.That(HitsOfDownpour(amazon), Is.EqualTo(13));
        }

        [Test]
        public void DownpourStopsAtSixteenArrows()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "tropical_downpour");
            MoveMouse(amazon, 30000, 30);

            Assert.That(HitsOfDownpour(amazon), Is.EqualTo(16));
        }

        [Test]
        public void MouseOlderThanTenSecondsDoesNotCountForTheDownpour()
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "tropical_downpour");
            MoveMouse(amazon, 5000, 10);
            amazon.Ticks(10 * GameLoop.TicksPerSecond);

            Assert.That(HitsOfDownpour(amazon), Is.EqualTo(8));
        }

        private static double SeedRelease(double mousePx)
        {
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "seed_arrow");
            Assert.That(amazon.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = amazon.State.Skills[0];
            var activeTicks = slot.ActiveTicks;
            for (var i = 0; i < activeTicks - 1; i++)
            {
                if (i % 2 == 0)
                    amazon.Input.PressKeys(1);
                if (i < 10)
                    amazon.Input.MoveMouse((float)(mousePx / 10));
                amazon.Loop.Tick();
            }
            amazon.Loop.Events.Clear();
            var stored = slot.Stored.ToDouble();
            amazon.Loop.Tick();
            Assert.That(slot.Active, Is.False);
            return amazon.DamageFrom(DamageSource.Skill) / stored;
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
            var plain = SkillTestHero.Of(HeroClass.Amazon, "reed_arrow");
            var amazon = SkillTestHero.Of(HeroClass.Amazon, "poison_vine_sap");
            foreach (var hero in new[] { plain, amazon })
            {
                hero.State.Resource = 0;
                hero.State.PoisonStacks = 5;
                hero.Ticks(10 * GameLoop.TicksPerSecond + 1);
            }

            Assert.That(plain.State.Resource, Is.EqualTo(QuiverPerSecond * (10 + GameLoop.TickSeconds)).Within(1e-6));
            Assert.That(amazon.State.Resource - plain.State.Resource, Is.EqualTo(10 * 0.5).Within(1e-6));
            Assert.That(amazon.Loop.Skills.Dots.PoisonTicks, Is.EqualTo(10));
        }
    }
}
