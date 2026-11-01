using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DuelistSkillTests
    {
        private static int SkillHits(SkillTestHero hero)
        {
            var count = 0;
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                if (hero.Loop.Events[i].Kind == GameEventKind.Damage && hero.Loop.Events[i].Source == DamageSource.Skill)
                    count++;
            }
            return count;
        }

        private static void KeyHitsUntilCombo(SkillTestHero hero, int combo)
        {
            for (var i = 0; i < 10 * combo && hero.State.Combo < combo; i++)
                hero.Ticks(1, keysPerTick: 1);
            Assert.That(hero.State.Combo, Is.EqualTo(combo));
        }

        private static void FightBoss(SkillTestHero hero)
        {
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = 10000;
        }

        [Test]
        public void ThinCutIsFreeForTheRoninAfterFiftyStreakHits()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "thin_cut");
            duelist.State.Resource = 0;
            duelist.State.Combo = 51;

            Assert.That(duelist.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(duelist.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void ThinCutCostsTwentyBelowFiftyStreakHits()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "thin_cut");
            duelist.State.Combo = 50;

            duelist.Cast(0);

            Assert.That(duelist.State.Resource, Is.EqualTo(80));
        }

        [Test]
        public void HundredCutsHitsFifteenTimesInOneAndAHalfSecondsAndWoundsTheNext()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "hundred_cuts");

            Assert.That(duelist.Cast(0), Is.EqualTo(CastResult.Cast));
            duelist.Ticks(1);
            Assert.That(SkillHits(duelist), Is.EqualTo(1));

            duelist.Ticks(20);

            var hit = duelist.SkillDamage("hundred_cuts", 2);
            Assert.That(SkillHits(duelist), Is.EqualTo(15));
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(15 * hit).Within(1e-6 * hit));
            Assert.That(duelist.Queued(0), Is.EqualTo(15 * hit).Within(1e-6 * hit));
            Assert.That(duelist.State.Resource, Is.EqualTo(0));
            Assert.That(TestWorld.Real.SkillBook.StartingGems("duelist"), Has.Some.Property("Id").EqualTo("hundred_cuts"));
        }

        [Test]
        public void HundredCutsGoOnDownTheQueueAfterAKillForTheRonin()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "hundred_cuts");
            duelist.State.EnemyHp = BigNumber.FromDouble(1e-6);

            duelist.Cast(0);
            duelist.Ticks(1);
            Assert.That(duelist.State.Kills, Is.EqualTo(1));
            Assert.That(duelist.State.Skills[0].HitsLeft, Is.EqualTo(14));

            duelist.Ticks(60);
            Assert.That(duelist.State.Skills[0].HitsLeft, Is.EqualTo(0));
            Assert.That(duelist.State.Kills, Is.GreaterThan(1));
        }

        [Test]
        public void HundredCutsStopAtTheKillWithABorrowedKatana()
        {
            var runedancer = SkillTestHero.Borrowing(HeroClass.Runedancer, HeroClass.Duelist, "hundred_cuts");
            runedancer.State.Resource = 100;
            runedancer.State.EnemyHp = BigNumber.FromDouble(1e-6);

            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));
            runedancer.Ticks(1);

            Assert.That(runedancer.State.Kills, Is.EqualTo(1));
            Assert.That(runedancer.State.Skills[0].HitsLeft, Is.EqualTo(0));
        }

        [Test]
        public void NoRushStrikesOnTheFirstKeyAfterAOneSecondPause()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "no_rush");
            duelist.Ticks(3, keysPerTick: 1);

            duelist.Cast(0);
            duelist.Ticks(1, keysPerTick: 1);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(0));

            duelist.Ticks(10);
            duelist.Ticks(1, keysPerTick: 1);

            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(duelist.SkillDamage("no_rush", 6)).Within(1e-9));
            Assert.That(duelist.State.Skills[0].Active, Is.False);
        }

        [Test]
        public void NoRushKeepsTheRoninsStreakThroughThePause()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "no_rush");
            duelist.Ticks(5, keysPerTick: 1);

            duelist.Cast(0);
            duelist.Ticks(30);
            duelist.Ticks(1, keysPerTick: 1);

            Assert.That(duelist.State.Combo, Is.EqualTo(6));
        }

        [Test]
        public void KitchenRhythmDoublesEveryTenthKeyHit()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "kitchen_rhythm");

            duelist.Cast(0);
            KeyHitsUntilCombo(duelist, 9);
            Assert.That(SkillHits(duelist), Is.EqualTo(0));

            KeyHitsUntilCombo(duelist, 10);
            Assert.That(SkillHits(duelist), Is.EqualTo(1));
        }

        [Test]
        public void KitchenRhythmGivesTheRoninWhirlsTwiceAsFast()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "kitchen_rhythm");
            duelist.State.Resource = 40;
            var rules = TestWorld.Data.Combat.Classes;

            duelist.Cast(0);
            KeyHitsUntilCombo(duelist, rules.BreathEvery);
            duelist.Ticks(1);

            Assert.That(duelist.State.Resource, Is.EqualTo(2 * rules.WhirlResource).Within(1e-9));
        }

        [TestCase(120, 50, 112)]
        [TestCase(120, 100, 137)]
        [TestCase(1000, 50, 150)]
        [TestCase(9, 50, 100)]
        public void WhatRemainsOfTheDebtGrowsCritDamageWithTheStreak(int combo, double resource, double expected)
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "what_remains_of_the_debt");
            duelist.State.Resource = resource;
            duelist.State.Combo = combo;

            duelist.Ticks(1);

            Assert.That(duelist.Cast(0), Is.EqualTo(CastResult.Passive));
            Assert.That(duelist.Loop.Hero.Value(CombatStats.CritMultiplier, 100), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void SweepOverTheQueueHitsEveryEnemyInLine()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "sweep_over_the_queue");

            duelist.Cast(0);

            var hit = duelist.SkillDamage("sweep_over_the_queue", 20);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(hit).Within(1e-6 * hit));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(duelist.Queued(i), Is.EqualTo(hit).Within(1e-6 * hit));
        }

        [Test]
        public void SweepOverTheQueueGoesTwiceAfterAHundredStreakHits()
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "sweep_over_the_queue");
            duelist.State.Combo = 101;

            duelist.Cast(0);

            var hit = duelist.SkillDamage("sweep_over_the_queue", 20);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(2 * hit).Within(1e-6 * hit));
            Assert.That(duelist.Queued(GameState.QueueDepth - 1), Is.EqualTo(2 * hit).Within(1e-6 * hit));
        }

        [TestCase(EnemyType.Normal, 2)]
        [TestCase(EnemyType.Boss, 2.5)]
        public void SlowSimmerReleasesTheStoredHitsTwiceAndMoreOnBosses(EnemyType type, double share)
        {
            var duelist = SkillTestHero.Of(HeroClass.Duelist, "slow_simmer");
            if (type == EnemyType.Boss)
                FightBoss(duelist);

            duelist.Cast(0);
            duelist.Ticks(10, keysPerTick: 1);
            duelist.Ticks(45);

            var keys = duelist.DamageFrom(DamageSource.Keyboard);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(share * keys).Within(1e-6 * keys));
        }

        [Test]
        public void SlowSimmerWithABorrowedKatanaHasNoBossBonus()
        {
            var runedancer = SkillTestHero.Borrowing(HeroClass.Runedancer, HeroClass.Duelist, "slow_simmer");
            runedancer.State.Resource = 100;
            FightBoss(runedancer);

            runedancer.Cast(0);
            runedancer.Ticks(10, keysPerTick: 1);
            runedancer.Ticks(45);

            var keys = runedancer.DamageFrom(DamageSource.Keyboard);
            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(2 * keys).Within(1e-6 * keys));
        }
    }
}
