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
    public class GravediggerNativeTests
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

        private static void RaiseSkeletons(SkillTestHero hero, int count)
        {
            for (var i = 0; i < count; i++)
                hero.Loop.Skills.Minions.Place(TestWorld.Data.Combat.Paths.SkeletonOnKill, StatTags.Skill);
        }

        [Test]
        public void TheWholeShiftRaisesASkeletonFromEveryKillForTheGravedigger()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "the_whole_shift");
            var viking = Stranger("the_whole_shift");
            foreach (var hero in new[] { gravedigger, viking })
            {
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                hero.Ticks(1);
            }

            Assert.That(gravedigger.Loop.Hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion), Is.EqualTo(1));
            Assert.That(viking.Loop.Hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion), Is.Zero);
        }

        [Test]
        public void ShovelBlowIsTenPercentStrongerPerSkeletonForTheGravedigger()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "shovel_blow");
            var viking = Stranger("shovel_blow");
            RaiseSkeletons(gravedigger, 3);
            RaiseSkeletons(viking, 3);
            gravedigger.Loop.Events.Clear();

            Assert.That(gravedigger.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(gravedigger.DamageFrom(DamageSource.Skill), Is.EqualTo(gravedigger.SkillDamage("shovel_blow", 3 * 1.3)).Within(1e-6));
            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(viking.SkillDamage("shovel_blow", 3)).Within(1e-6));
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
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "bone_scree");
            var viking = Stranger("bone_scree");
            foreach (var hero in new[] { gravedigger, viking })
            {
                RaiseSkeletons(hero, 1);
                hero.Ticks(20);
                hero.State.Resource = 100;
                hero.Loop.Events.Clear();
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("bone_scree", 6)).Within(1e-6));
            }

            Assert.That(viking.Loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.Zero);
            Assert.That(gravedigger.Loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(1));
            Assert.That(gravedigger.State.Minions.First(m => m.Standing).TicksLeft, Is.EqualTo(60 * GameLoop.TicksPerSecond));
        }

        [Test]
        public void BoneScreeWithoutASkeletonDoesNothing()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "bone_scree");

            Assert.That(gravedigger.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(gravedigger.DamageFrom(DamageSource.Skill), Is.Zero);
        }

        [Test]
        public void PlentyOfWorkRaisesSkeletonsFromKills()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "plenty_of_work");
            gravedigger.Ticks(1);

            Assert.That(gravedigger.Loop.Hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion), Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void RestlessEarthMakesMinionsHalfAgainStronger()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "restless_earth");
            Assert.That(gravedigger.Cast(0), Is.EqualTo(CastResult.Cast));

            var modifiers = gravedigger.State.Skills[0].AuraModifiers;
            Assert.That(modifiers.Any(m => m.Stat == CombatStats.Damage && m.Kind == ModifierKind.More && m.Value == 0.5 && m.Tags == StatTags.Minion), Is.True);
        }

        [Test]
        public void TooMuchColossusRaisesAServantEveryTenthKey()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "too_much_colossus");
            Assert.That(gravedigger.Cast(0), Is.EqualTo(CastResult.Cast));

            for (var i = 0; i < 40; i++)
                gravedigger.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);

            Assert.That(gravedigger.Loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(2));
        }

        [Test]
        public void NightRoundBringsAWolfAndAPackOfUpToSix()
        {
            var gravedigger = SkillTestHero.Of(HeroClass.Gravedigger, "night_round");
            Assert.That(gravedigger.Cast(0), Is.EqualTo(CastResult.Cast));
            gravedigger.Ticks(1);

            Assert.That(gravedigger.Loop.Skills.Minions.Standing(MinionKind.Beast), Is.EqualTo(1));
            Assert.That(gravedigger.Loop.Skills.Minions.Limit(MinionKind.Beast), Is.EqualTo(6));
            Assert.That(gravedigger.Loop.Hero.Value(CombatStats.BeastOnKill, 0, StatTags.Minion), Is.EqualTo(1));
            Assert.That(TestWorld.Data.Gems["night_round"].Locked, Is.False);
        }
    }
}
