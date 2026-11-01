using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DowserNativeTests
    {
        private static SkillTestHero Stranger(string gem)
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer);
            var type = TestWorld.Data.Gems[gem].Skill!.Weapon;
            var weaponBase = runedancer.Loop.Data.Weapons.Bases.First(weapon => weapon.Type.Id == type);
            runedancer.State.Weapons.Left = new WeaponItem(weaponBase, ItemRarity.Simple, new[] { gem });
            runedancer.Loop.Skills.Equip(TestWorld.Data.Gems[gem]);
            runedancer.State.Resource = 100;
            runedancer.Loop.Events.Clear();
            return runedancer;
        }

        [Test]
        public void SteamWillTellHitsByThePressureAndEmptiesIt()
        {
            var dowser = SkillTestHero.Of(HeroClass.Dowser, "the_steam_will_tell");
            dowser.State.Resource = 60;

            Assert.That(dowser.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(dowser.State.Resource, Is.Zero);
            Assert.That(dowser.DamageFrom(DamageSource.Skill), Is.EqualTo(dowser.SkillDamage("the_steam_will_tell", 6)).Within(1e-6));
            Assert.That(dowser.State.BurnStacks, Is.EqualTo(6).Within(1e-9));
        }

        [Test]
        public void SteamWillTellIsAPlainBlastForAnotherHero()
        {
            var runedancer = Stranger("the_steam_will_tell");

            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(runedancer.SkillDamage("the_steam_will_tell", 5)).Within(1e-6));
            Assert.That(runedancer.State.BurnStacks, Is.EqualTo(5).Within(1e-9));
        }

        private static double BurnAfterTwelveKeys(SkillTestHero hero)
        {
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            for (var i = 0; i < 24; i++)
            {
                hero.State.Resource = System.Math.Min(hero.State.Resource, 10);
                hero.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);
            }
            return hero.State.BurnStacks;
        }

        [Test]
        public void FirstDropSetsFireEveryTenthKeyAndEverySixthForTheMoonshiner()
        {
            Assert.That(BurnAfterTwelveKeys(Stranger("first_drop")), Is.EqualTo(1).Within(1e-9));
            Assert.That(BurnAfterTwelveKeys(SkillTestHero.Of(HeroClass.Dowser, "first_drop")), Is.EqualTo(2).Within(1e-9));
        }

        private static double MashBlast(SkillTestHero hero, double pressureAtTheEnd)
        {
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(hero.State.BurnStacks, Is.Zero);
            var slot = hero.State.Skills[0];
            var ticks = slot.ActiveTicks;
            hero.Ticks(ticks - 1);
            hero.State.Resource = pressureAtTheEnd;
            hero.Loop.Events.Clear();
            hero.Ticks(1);
            Assert.That(slot.Active, Is.False);
            Assert.That(hero.State.BurnStacks, Is.EqualTo(10).Within(0.5));
            return hero.Loop.Events.Count == 0 ? 0 : hero.DamageFrom(DamageSource.Skill);
        }

        [Test]
        public void MashBarrelBlowsUpAfterFiveSecondsAndHarderAboveSeventyPressure()
        {
            var runedancer = Stranger("mash_barrel");
            var dowser = SkillTestHero.Of(HeroClass.Dowser, "mash_barrel");

            Assert.That(MashBlast(runedancer, 100), Is.InRange(runedancer.SkillDamage("mash_barrel", 10), runedancer.SkillDamage("mash_barrel", 11)));
            Assert.That(MashBlast(dowser, 80), Is.InRange(dowser.SkillDamage("mash_barrel", 15), dowser.SkillDamage("mash_barrel", 16)));
        }

        [Test]
        public void ByEyeTakesTwentyPressureOffAndStopsOverheatForFiveSeconds()
        {
            var dowser = SkillTestHero.Of(HeroClass.Dowser, "by_eye");
            dowser.State.Resource = 50;

            Assert.That(dowser.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(dowser.State.Resource, Is.EqualTo(30).Within(1e-9));
            Assert.That(dowser.Loop.Hero.Value(CombatStats.OverheatDisabled, 0), Is.EqualTo(1));
            dowser.Ticks(5 * GameLoop.TicksPerSecond + 1);
            Assert.That(dowser.Loop.Hero.Value(CombatStats.OverheatDisabled, 0), Is.Zero);
        }

        [Test]
        public void BrokenGaugeIsStrongerBetweenFiftyAndNinetyPressure()
        {
            var dowser = SkillTestHero.Of(HeroClass.Dowser, "broken_gauge");
            dowser.State.Resource = 60;
            dowser.Ticks(1);
            var inWindow = dowser.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.DoT);
            dowser.State.Resource = 95;
            dowser.Ticks(1);
            var above = dowser.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.DoT);

            Assert.That(inWindow / above, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void BrokenGaugeBlowsTheStillUpAtFullPressureInsteadOfJamming()
        {
            var dowser = SkillTestHero.Of(HeroClass.Dowser, "fuse_bottle", "broken_gauge");
            dowser.Ticks(1);
            dowser.State.Resource = 90;

            dowser.Input.ClickLeft();
            dowser.Ticks(1);

            Assert.That(dowser.Loop.Overload.Bursts, Is.EqualTo(1));
            Assert.That(dowser.State.Resource, Is.LessThan(5));
        }

        [Test]
        public void ExhaleBurnsTheWholeQueueAndScrapSawSetsFireWithEveryKey()
        {
            var dowser = SkillTestHero.Of(HeroClass.Dowser, "exhale", "scrap_saw");
            dowser.State.Resource = 0;
            Assert.That(dowser.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(dowser.Cast(1), Is.EqualTo(CastResult.Cast));
            dowser.Ticks(1);
            Assert.That(dowser.Loop.Hero.Value(CombatStats.DotQueueShare, 0, StatTags.DoT), Is.EqualTo(1).Within(1e-9));
            var burn = dowser.State.BurnStacks;

            dowser.Ticks(1, keysPerTick: 1);

            Assert.That(dowser.State.BurnStacks - burn, Is.GreaterThanOrEqualTo(1));
        }
    }
}
