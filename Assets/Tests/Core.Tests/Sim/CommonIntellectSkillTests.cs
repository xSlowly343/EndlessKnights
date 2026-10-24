using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CommonIntellectSkillTests
    {
        private static SkillTestHero Hero(params string[] skills) => SkillTestHero.Of(HeroClass.Ronin, skills);

        private static void PressKeys(SkillTestHero hero, int keys)
        {
            for (var i = 0; i < keys; i++)
            {
                hero.Input.PressKeys(1);
                hero.Ticks(2);
            }
        }

        private static int Crits(SkillTestHero hero)
        {
            var crits = 0;
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                if (hero.Loop.Events[i].Kind == GameEventKind.Damage && (hero.Loop.Events[i].Flags & HitFlags.Crit) != 0)
                    crits++;
            }
            return crits;
        }

        [Test]
        public void MarkMakesEveryFifthHitACrit()
        {
            var hero = Hero("mark");
            PressKeys(hero, 5);
            Assert.That(Crits(hero), Is.EqualTo(0));

            hero.Cast(0);
            hero.Loop.Events.Clear();
            PressKeys(hero, 10);

            Assert.That(Crits(hero), Is.EqualTo(2));
        }

        [Test]
        public void WeakSpotDoublesCritDamage()
        {
            var hero = Hero("weak_spot");
            var before = hero.Loop.Combat.CritMultiplier(StatTags.Keyboard);

            hero.Cast(0);

            Assert.That(hero.Loop.Combat.CritMultiplier(StatTags.Keyboard), Is.EqualTo(before * 2).Within(1e-9));
        }

        [Test]
        public void ExecuteKillsAWoundedEnemyOutright()
        {
            var hero = Hero("execute");
            var kills = hero.State.Kills;
            hero.State.EnemyHp = hero.State.EnemyMaxHp * 0.15;

            hero.Cast(0);

            Assert.That(hero.State.Kills, Is.EqualTo(kills + 1));
        }

        [Test]
        public void ExecuteStrikesThreeTimesAsHardAboveTheThreshold()
        {
            var hero = Hero("execute");

            hero.Cast(0);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("execute", 3)).Within(1e-6));
        }

        [Test]
        public void ExposureMakesSkillsHitHarder()
        {
            var hero = Hero("exposure");

            hero.Cast(0);

            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.Skill), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.EqualTo(1));
        }

        [Test]
        public void BossHuntHitsBossesHarderAndHarderStillAtTheEnd()
        {
            var hero = Hero("boss_hunt");
            hero.Ticks(1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1));

            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = 300;
            hero.Ticks(1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.15).Within(1e-9));

            hero.State.BossTicksLeft = 50;
            hero.Ticks(1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.30).Within(1e-9));
        }

        [Test]
        public void RicochetStrikesTwiceAsHardAndBouncesToTheNextEnemy()
        {
            var hero = Hero("ricochet");

            hero.Cast(0);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("ricochet", 2)).Within(1e-6));
            Assert.That(hero.Queued(0), Is.EqualTo(hero.SkillDamage("ricochet", 1)).Within(1e-6));
        }

        [Test]
        public void LuckyCoinEitherStrikesSixTimesAsHardOrResetsItsCooldown()
        {
            var hero = Hero("lucky_coin");
            var heads = 0;
            var tails = 0;
            for (var i = 0; i < 40; i++)
            {
                hero.State.Resource = 100;
                hero.Loop.Events.Clear();
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                var damage = hero.DamageFrom(DamageSource.Skill);
                if (damage > 0)
                {
                    heads++;
                    Assert.That(damage, Is.EqualTo(hero.SkillDamage("lucky_coin", 6)).Within(1e-6));
                    hero.Ticks(60);
                }
                else
                {
                    tails++;
                    Assert.That(hero.State.Skills[0].Ready, Is.True);
                }
            }

            Assert.That(heads, Is.GreaterThan(0));
            Assert.That(tails, Is.GreaterThan(0));
        }

        [Test]
        public void ComposureStrengthensASkillThatWaitedLongerThanItsCooldown()
        {
            var hero = Hero("ricochet", "composure");
            hero.Ticks(40);

            hero.Cast(0);
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("ricochet", 3)).Within(1e-6));

            hero.Ticks(40);
            hero.Loop.Events.Clear();
            hero.Cast(0);
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("ricochet", 2)).Within(1e-6));
        }

        [Test]
        public void FreezeFrameStopsTheBossTimer()
        {
            var hero = Hero("freeze_frame");
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = 300;

            hero.Cast(0);
            hero.Ticks(50);
            Assert.That(hero.State.BossTicksLeft, Is.EqualTo(300));

            hero.Ticks(60);
            Assert.That(hero.State.BossTicksLeft, Is.LessThan(300));
        }
    }
}
