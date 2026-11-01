using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class RunedancerSkillTests
    {
        [Test]
        public void IronRimBashKeepsFuryFromBurningForFiveSeconds()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "iron_rim_bash");

            runedancer.Cast(0);
            runedancer.Ticks(45);
            Assert.That(runedancer.State.Resource, Is.EqualTo(85));

            runedancer.Ticks(20);
            Assert.That(runedancer.State.Resource, Is.LessThan(85));
        }

        [TestCase(100, 10)]
        [TestCase(80, 8)]
        [TestCase(50, 5)]
        public void NoMercySpendsAllFuryForTheVikingAndScalesWithIt(double fury, double multiplier)
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "no_mercy");
            runedancer.State.Resource = fury;

            runedancer.Cast(0);

            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(runedancer.KeyDamage * multiplier).Within(1e-9));
            Assert.That(runedancer.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void NoMercyWithABorrowedAxeHasAFixedCostAndForce()
        {
            var duelist = SkillTestHero.Borrowing(HeroClass.Duelist, HeroClass.Runedancer, "no_mercy");

            Assert.That(duelist.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(duelist.Loop.Skills.IsNative(0), Is.False);
            Assert.That(duelist.DamageFrom(DamageSource.Skill), Is.EqualTo(duelist.SkillDamage("no_mercy", 5)).Within(1e-9));
            Assert.That(duelist.State.Resource, Is.EqualTo(50));
        }

        [Test]
        public void RoarFromTheDoorRaisesKeyDamageForFifteenSeconds()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "roar_from_the_door");
            var before = runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            runedancer.Cast(0);
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before + 30).Within(1e-9));
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));

            runedancer.Ticks(150);
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void EveryKeyHitInTheVikingsRoarGivesOneMoreFury()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "roar_from_the_door");
            runedancer.State.Resource = 30;
            runedancer.Cast(0);

            runedancer.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("runedancer").PerKey;
            Assert.That(runedancer.State.Resource, Is.EqualTo(5 * perKey + 5).Within(1e-9));
        }

        [Test]
        public void HatchetTossSparesTheCurrentEnemyAndWoundsTheNext()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "hatchet_toss");
            var hp = runedancer.State.EnemyHp;

            runedancer.Cast(0);

            Assert.That(runedancer.State.EnemyHp, Is.EqualTo(hp));
            Assert.That(runedancer.Queued(0), Is.EqualTo(runedancer.SkillDamage("hatchet_toss", 2)).Within(1e-9));
        }

        [Test]
        public void WoundedEnemyGivesTheVikingTenFuryWhenItAppears()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "hatchet_toss");
            runedancer.State.Resource = 20;
            runedancer.Cast(0);

            runedancer.KillCurrentEnemy();
            var afterKill = runedancer.State.Resource;
            runedancer.Ticks(1);

            Assert.That(runedancer.Queued(0), Is.EqualTo(0));
            Assert.That(runedancer.State.Resource, Is.EqualTo(afterKill + 10).Within(1e-9));
        }

        [Test]
        public void WoundedEnemyGivesNoFuryToABorrowedAxe()
        {
            var duelist = SkillTestHero.Borrowing(HeroClass.Duelist, HeroClass.Runedancer, "hatchet_toss");
            duelist.State.Resource = 20;
            duelist.Cast(0);

            duelist.KillCurrentEnemy();
            var afterKill = duelist.State.Resource;
            duelist.Ticks(1);

            Assert.That(duelist.State.Resource, Is.EqualTo(afterKill).Within(1e-9));
        }

        [Test]
        public void ForgeHeatIsPassiveAndStacksOnKillsUpToFive()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "forge_heat");
            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Passive));

            runedancer.KillCurrentEnemy();
            runedancer.Ticks(1);
            Assert.That(runedancer.State.Skills[0].Stacks, Is.EqualTo(1));
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(105).Within(1e-9));

            for (var i = 0; i < 6; i++)
                runedancer.KillCurrentEnemy();
            runedancer.Ticks(1);
            Assert.That(runedancer.State.Skills[0].Stacks, Is.EqualTo(5));
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(125).Within(1e-9));
        }

        [Test]
        public void ForgeHeatStacksFallOffTenSecondsAfterTheLastKill()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "forge_heat");
            runedancer.KillCurrentEnemy();
            runedancer.Ticks(1);
            runedancer.State.Resource = 0;

            runedancer.Ticks(95);
            Assert.That(runedancer.State.Skills[0].Stacks, Is.EqualTo(1));
            runedancer.Ticks(10);
            Assert.That(runedancer.State.Skills[0].Stacks, Is.EqualTo(0));
            Assert.That(runedancer.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void ForgeHeatStacksHoldWhileTheVikingsFuryIsAboveFifty()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "forge_heat");
            runedancer.KillCurrentEnemy();

            for (var i = 0; i < 300; i++)
            {
                runedancer.State.Resource = 80;
                runedancer.Ticks(1);
            }

            Assert.That(runedancer.State.Skills[0].Stacks, Is.EqualTo(1));
        }

        [Test]
        public void AnvilReleasesHalfOfTheStoredHitsWhenItEnds()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "anvil");
            runedancer.Cast(0);
            var duration = (int)(TestWorld.Data.Gems["anvil"].Skill!.DurationSeconds * GameLoop.TicksPerSecond);

            runedancer.Ticks(duration - 1, keysPerTick: 2);
            var keys = runedancer.DamageFrom(DamageSource.Keyboard);
            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(0));

            runedancer.Ticks(1);
            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(keys * 0.5).Within(keys * 1e-9));
        }

        [Test]
        public void FuryGrowsTwiceAsFastAtTheVikingsAnvil()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "anvil");
            runedancer.State.Resource = 40;
            runedancer.Cast(0);

            runedancer.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("runedancer").PerKey;
            Assert.That(runedancer.State.Resource, Is.EqualTo(5 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void LongWinterSendsFortyPercentOfEveryKeyHitToTheWholeQueue()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "long_winter");
            runedancer.State.Resource = 70;
            runedancer.Cast(0);

            runedancer.Ticks(10, keysPerTick: 3);

            var keys = runedancer.DamageFrom(DamageSource.Keyboard);
            for (var position = 0; position < GameState.QueueDepth; position++)
                Assert.That(runedancer.Queued(position), Is.EqualTo(keys * 0.4).Within(keys * 1e-9), $"position {position}");
        }

        [TestCase(70, 80)]
        [TestCase(100, 160)]
        public void LongWinterLastsTwiceAsLongAtFullFury(double fury, int ticks)
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer, "long_winter");
            runedancer.State.Resource = fury;

            runedancer.Cast(0);

            Assert.That(runedancer.State.Skills[0].ActiveTicks, Is.EqualTo(ticks));
        }

        [Test]
        public void QueuedDamageLandsOnEachNextEnemyInTurn()
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer);
            runedancer.State.QueueDamage[0] = BigNumber.Create(1, 60);
            runedancer.State.QueueDamage[1] = BigNumber.Create(1, 60);

            runedancer.KillCurrentEnemy();

            Assert.That(runedancer.Loop.Events.CountOf(GameEventKind.EnemyKilled), Is.EqualTo(3));
            Assert.That(runedancer.Queued(0), Is.EqualTo(0));
            Assert.That(runedancer.Queued(1), Is.EqualTo(0));
        }
    }
}
