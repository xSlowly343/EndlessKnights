using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SoulbinderNativeTests
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

        private static void RaiseSkeletons(SkillTestHero hero, int count)
        {
            for (var i = 0; i < count; i++)
                hero.Loop.Skills.Minions.Place(TestWorld.Data.Combat.Paths.SkeletonOnKill, StatTags.Skill);
        }

        [Test]
        public void TheWholeShiftRaisesASkeletonFromEveryKillForTheGravedigger()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "the_whole_shift");
            var runedancer = Stranger("the_whole_shift");
            foreach (var hero in new[] { soulbinder, runedancer })
            {
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                hero.Ticks(1);
            }

            Assert.That(soulbinder.Loop.Hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion), Is.EqualTo(1));
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion), Is.Zero);
        }

        [Test]
        public void ShovelBlowIsTenPercentStrongerPerSkeletonForTheGravedigger()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "shovel_blow");
            var runedancer = Stranger("shovel_blow");
            RaiseSkeletons(soulbinder, 3);
            RaiseSkeletons(runedancer, 3);
            soulbinder.Loop.Events.Clear();

            Assert.That(soulbinder.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(soulbinder.DamageFrom(DamageSource.Skill), Is.EqualTo(soulbinder.SkillDamage("shovel_blow", 3 * 1.3)).Within(1e-6));
            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(runedancer.SkillDamage("shovel_blow", 3)).Within(1e-6));
        }

        [Test]
        public void ShovelBlowKillGivesFiveBack()
        {
            var killer = Stranger("shovel_blow");
            var miss = Stranger("shovel_blow");
            killer.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1));
            killer.State.Resource = 50;
            miss.State.Resource = 50;

            Assert.That(killer.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(miss.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(miss.State.Resource, Is.EqualTo(40).Within(1e-9));
            Assert.That(killer.State.Resource, Is.GreaterThanOrEqualTo(45 - 1e-9));
        }

        [Test]
        public void BoneScreeScattersTheOldestSkeletonAndTheGravediggerRaisesItAgain()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "bone_scree");
            var runedancer = Stranger("bone_scree");
            foreach (var hero in new[] { soulbinder, runedancer })
            {
                RaiseSkeletons(hero, 1);
                hero.Ticks(20);
                hero.State.Resource = 100;
                hero.Loop.Events.Clear();
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("bone_scree", 6)).Within(1e-6));
            }

            Assert.That(runedancer.Loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.Zero);
            Assert.That(soulbinder.Loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(1));
            Assert.That(soulbinder.State.Minions.First(m => m.Standing).TicksLeft, Is.EqualTo(60 * GameLoop.TicksPerSecond));
        }

        [Test]
        public void BoneScreeWithoutASkeletonDoesNothing()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "bone_scree");

            Assert.That(soulbinder.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(soulbinder.DamageFrom(DamageSource.Skill), Is.Zero);
        }

        [Test]
        public void PlentyOfWorkRaisesSkeletonsFromKills()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "plenty_of_work");
            soulbinder.Ticks(1);

            Assert.That(soulbinder.Loop.Hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion), Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void RestlessEarthMakesMinionsHalfAgainStronger()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "restless_earth");
            Assert.That(soulbinder.Cast(0), Is.EqualTo(CastResult.Cast));

            var modifiers = soulbinder.State.Skills[0].AuraModifiers;
            Assert.That(modifiers.Any(m => m.Stat == CombatStats.Damage && m.Kind == ModifierKind.More && m.Value == 0.5 && m.Tags == StatTags.Minion), Is.True);
        }

        [Test]
        public void TooMuchColossusRaisesAServantEveryTenthKey()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "too_much_colossus");
            Assert.That(soulbinder.Cast(0), Is.EqualTo(CastResult.Cast));

            for (var i = 0; i < 40; i++)
                soulbinder.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);

            Assert.That(soulbinder.Loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(2));
        }

        [Test]
        public void NightRoundBringsAWolfAndAPackOfUpToSix()
        {
            var soulbinder = SkillTestHero.Of(HeroClass.Soulbinder, "night_round");
            Assert.That(soulbinder.Cast(0), Is.EqualTo(CastResult.Cast));
            soulbinder.Ticks(1);

            Assert.That(soulbinder.Loop.Skills.Minions.Standing(MinionKind.Beast), Is.EqualTo(1));
            Assert.That(soulbinder.Loop.Skills.Minions.Limit(MinionKind.Beast), Is.EqualTo(6));
            Assert.That(soulbinder.Loop.Hero.Value(CombatStats.BeastOnKill, 0, StatTags.Minion), Is.EqualTo(1));
            Assert.That(TestWorld.Data.Gems["night_round"].Locked, Is.False);
        }
    }
}
