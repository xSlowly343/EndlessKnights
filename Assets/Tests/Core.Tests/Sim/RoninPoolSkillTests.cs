using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RoninPoolSkillTests
    {
        private static void FightBoss(SkillTestHero hero, int ticksLeft = 10000)
        {
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = ticksLeft;
        }

        private static double Damage(SkillTestHero hero) => hero.Loop.Hero.Value(CombatStats.Damage, 100);

        [TestCase(10000, 12)]
        [TestCase(50, 18)]
        public void NightOrderHitsHalfAgainInTheLastTenSecondsOfABoss(int ticksLeft, double multiplier)
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "night_order");
            FightBoss(ronin, ticksLeft);

            ronin.Cast(0);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("night_order", multiplier)).Within(1e-6));
        }

        [Test]
        public void KnifeThatDoesNotHurryNeedsASecondOfQuiet()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "the_knife_that_does_not_hurry");
            ronin.Ticks(1, keysPerTick: 1);
            ronin.Loop.Events.Clear();

            ronin.Cast(0);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("the_knife_that_does_not_hurry", 5)).Within(1e-6));

            ronin.Ticks(40);
            ronin.Loop.Events.Clear();
            ronin.Cast(0);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.GreaterThanOrEqualTo(ronin.SkillDamage("the_knife_that_does_not_hurry", 20) * 0.999));
        }

        [Test]
        public void TheFishKnifeBranchMakesTheThinCutHarderBelowHalfHealth()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "thin_cut");
            ronin.Branch("thin_cut", "fish_knife");
            ronin.Cast(0);
            var full = ronin.DamageFrom(DamageSource.Skill);
            ronin.Loop.Events.Clear();
            ronin.State.Skills[0].CooldownTicks = 0;
            ronin.State.EnemyHp = ronin.State.EnemyMaxHp * 0.4;

            ronin.Cast(0);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(full * 1.5).Within(full * 1e-6));
        }

        [Test]
        public void TrimTheEdgeStrikesAndMarksTheEnemyTwiceAsLongForTheRonin()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "trim_the_edge");
            var before = Damage(ronin);

            ronin.Cast(0);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.GreaterThan(0));
            Assert.That(Damage(ronin), Is.EqualTo(before * 1.1).Within(1e-6));
            ronin.Ticks(60);
            Assert.That(ronin.State.Skills[0].Active, Is.True);
            ronin.Ticks(41);
            Assert.That(ronin.State.Skills[0].Active, Is.False);
        }
    }
}
