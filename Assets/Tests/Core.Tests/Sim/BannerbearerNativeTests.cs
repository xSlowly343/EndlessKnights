using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BannerbearerNativeTests
    {
        private static void HammerTimes(SkillTestHero hero, int casts)
        {
            for (var i = 0; i < casts; i++)
            {
                hero.State.Resource = 100;
                hero.State.Skills[0].CooldownTicks = 0;
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            }
        }

        [Test]
        public void ForemanHammerStacksThreePercentDamageUpToFive()
        {
            var hero = SkillTestHero.Of(HeroClass.Bannerbearer, "foreman_hammer");

            HammerTimes(hero, 7);

            var slot = hero.State.Skills[0];
            Assert.That(slot.Stacks, Is.EqualTo(5));
            Assert.That(slot.StackModifier.Value, Is.EqualTo(0.15).Within(1e-9));
        }

        [Test]
        public void HammerStacksLastTwelveSecondsForTheBannerman()
        {
            var hero = SkillTestHero.Of(HeroClass.Bannerbearer, "foreman_hammer");
            HammerTimes(hero, 2);

            hero.Ticks(11 * GameLoop.TicksPerSecond);
            Assert.That(hero.State.Skills[0].Stacks, Is.EqualTo(2));

            hero.Ticks(GameLoop.TicksPerSecond);
            Assert.That(hero.State.Skills[0].Stacks, Is.Zero);
        }

        [Test]
        public void HammerStacksLastEightSecondsForAnotherHero()
        {
            var viking = SkillTestHero.Borrowing(HeroClass.Viking, HeroClass.Bannerbearer, "foreman_hammer");
            HammerTimes(viking, 1);

            viking.Ticks(8 * GameLoop.TicksPerSecond);

            Assert.That(viking.State.Skills[0].Stacks, Is.Zero);
        }

        [TestCase(100, 14)]
        [TestCase(40, 8)]
        public void TheBannerDoesNotFallTakesTheWholeBarForOneSecondPerTenAboveForty(double resource, double seconds)
        {
            var hero = SkillTestHero.Of(HeroClass.Bannerbearer, "the_banner_does_not_fall");
            hero.State.Resource = resource;

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(hero.State.Resource, Is.EqualTo(0).Within(1e-9));
            Assert.That(hero.State.Skills[0].ActiveTicks, Is.EqualTo((int)(seconds * GameLoop.TicksPerSecond)));
        }

        [Test]
        public void BannerAurasGoOutForOthersAfterTenSecondsWithoutInput()
        {
            var viking = SkillTestHero.Borrowing(HeroClass.Viking, HeroClass.Bannerbearer, "artel_pay");
            var bannerman = SkillTestHero.Of(HeroClass.Bannerbearer, "artel_pay");
            foreach (var hero in new[] { viking, bannerman })
            {
                hero.Ticks(1, keysPerTick: 1);
                Assert.That(hero.State.Skills[0].AuraApplied, Is.True);
                hero.State.IdleTicks = 10 * GameLoop.TicksPerSecond;
                hero.Ticks(2);
            }

            Assert.That(viking.State.Skills[0].AuraApplied, Is.False);
            Assert.That(bannerman.State.Skills[0].AuraApplied, Is.True);

            viking.Ticks(1, keysPerTick: 1);
            viking.Ticks(1);
            Assert.That(viking.State.Skills[0].AuraApplied, Is.True);
        }

        [Test]
        public void SignedHammerCirclesTheWholeQueueOnceASecond()
        {
            var hero = SkillTestHero.Of(HeroClass.Bannerbearer, "signed_hammer");
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.Loop.Events.Clear();

            hero.Ticks(GameLoop.TicksPerSecond);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("signed_hammer", 2)).Within(1e-6));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(hero.Queued(i), Is.EqualTo(hero.SkillDamage("signed_hammer", 2)).Within(1e-6));
            Assert.That(TestWorld.Data.Gems["signed_hammer"].Locked, Is.False);
        }

        [Test]
        public void AnswerFromTheShieldPushesTheBossTimerBackOneSecondEveryThreeSeconds()
        {
            var hero = SkillTestHero.Of(HeroClass.Bannerbearer, "answer_from_the_shield");
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = 300;
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.Loop.Events.Clear();

            hero.Ticks(3 * GameLoop.TicksPerSecond);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("answer_from_the_shield", 6)).Within(1e-6));
            Assert.That(hero.State.BossTicksLeft, Is.EqualTo(300 - 30 + 10));
            Assert.That(TestWorld.Data.Gems["answer_from_the_shield"].Locked, Is.False);
        }
    }
}
