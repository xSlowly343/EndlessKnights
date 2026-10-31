using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RoninSkillTests
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
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "thin_cut");
            ronin.State.Resource = 0;
            ronin.State.Combo = 51;

            Assert.That(ronin.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(ronin.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void ThinCutCostsTwentyBelowFiftyStreakHits()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "thin_cut");
            ronin.State.Combo = 50;

            ronin.Cast(0);

            Assert.That(ronin.State.Resource, Is.EqualTo(80));
        }

        [Test]
        public void HundredCutsHitsFifteenTimesInOneAndAHalfSecondsAndWoundsTheNext()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "hundred_cuts");

            Assert.That(ronin.Cast(0), Is.EqualTo(CastResult.Cast));
            ronin.Ticks(1);
            Assert.That(SkillHits(ronin), Is.EqualTo(1));

            ronin.Ticks(20);

            var hit = ronin.SkillDamage("hundred_cuts", 2);
            Assert.That(SkillHits(ronin), Is.EqualTo(15));
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(15 * hit).Within(1e-6 * hit));
            Assert.That(ronin.Queued(0), Is.EqualTo(15 * hit).Within(1e-6 * hit));
            Assert.That(ronin.State.Resource, Is.EqualTo(0));
            Assert.That(TestWorld.Real.SkillBook.StartingGems("ronin"), Has.Some.Property("Id").EqualTo("hundred_cuts"));
        }

        [Test]
        public void HundredCutsGoOnDownTheQueueAfterAKillForTheRonin()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "hundred_cuts");
            ronin.State.EnemyHp = BigNumber.FromDouble(1e-6);

            ronin.Cast(0);
            ronin.Ticks(1);
            Assert.That(ronin.State.Kills, Is.EqualTo(1));
            Assert.That(ronin.State.Skills[0].HitsLeft, Is.EqualTo(14));

            ronin.Ticks(60);
            Assert.That(ronin.State.Skills[0].HitsLeft, Is.EqualTo(0));
            Assert.That(ronin.State.Kills, Is.GreaterThan(1));
        }

        [Test]
        public void HundredCutsStopAtTheKillWithABorrowedKatana()
        {
            var viking = SkillTestHero.Borrowing(HeroClass.Viking, HeroClass.Ronin, "hundred_cuts");
            viking.State.Resource = 100;
            viking.State.EnemyHp = BigNumber.FromDouble(1e-6);

            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));
            viking.Ticks(1);

            Assert.That(viking.State.Kills, Is.EqualTo(1));
            Assert.That(viking.State.Skills[0].HitsLeft, Is.EqualTo(0));
        }

        [Test]
        public void NoRushStrikesOnTheFirstKeyAfterAOneSecondPause()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "no_rush");
            ronin.Ticks(3, keysPerTick: 1);

            ronin.Cast(0);
            ronin.Ticks(1, keysPerTick: 1);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(0));

            ronin.Ticks(10);
            ronin.Ticks(1, keysPerTick: 1);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("no_rush", 6)).Within(1e-9));
            Assert.That(ronin.State.Skills[0].Active, Is.False);
        }

        [Test]
        public void NoRushKeepsTheRoninsStreakThroughThePause()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "no_rush");
            ronin.Ticks(5, keysPerTick: 1);

            ronin.Cast(0);
            ronin.Ticks(30);
            ronin.Ticks(1, keysPerTick: 1);

            Assert.That(ronin.State.Combo, Is.EqualTo(6));
        }

        [Test]
        public void KitchenRhythmDoublesEveryTenthKeyHit()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "kitchen_rhythm");

            ronin.Cast(0);
            KeyHitsUntilCombo(ronin, 9);
            Assert.That(SkillHits(ronin), Is.EqualTo(0));

            KeyHitsUntilCombo(ronin, 10);
            Assert.That(SkillHits(ronin), Is.EqualTo(1));
        }

        [Test]
        public void KitchenRhythmGivesTheRoninWhirlsTwiceAsFast()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "kitchen_rhythm");
            ronin.State.Resource = 40;
            var rules = TestWorld.Data.Combat.Classes;

            ronin.Cast(0);
            KeyHitsUntilCombo(ronin, rules.BreathEvery);
            ronin.Ticks(1);

            Assert.That(ronin.State.Resource, Is.EqualTo(2 * rules.WhirlResource).Within(1e-9));
        }

        [TestCase(120, 50, 112)]
        [TestCase(120, 100, 137)]
        [TestCase(1000, 50, 150)]
        [TestCase(9, 50, 100)]
        public void WhatRemainsOfTheDebtGrowsCritDamageWithTheStreak(int combo, double resource, double expected)
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "what_remains_of_the_debt");
            ronin.State.Resource = resource;
            ronin.State.Combo = combo;

            ronin.Ticks(1);

            Assert.That(ronin.Cast(0), Is.EqualTo(CastResult.Passive));
            Assert.That(ronin.Loop.Hero.Value(CombatStats.CritMultiplier, 100), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void SweepOverTheQueueHitsEveryEnemyInLine()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "sweep_over_the_queue");

            ronin.Cast(0);

            var hit = ronin.SkillDamage("sweep_over_the_queue", 20);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(hit).Within(1e-6 * hit));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(ronin.Queued(i), Is.EqualTo(hit).Within(1e-6 * hit));
        }

        [Test]
        public void SweepOverTheQueueGoesTwiceAfterAHundredStreakHits()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "sweep_over_the_queue");
            ronin.State.Combo = 101;

            ronin.Cast(0);

            var hit = ronin.SkillDamage("sweep_over_the_queue", 20);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(2 * hit).Within(1e-6 * hit));
            Assert.That(ronin.Queued(GameState.QueueDepth - 1), Is.EqualTo(2 * hit).Within(1e-6 * hit));
        }

        [TestCase(EnemyType.Normal, 2)]
        [TestCase(EnemyType.Boss, 2.5)]
        public void SlowSimmerReleasesTheStoredHitsTwiceAndMoreOnBosses(EnemyType type, double share)
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "slow_simmer");
            if (type == EnemyType.Boss)
                FightBoss(ronin);

            ronin.Cast(0);
            ronin.Ticks(10, keysPerTick: 1);
            ronin.Ticks(45);

            var keys = ronin.DamageFrom(DamageSource.Keyboard);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(share * keys).Within(1e-6 * keys));
        }

        [Test]
        public void SlowSimmerWithABorrowedKatanaHasNoBossBonus()
        {
            var viking = SkillTestHero.Borrowing(HeroClass.Viking, HeroClass.Ronin, "slow_simmer");
            viking.State.Resource = 100;
            FightBoss(viking);

            viking.Cast(0);
            viking.Ticks(10, keysPerTick: 1);
            viking.Ticks(45);

            var keys = viking.DamageFrom(DamageSource.Keyboard);
            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(2 * keys).Within(1e-6 * keys));
        }
    }
}
