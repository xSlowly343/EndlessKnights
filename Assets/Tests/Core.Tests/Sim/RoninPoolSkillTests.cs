using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RoninPoolSkillTests
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

        private static void FightBoss(SkillTestHero hero, int ticksLeft = 10000)
        {
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = ticksLeft;
        }

        private static void KeyHitsUntilCombo(SkillTestHero hero, int combo)
        {
            for (var i = 0; i < 10 * combo && hero.State.Combo < combo; i++)
                hero.Ticks(1, keysPerTick: 1);
            Assert.That(hero.State.Combo, Is.EqualTo(combo));
        }

        private static double Damage(SkillTestHero hero) => hero.Loop.Hero.Value(CombatStats.Damage, 100);

        [TestCase(1.0, 4)]
        [TestCase(0.4, 6)]
        public void FishKnifeCutsHarderBelowHalfHealth(double hpShare, double multiplier)
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "fish_knife");
            ronin.State.EnemyHp = ronin.State.EnemyMaxHp * hpShare;

            ronin.Cast(0);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("fish_knife", multiplier)).Within(1e-6));
        }

        [Test]
        public void FishKnifeKillGivesTheRoninAWhirlBack()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "fish_knife");
            ronin.State.EnemyHp = BigNumber.FromDouble(1e-6);

            ronin.Cast(0);

            Assert.That(ronin.State.Resource, Is.EqualTo(100));
        }

        [Test]
        public void PinchOfSaltWoundsTheNextEnemyAndAddsFiveToTheStreak()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "pinch_of_salt");

            ronin.Cast(0);

            Assert.That(ronin.State.Combo, Is.EqualTo(5));
            Assert.That(ronin.Queued(0), Is.EqualTo(ronin.SkillDamage("pinch_of_salt", 1)).Within(1e-6));
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("pinch_of_salt", 3)).Within(1e-6));
        }

        [TestCase(10000, 12)]
        [TestCase(50, 18)]
        public void NightOrderHitsHalfAgainInTheLastTenSecondsOfABoss(int ticksLeft, double multiplier)
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "night_order");
            FightBoss(ronin, ticksLeft);

            ronin.Cast(0);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("night_order", multiplier)).Within(1e-6));
        }

        [TestCase(100, 14)]
        [TestCase(80, 10)]
        public void ThroughThreeBoardsIsStrongerAtFiveWhirls(double resource, double multiplier)
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "through_three_boards");
            ronin.State.Resource = resource;

            ronin.Cast(0);

            var hit = ronin.SkillDamage("through_three_boards", multiplier);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(hit).Within(1e-6 * hit));
            Assert.That(ronin.Queued(1), Is.EqualTo(hit).Within(1e-6 * hit));
            Assert.That(ronin.Queued(2), Is.EqualTo(0));
        }

        [Test]
        public void ReflectionInTheBladeRepeatsTheLastKeyHitFiveTimesOver()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "reflection_in_the_blade");
            ronin.Ticks(1, keysPerTick: 1);
            var keyHit = ronin.DamageFrom(DamageSource.Keyboard);
            ronin.Loop.Events.Clear();

            ronin.Cast(0);

            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(5 * keyHit).Within(1e-6 * keyHit));
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
        public void RiceGrainIsFreeTwoSecondsAfterTheBladeDance()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "rice_grain");
            ronin.State.LastComboStrikeTick = ronin.State.Tick;

            ronin.Cast(0);

            Assert.That(ronin.State.Resource, Is.EqualTo(100));
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

        [Test]
        public void DanceWithoutMusicDoublesTheNextFiveKeyHitsAndGivesWhirls()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "dance_without_music");
            ronin.Cast(0);
            ronin.State.Resource = 0;

            ronin.Ticks(8, keysPerTick: 1);

            Assert.That(SkillHits(ronin), Is.EqualTo(5));
            Assert.That(ronin.State.Skills[0].Active, Is.False);
            Assert.That(ronin.State.Resource, Is.EqualTo(20).Within(1e-9));
        }

        [Test]
        public void DebtPaidStrikesOnEveryHundredthStreakHit()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "debt_paid");

            KeyHitsUntilCombo(ronin, 100);

            Assert.That(SkillHits(ronin), Is.EqualTo(1));
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("debt_paid", 25)).Within(1e-6));
        }

        [Test]
        public void SteadyKnockAddsFiveToTheStreakStrike()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "steady_knock");
            ronin.State.Resource = 0;
            var rules = TestWorld.Data.Combat.Classes;

            KeyHitsUntilCombo(ronin, rules.ComboStrikeEvery);
            ronin.Ticks(1);

            Assert.That(ronin.State.Resource, Is.EqualTo(rules.ComboStrikeEvery / rules.BreathEvery * rules.WhirlResource + 5).Within(1e-9));
        }

        [Test]
        public void MemoryOfTheKnifeKeepsHalfTheStreakWhenItBreaks()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "memory_of_the_knife");
            KeyHitsUntilCombo(ronin, 40);

            ronin.Ticks(30);

            Assert.That(ronin.State.Combo, Is.EqualTo(20));
        }

        [Test]
        public void HeavenlySquareStopsTheBossTimerForTenSeconds()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "heavenly_square");
            FightBoss(ronin, 1000);

            ronin.Cast(0);
            ronin.Ticks(50);

            Assert.That(ronin.State.BossTicksLeft, Is.EqualTo(1000));
        }

        [Test]
        public void ColdWaterHelpsOnlyARetriedBoss()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "cold_water");
            FightBoss(ronin);
            ronin.Ticks(1);
            var first = Damage(ronin);

            ronin.State.BossFailsInARow = 1;
            ronin.Ticks(1);

            Assert.That(Damage(ronin), Is.EqualTo(first * 1.25).Within(1e-6));
        }

        [Test]
        public void TheLastRoninStretchesTheBarToSevenWhirls()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "the_last_ronin");
            ronin.Ticks(1);
            var atFive = Damage(ronin);

            Assert.That(ronin.Loop.Resource.Max, Is.EqualTo(140));
            ronin.State.Resource = 140;
            ronin.Ticks(1);

            Assert.That(Damage(ronin), Is.EqualTo(atFive * 1.2 / 1.0).Within(1e-6));
        }

        [Test]
        public void CleanBoardGrowsFivePercentForEverySecondOnOneEnemy()
        {
            var ronin = SkillTestHero.Of(HeroClass.Ronin, "clean_board");
            ronin.Ticks(1);
            var start = Damage(ronin);

            ronin.Ticks(50);

            Assert.That(Damage(ronin), Is.EqualTo(start * 1.25).Within(1e-6));
        }
    }
}
