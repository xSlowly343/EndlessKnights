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
    public class GhostcaptainNativeTests
    {
        private static SummonEffect Cannon => (SummonEffect)TestWorld.Data.Gems["cannon_by_name"].Skill!.Effect;

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

        private static double FirstKeyHit(SkillTestHero hero, int shots)
        {
            hero.Ticks(1);
            hero.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            hero.State.CannonShotsOnEnemy = shots;
            hero.Loop.Events.Clear();
            hero.Input.PressKeys(1);
            hero.Loop.Tick();
            return Enumerable.Range(0, hero.Loop.Events.Count).Select(i => hero.Loop.Events[i])
                .First(e => e.Kind == GameEventKind.Damage && e.Source == DamageSource.Keyboard).Amount.ToDouble();
        }

        [Test]
        public void ChainShotAddsOnePercentPerShotUpToFifteenAndTwentyFiveForTheCaptain()
        {
            Assert.That(FirstKeyHit(SkillTestHero.Of(HeroClass.Ghostcaptain, "chain_shot"), 40) / FirstKeyHit(SkillTestHero.Of(HeroClass.Ghostcaptain, "chain_shot"), 0), Is.EqualTo(1.25).Within(1e-6));
            Assert.That(FirstKeyHit(Stranger("chain_shot"), 40) / FirstKeyHit(Stranger("chain_shot"), 0), Is.EqualTo(1.15).Within(1e-6));
            Assert.That(FirstKeyHit(Stranger("chain_shot"), 5) / FirstKeyHit(Stranger("chain_shot"), 0), Is.EqualTo(1.05).Within(1e-6));
        }

        [Test]
        public void CannonShotsCountOnTheEnemy()
        {
            var ghostcaptain = SkillTestHero.Of(HeroClass.Ghostcaptain);
            ghostcaptain.Loop.Skills.Minions.Place(Cannon, StatTags.Skill);

            ghostcaptain.Ticks(3 * GameLoop.TicksPerSecond);

            Assert.That(ghostcaptain.State.CannonShotsOnEnemy, Is.EqualTo(3));
        }

        [Test]
        public void ParrotSendsThirtyPercentOfCannonShotsToTheNextEnemyAndBringsPowder()
        {
            var ghostcaptain = SkillTestHero.Of(HeroClass.Ghostcaptain, "parrot_with_five_words");
            ghostcaptain.Ticks(1);
            ghostcaptain.Loop.Skills.Minions.Place(Cannon, StatTags.Skill);

            ghostcaptain.Ticks(GameLoop.TicksPerSecond);

            Assert.That(ghostcaptain.Queued(0), Is.GreaterThan(0));
            Assert.That(ghostcaptain.Queued(1), Is.Zero);
            Assert.That(ghostcaptain.Loop.Hero.Value(CombatStats.ResourcePerSecond, 0), Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void BarrelBlowsUpOnTheNextCannonShotAndTheCaptainSetsTwo()
        {
            var ghostcaptain = SkillTestHero.Of(HeroClass.Ghostcaptain, "barrel_from_the_hold");
            var runedancer = Stranger("barrel_from_the_hold");
            Assert.That(ghostcaptain.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(ghostcaptain.State.Barrels, Is.EqualTo(2));
            Assert.That(runedancer.State.Barrels, Is.EqualTo(1));

            ghostcaptain.Loop.Skills.Minions.Place(Cannon, StatTags.Skill);
            ghostcaptain.Ticks(GameLoop.TicksPerSecond);

            Assert.That(ghostcaptain.State.Barrels, Is.EqualTo(1));
        }

        [Test]
        public void BarrelBlowsUpByItselfAfterFiveSeconds()
        {
            var runedancer = Stranger("barrel_from_the_hold");
            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));
            runedancer.Ticks(5 * GameLoop.TicksPerSecond - 1);
            runedancer.Loop.Events.Clear();

            runedancer.Ticks(1);

            Assert.That(runedancer.State.Barrels, Is.Zero);
            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.EqualTo(runedancer.Loop.Combat.SkillDamage(TestWorld.Data.Combat.KeyDamage, 8, StatTags.Skill).ToDouble()).Within(1e-6));
        }

        [Test]
        public void ShipAgroundAddsFiveBallsPerThousandPixelsForTheCaptain()
        {
            var ghostcaptain = SkillTestHero.Of(HeroClass.Ghostcaptain, "ship_aground");
            for (var i = 0; i < 10; i++)
            {
                ghostcaptain.Input.MoveMouse(300);
                ghostcaptain.Loop.Tick();
            }
            ghostcaptain.State.Resource = 100;

            Assert.That(ghostcaptain.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(ghostcaptain.State.Skills[0].HitsLeft, Is.EqualTo(45));
        }

        [Test]
        public void OneMoreCannonDropsABarrelEveryFifthShot()
        {
            var ghostcaptain = SkillTestHero.Of(HeroClass.Ghostcaptain, "one_more_cannon");
            Assert.That(ghostcaptain.Cast(0), Is.EqualTo(CastResult.Cast));
            ghostcaptain.Ticks(1);

            Assert.That(ghostcaptain.Loop.Hero.Value(CombatStats.CannonBarrelEvery, 0, StatTags.Minion), Is.EqualTo(5));
            Assert.That(ghostcaptain.Loop.Hero.Value(CombatStats.CannonBarrelMultiplier, 0, StatTags.Minion), Is.EqualTo(8));
        }

        [Test]
        public void FigureheadAndTheNeighbourFromBelowOpen()
        {
            var ghostcaptain = SkillTestHero.Of(HeroClass.Ghostcaptain, "figurehead", "neighbour_from_below");
            Assert.That(ghostcaptain.Cast(0), Is.EqualTo(CastResult.Cast));
            ghostcaptain.State.Resource = 100;
            Assert.That(ghostcaptain.Cast(1), Is.EqualTo(CastResult.Cast));

            ghostcaptain.Ticks(GameLoop.TicksPerSecond);

            Assert.That(ghostcaptain.Loop.Skills.Minions.Standing(MinionKind.Beast), Is.EqualTo(1));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(ghostcaptain.Queued(i), Is.GreaterThan(0));
        }
    }
}
