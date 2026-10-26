using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class VikingSkillTests
    {
        [Test]
        public void ShieldBashKeepsFuryFromBurningForFiveSeconds()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "shield_bash");

            viking.Cast(0);
            viking.Ticks(45);
            Assert.That(viking.State.Resource, Is.EqualTo(85));

            viking.Ticks(20);
            Assert.That(viking.State.Resource, Is.LessThan(85));
        }

        [TestCase(100, 10)]
        [TestCase(80, 8)]
        [TestCase(50, 5)]
        public void OdinsFurySpendsAllFuryForTheVikingAndScalesWithIt(double fury, double multiplier)
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "odins_fury");
            viking.State.Resource = fury;

            viking.Cast(0);

            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(viking.KeyDamage * multiplier).Within(1e-9));
            Assert.That(viking.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void OdinsFuryWithABorrowedAxeHasAFixedCostAndForce()
        {
            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Viking, "odins_fury");

            Assert.That(ronin.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(ronin.Loop.Skills.IsNative(0), Is.False);
            Assert.That(ronin.DamageFrom(DamageSource.Skill), Is.EqualTo(ronin.SkillDamage("odins_fury", 5)).Within(1e-9));
            Assert.That(ronin.State.Resource, Is.EqualTo(50));
        }

        [Test]
        public void BattleCryRaisesKeyDamageForFifteenSeconds()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "battle_cry");
            var before = viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            viking.Cast(0);
            Assert.That(viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before + 30).Within(1e-9));
            Assert.That(viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));

            viking.Ticks(150);
            Assert.That(viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void EveryKeyHitInTheVikingsBattleCryGivesOneMoreFury()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "battle_cry");
            viking.State.Resource = 30;
            viking.Cast(0);

            viking.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("viking").PerKey;
            Assert.That(viking.State.Resource, Is.EqualTo(5 * perKey + 5).Within(1e-9));
        }

        [Test]
        public void ThrowingAxeSparesTheCurrentEnemyAndWoundsTheNext()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "throwing_axe");
            var hp = viking.State.EnemyHp;

            viking.Cast(0);

            Assert.That(viking.State.EnemyHp, Is.EqualTo(hp));
            Assert.That(viking.Queued(0), Is.EqualTo(viking.SkillDamage("throwing_axe", 2)).Within(1e-9));
        }

        [Test]
        public void WoundedEnemyGivesTheVikingTenFuryWhenItAppears()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "throwing_axe");
            viking.State.Resource = 20;
            viking.Cast(0);

            viking.KillCurrentEnemy();
            var afterKill = viking.State.Resource;
            viking.Ticks(1);

            Assert.That(viking.Queued(0), Is.EqualTo(0));
            Assert.That(viking.State.Resource, Is.EqualTo(afterKill + 10).Within(1e-9));
        }

        [Test]
        public void WoundedEnemyGivesNoFuryToABorrowedAxe()
        {
            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Viking, "throwing_axe");
            ronin.State.Resource = 20;
            ronin.Cast(0);

            ronin.KillCurrentEnemy();
            var afterKill = ronin.State.Resource;
            ronin.Ticks(1);

            Assert.That(ronin.State.Resource, Is.EqualTo(afterKill).Within(1e-9));
        }

        [Test]
        public void BloodHarvestIsPassiveAndStacksOnKillsUpToFive()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "blood_harvest");
            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Passive));

            viking.KillCurrentEnemy();
            viking.Ticks(1);
            Assert.That(viking.State.Skills[0].Stacks, Is.EqualTo(1));
            Assert.That(viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(105).Within(1e-9));

            for (var i = 0; i < 6; i++)
                viking.KillCurrentEnemy();
            viking.Ticks(1);
            Assert.That(viking.State.Skills[0].Stacks, Is.EqualTo(5));
            Assert.That(viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(125).Within(1e-9));
        }

        [Test]
        public void BloodHarvestStacksFallOffTenSecondsAfterTheLastKill()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "blood_harvest");
            viking.KillCurrentEnemy();
            viking.Ticks(1);
            viking.State.Resource = 0;

            viking.Ticks(95);
            Assert.That(viking.State.Skills[0].Stacks, Is.EqualTo(1));
            viking.Ticks(10);
            Assert.That(viking.State.Skills[0].Stacks, Is.EqualTo(0));
            Assert.That(viking.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void BloodHarvestStacksHoldWhileTheVikingsFuryIsAboveFifty()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "blood_harvest");
            viking.KillCurrentEnemy();

            for (var i = 0; i < 300; i++)
            {
                viking.State.Resource = 80;
                viking.Ticks(1);
            }

            Assert.That(viking.State.Skills[0].Stacks, Is.EqualTo(1));
        }

        [Test]
        public void ShieldWallReleasesHalfOfTheStoredHitsWhenItEnds()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "shield_wall");
            viking.Cast(0);
            var duration = (int)(TestWorld.Data.Gems["shield_wall"].Skill!.DurationSeconds * GameLoop.TicksPerSecond);

            viking.Ticks(duration - 1, keysPerTick: 2);
            var keys = viking.DamageFrom(DamageSource.Keyboard);
            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(0));

            viking.Ticks(1);
            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(keys * 0.5).Within(keys * 1e-9));
        }

        [Test]
        public void FuryGrowsTwiceAsFastInTheVikingsShieldWall()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "shield_wall");
            viking.State.Resource = 40;
            viking.Cast(0);

            viking.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("viking").PerKey;
            Assert.That(viking.State.Resource, Is.EqualTo(5 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void RagnarokRoarSendsFortyPercentOfEveryKeyHitToTheWholeQueue()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "ragnarok_roar");
            viking.State.Resource = 70;
            viking.Cast(0);

            viking.Ticks(10, keysPerTick: 3);

            var keys = viking.DamageFrom(DamageSource.Keyboard);
            for (var position = 0; position < GameState.QueueDepth; position++)
                Assert.That(viking.Queued(position), Is.EqualTo(keys * 0.4).Within(keys * 1e-9), $"position {position}");
        }

        [TestCase(70, 80)]
        [TestCase(100, 160)]
        public void RagnarokRoarLastsTwiceAsLongAtFullFury(double fury, int ticks)
        {
            var viking = SkillTestHero.Of(HeroClass.Viking, "ragnarok_roar");
            viking.State.Resource = fury;

            viking.Cast(0);

            Assert.That(viking.State.Skills[0].ActiveTicks, Is.EqualTo(ticks));
        }

        [Test]
        public void QueuedDamageLandsOnEachNextEnemyInTurn()
        {
            var viking = SkillTestHero.Of(HeroClass.Viking);
            viking.State.QueueDamage[0] = BigNumber.Create(1, 60);
            viking.State.QueueDamage[1] = BigNumber.Create(1, 60);

            viking.KillCurrentEnemy();

            Assert.That(viking.Loop.Events.CountOf(GameEventKind.EnemyKilled), Is.EqualTo(3));
            Assert.That(viking.Queued(0), Is.EqualTo(0));
            Assert.That(viking.Queued(1), Is.EqualTo(0));
        }
    }
}
