using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CommonUniversalSkillTests
    {
        private static SkillTestHero Hero(params string[] skills) => SkillTestHero.Of(HeroClass.Ronin, skills);

        [Test]
        public void GoldenFeverTradesAThirdOfDamageForDoubleGold()
        {
            var hero = Hero("golden_fever");

            hero.Cast(0);

            Assert.That(hero.Loop.Hero.Value(CombatStats.Gold, 1, StatTags.Gold), Is.EqualTo(2).Within(1e-9));
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(0.6667).Within(1e-9));
        }

        [Test]
        public void SecondWindGivesFiftyResourceAtOnce()
        {
            var hero = Hero("second_wind");
            hero.State.Resource = 10;

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(hero.State.Resource, Is.EqualTo(60));
        }

        [Test]
        public void EchoRepeatsTheLastOtherStrikeAtHalfPower()
        {
            var hero = Hero("ricochet", "echo");
            hero.Cast(0);
            hero.Loop.Events.Clear();
            var queuedBefore = hero.Queued(0);

            hero.Cast(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("ricochet", 1)).Within(1e-6));
            Assert.That(hero.Queued(0) - queuedBefore, Is.EqualTo(hero.SkillDamage("ricochet", 0.5)).Within(1e-6));
        }

        [Test]
        public void EchoRecastsAnEndedAuraAtHalfStrength()
        {
            var hero = Hero("exposure", "echo");
            hero.Cast(0);
            hero.Ticks(160);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.Skill), Is.EqualTo(1));

            hero.Cast(1);

            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.Skill), Is.EqualTo(1.1).Within(1e-9));
        }

        [Test]
        public void EchoWithoutAnEarlierCastDoesNothing()
        {
            var hero = Hero("echo");

            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void CoffeeBreakDoublesDamageForAMinuteAfterAFiveMinutePause()
        {
            var hero = Hero("coffee_break");
            hero.Ticks(3000);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1));

            hero.Ticks(1, keysPerTick: 1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(2).Within(1e-9));

            for (var i = 0; i < 300; i++)
            {
                hero.Input.PressKeys(1);
                hero.Ticks(2);
            }
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1));
        }

        [Test]
        public void CoffeeBreakNeedsALongPause()
        {
            var hero = Hero("coffee_break");
            hero.Ticks(2000);

            hero.Ticks(1, keysPerTick: 1);

            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1));
        }

        [Test]
        public void CallReservesAndStrikesEveryTwoSecondsWithoutKeys()
        {
            var hero = Hero("call");
            hero.Ticks(1);
            Assert.That(hero.State.ReservedResource, Is.EqualTo(20));
            hero.Ticks(1, keysPerTick: 1);
            hero.Loop.Events.Clear();

            hero.Ticks(40);
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("call", 2)).Within(1e-6));

            hero.Loop.Events.Clear();
            for (var i = 0; i < 20; i++)
            {
                hero.Input.PressKeys(1);
                hero.Ticks(2);
            }
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void ShoulderToShoulderCountsEveryoneInTheParty()
        {
            var hero = Hero("shoulder_to_shoulder");
            hero.Ticks(1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.05).Within(1e-9));

            hero.Loop.PartyBuffs.Ally(1).Present = true;
            hero.Loop.PartyBuffs.Ally(2).Present = true;
            hero.Ticks(2);

            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.15).Within(1e-9));
        }

        [Test]
        public void HerosDiaryRaisesGemExperience()
        {
            var hero = Hero("heros_diary");

            hero.Ticks(1);

            Assert.That(hero.Loop.Hero.Value(CombatStats.GemExperience, 1), Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void RiftCrystalTriplesTheRewardOfTheNextKillOnly()
        {
            var hero = Hero("rift_crystal");
            hero.Cast(0);

            var gold = hero.State.Gold;
            var expected = hero.Loop.Loot.GoldFor(hero.State.Stage, hero.State.EnemyType, hero.State.Enemy) * 3;
            hero.KillCurrentEnemy();
            Assert.That((hero.State.Gold - gold).ToDouble(), Is.EqualTo(expected.ToDouble()).Within(expected.ToDouble() * 1e-9));

            gold = hero.State.Gold;
            expected = hero.Loop.Loot.GoldFor(hero.State.Stage, hero.State.EnemyType, hero.State.Enemy);
            hero.KillCurrentEnemy();
            Assert.That((hero.State.Gold - gold).ToDouble(), Is.EqualTo(expected.ToDouble()).Within(expected.ToDouble() * 1e-9));
        }

        [Test]
        public void RiftCrystalSometimesDropsAShard()
        {
            var hero = Hero("rift_crystal");
            for (var i = 0; i < 40; i++)
            {
                hero.State.Resource = 100;
                hero.State.Skills[0].StartCooldown(0);
                hero.Cast(0);
                hero.KillCurrentEnemy();
                hero.Ticks(1);
            }

            Assert.That(hero.State.Rift.Shards.ToDouble(), Is.InRange(1, 39));
        }
    }
}
