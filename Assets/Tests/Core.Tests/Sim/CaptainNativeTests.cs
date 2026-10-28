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
    public class CaptainNativeTests
    {
        private static SummonEffect Cannon => (SummonEffect)TestWorld.Data.Gems["cannon_by_name"].Skill!.Effect;

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
            Assert.That(FirstKeyHit(SkillTestHero.Of(HeroClass.Captain, "chain_shot"), 40) / FirstKeyHit(SkillTestHero.Of(HeroClass.Captain, "chain_shot"), 0), Is.EqualTo(1.25).Within(1e-6));
            Assert.That(FirstKeyHit(Stranger("chain_shot"), 40) / FirstKeyHit(Stranger("chain_shot"), 0), Is.EqualTo(1.15).Within(1e-6));
            Assert.That(FirstKeyHit(Stranger("chain_shot"), 5) / FirstKeyHit(Stranger("chain_shot"), 0), Is.EqualTo(1.05).Within(1e-6));
        }

        [Test]
        public void CannonShotsCountOnTheEnemy()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain);
            captain.Loop.Skills.Minions.Place(Cannon, StatTags.Skill);

            captain.Ticks(3 * GameLoop.TicksPerSecond);

            Assert.That(captain.State.CannonShotsOnEnemy, Is.EqualTo(3));
        }

        [Test]
        public void ParrotSendsThirtyPercentOfCannonShotsToTheNextEnemyAndBringsPowder()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain, "parrot_with_five_words");
            captain.Ticks(1);
            captain.Loop.Skills.Minions.Place(Cannon, StatTags.Skill);

            captain.Ticks(GameLoop.TicksPerSecond);

            Assert.That(captain.Queued(0), Is.GreaterThan(0));
            Assert.That(captain.Queued(1), Is.Zero);
            Assert.That(captain.Loop.Hero.Value(CombatStats.ResourcePerSecond, 0), Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void BarrelBlowsUpOnTheNextCannonShotAndTheCaptainSetsTwo()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain, "barrel_from_the_hold");
            var viking = Stranger("barrel_from_the_hold");
            Assert.That(captain.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(captain.State.Barrels, Is.EqualTo(2));
            Assert.That(viking.State.Barrels, Is.EqualTo(1));

            captain.Loop.Skills.Minions.Place(Cannon, StatTags.Skill);
            captain.Ticks(GameLoop.TicksPerSecond);

            Assert.That(captain.State.Barrels, Is.EqualTo(1));
        }

        [Test]
        public void BarrelBlowsUpByItselfAfterFiveSeconds()
        {
            var viking = Stranger("barrel_from_the_hold");
            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));
            viking.Ticks(5 * GameLoop.TicksPerSecond - 1);
            viking.Loop.Events.Clear();

            viking.Ticks(1);

            Assert.That(viking.State.Barrels, Is.Zero);
            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.EqualTo(viking.Loop.Combat.SkillDamage(TestWorld.Data.Combat.KeyDamage, 8, StatTags.Skill).ToDouble()).Within(1e-6));
        }

        [Test]
        public void ShipAgroundAddsFiveBallsPerThousandPixelsForTheCaptain()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain, "ship_aground");
            for (var i = 0; i < 10; i++)
            {
                captain.Input.MoveMouse(300);
                captain.Loop.Tick();
            }
            captain.State.Resource = 100;

            Assert.That(captain.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(captain.State.Skills[0].HitsLeft, Is.EqualTo(45));
        }

        [Test]
        public void OneMoreCannonDropsABarrelEveryFifthShot()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain, "one_more_cannon");
            Assert.That(captain.Cast(0), Is.EqualTo(CastResult.Cast));
            captain.Ticks(1);

            Assert.That(captain.Loop.Hero.Value(CombatStats.CannonBarrelEvery, 0, StatTags.Minion), Is.EqualTo(5));
            Assert.That(captain.Loop.Hero.Value(CombatStats.CannonBarrelMultiplier, 0, StatTags.Minion), Is.EqualTo(8));
        }

        [Test]
        public void FigureheadAndTheNeighbourFromBelowOpen()
        {
            var captain = SkillTestHero.Of(HeroClass.Captain, "figurehead", "neighbour_from_below");
            Assert.That(captain.Cast(0), Is.EqualTo(CastResult.Cast));
            captain.State.Resource = 100;
            Assert.That(captain.Cast(1), Is.EqualTo(CastResult.Cast));

            captain.Ticks(GameLoop.TicksPerSecond);

            Assert.That(captain.Loop.Skills.Minions.Standing(MinionKind.Beast), Is.EqualTo(1));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(captain.Queued(i), Is.GreaterThan(0));
        }
    }
}
