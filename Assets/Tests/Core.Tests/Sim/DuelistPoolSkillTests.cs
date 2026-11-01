using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DuelistPoolSkillTests
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
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "night_order");
            FightBoss(duelist, ticksLeft);

            duelist.Cast(0);

            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(duelist.SkillDamage("night_order", multiplier)).Within(1e-6));
        }

        [Test]
        public void KnifeThatDoesNotHurryNeedsASecondOfQuiet()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "the_knife_that_does_not_hurry");
            duelist.Ticks(1, keysPerTick: 1);
            duelist.Loop.Events.Clear();

            duelist.Cast(0);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(duelist.SkillDamage("the_knife_that_does_not_hurry", 5)).Within(1e-6));

            duelist.Ticks(40);
            duelist.Loop.Events.Clear();
            duelist.Cast(0);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.GreaterThanOrEqualTo(duelist.SkillDamage("the_knife_that_does_not_hurry", 20) * 0.999));
        }

        [Test]
        public void TheFishKnifeBranchMakesTheThinCutHarderBelowHalfHealth()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "thin_cut");
            duelist.Branch("thin_cut", "fish_knife");
            duelist.Cast(0);
            var full = duelist.DamageFrom(DamageSource.Skill);
            duelist.Loop.Events.Clear();
            duelist.State.Skills[0].CooldownTicks = 0;
            duelist.State.EnemyHp = duelist.State.EnemyMaxHp * 0.4;

            duelist.Cast(0);

            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(full * 1.5).Within(full * 1e-6));
        }

        [Test]
        public void TrimTheEdgeStrikesAndMarksTheEnemyTwiceAsLongForTheRonin()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "trim_the_edge");
            var before = Damage(duelist);

            duelist.Cast(0);

            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.GreaterThan(0));
            Assert.That(Damage(duelist), Is.EqualTo(before * 1.1).Within(1e-6));
            duelist.Ticks(60);
            Assert.That(duelist.State.Skills[0].Active, Is.True);
            duelist.Ticks(41);
            Assert.That(duelist.State.Skills[0].Active, Is.False);
        }
    }
}
