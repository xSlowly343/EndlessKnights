using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MoonshinerNativeTests
    {
        private static SkillTestHero Stranger(string gem)
        {
            var viking = SkillTestHero.Of(HeroClass.Viking);
            var type = TestWorld.Data.Gems[gem].Skill!.Weapon;
            var weaponBase = viking.Loop.Data.Weapons.Bases.First(weapon => weapon.Type.Id == type);
            viking.State.Weapons.Left = new WeaponItem(weaponBase, ItemRarity.Simple, new[] { gem });
            viking.Loop.Skills.Equip(TestWorld.Data.Gems[gem]);
            viking.State.Resource = 100;
            viking.Loop.Events.Clear();
            return viking;
        }

        [Test]
        public void SteamWillTellHitsByThePressureAndEmptiesIt()
        {
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner, "the_steam_will_tell");
            moonshiner.State.Resource = 60;

            Assert.That(moonshiner.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(moonshiner.State.Resource, Is.Zero);
            Assert.That(moonshiner.DamageFrom(DamageSource.Skill), Is.EqualTo(moonshiner.SkillDamage("the_steam_will_tell", 6)).Within(1e-6));
            Assert.That(moonshiner.State.BurnStacks, Is.EqualTo(6).Within(1e-9));
        }

        [Test]
        public void SteamWillTellIsAPlainBlastForAnotherHero()
        {
            var viking = Stranger("the_steam_will_tell");

            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(viking.SkillDamage("the_steam_will_tell", 5)).Within(1e-6));
            Assert.That(viking.State.BurnStacks, Is.EqualTo(5).Within(1e-9));
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
            Assert.That(BurnAfterTwelveKeys(SkillTestHero.Of(HeroClass.Moonshiner, "first_drop")), Is.EqualTo(2).Within(1e-9));
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
            var viking = Stranger("mash_barrel");
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner, "mash_barrel");

            Assert.That(MashBlast(viking, 100), Is.InRange(viking.SkillDamage("mash_barrel", 10), viking.SkillDamage("mash_barrel", 11)));
            Assert.That(MashBlast(moonshiner, 80), Is.InRange(moonshiner.SkillDamage("mash_barrel", 15), moonshiner.SkillDamage("mash_barrel", 16)));
        }

        [Test]
        public void ByEyeTakesTwentyPressureOffAndStopsOverheatForFiveSeconds()
        {
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner, "by_eye");
            moonshiner.State.Resource = 50;

            Assert.That(moonshiner.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(moonshiner.State.Resource, Is.EqualTo(30).Within(1e-9));
            Assert.That(moonshiner.Loop.Hero.Value(CombatStats.OverheatDisabled, 0), Is.EqualTo(1));
            moonshiner.Ticks(5 * GameLoop.TicksPerSecond + 1);
            Assert.That(moonshiner.Loop.Hero.Value(CombatStats.OverheatDisabled, 0), Is.Zero);
        }

        [Test]
        public void BrokenGaugeIsStrongerBetweenFiftyAndNinetyPressure()
        {
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner, "broken_gauge");
            moonshiner.State.Resource = 60;
            moonshiner.Ticks(1);
            var inWindow = moonshiner.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.DoT);
            moonshiner.State.Resource = 95;
            moonshiner.Ticks(1);
            var above = moonshiner.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.DoT);

            Assert.That(inWindow / above, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void BrokenGaugeBlowsTheStillUpAtFullPressureInsteadOfJamming()
        {
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner, "fuse_bottle", "broken_gauge");
            moonshiner.Ticks(1);
            moonshiner.State.Resource = 90;

            moonshiner.Input.ClickLeft();
            moonshiner.Ticks(1);

            Assert.That(moonshiner.Loop.Overload.Bursts, Is.EqualTo(1));
            Assert.That(moonshiner.State.Resource, Is.LessThan(5));
        }

        [Test]
        public void ExhaleBurnsTheWholeQueueAndScrapSawSetsFireWithEveryKey()
        {
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner, "exhale", "scrap_saw");
            moonshiner.State.Resource = 0;
            Assert.That(moonshiner.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(moonshiner.Cast(1), Is.EqualTo(CastResult.Cast));
            moonshiner.Ticks(1);
            Assert.That(moonshiner.Loop.Hero.Value(CombatStats.DotQueueShare, 0, StatTags.DoT), Is.EqualTo(1).Within(1e-9));
            var burn = moonshiner.State.BurnStacks;

            moonshiner.Ticks(1, keysPerTick: 1);

            Assert.That(moonshiner.State.BurnStacks - burn, Is.GreaterThanOrEqualTo(1));
        }
    }
}
