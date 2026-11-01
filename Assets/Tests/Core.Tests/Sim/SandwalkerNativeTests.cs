using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SandwalkerNativeTests
    {
        private static void ArmLeftWith(SkillTestHero hero, string gemId)
        {
            var type = TestWorld.Data.Gems[gemId].Skill!.Weapon;
            var weaponBase = hero.Loop.Data.Weapons.Bases.First(weapon => weapon.Type.Id == type);
            hero.State.Weapons.Left = new WeaponItem(weaponBase, ItemRarity.Simple, new[] { gemId });
        }

        private static double Dealt(SkillTestHero hero, System.Action act)
        {
            hero.Loop.Events.Clear();
            act();
            return hero.DamageFrom(DamageSource.Mouse) + hero.DamageFrom(DamageSource.Skill);
        }

        [Test]
        public void FathersScimitarGivesTwoPathOnAHit()
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "fathers_scimitar");
            sandwalker.State.Resource = 10;

            Assert.That(sandwalker.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(sandwalker.State.Resource, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void SandCompanionRepeatsTheMouseOnlyForTheNomad()
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "companion_of_sand");
            var still = SkillTestHero.Of(HeroClass.Sandwalker, "companion_of_sand");
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer);
            runedancer.Loop.Skills.Equip(TestWorld.Data.Gems["companion_of_sand"]);
            ArmLeftWith(runedancer, "companion_of_sand");
            runedancer.State.Resource = 100;
            Assert.That(sandwalker.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(runedancer.Cast(0), Is.EqualTo(CastResult.Cast));

            double Mouse(SkillTestHero hero) => Dealt(hero, () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    hero.Input.MoveMouse(1500);
                    hero.Loop.Tick();
                }
            });

            var plain = Mouse(still);
            Assert.That(Mouse(sandwalker) / plain, Is.EqualTo(1.5).Within(1e-6));
            Assert.That(Mouse(runedancer), Is.GreaterThan(0));
            Assert.That(runedancer.State.Skills[0].HitCount, Is.Zero);
        }

        [Test]
        public void SandFromThePocketsHitsTheWholeQueueAndGivesPath()
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "sand_from_the_pockets");
            var plain = SkillTestHero.Of(HeroClass.Sandwalker, "sand_from_the_pockets");
            Assert.That(sandwalker.Cast(0), Is.EqualTo(CastResult.Cast));
            sandwalker.State.Resource = 20;
            plain.State.Resource = 20;

            foreach (var hero in new[] { sandwalker, plain })
            {
                for (var i = 0; i < 5; i++)
                {
                    hero.Input.MoveMouse(200);
                    hero.Loop.Tick();
                }
            }

            Assert.That(sandwalker.State.Resource - plain.State.Resource, Is.EqualTo(5).Within(1e-6));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(sandwalker.Queued(i), Is.EqualTo(sandwalker.SkillDamage("sand_from_the_pockets", 5)).Within(1e-6));
        }

        private static SkillTestHero MirageThenKill(double waitSeconds)
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "mirage_at_the_well");
            Assert.That(sandwalker.Cast(0), Is.EqualTo(CastResult.Cast));
            sandwalker.KillCurrentEnemy();
            sandwalker.Ticks(2);
            Assert.That(sandwalker.State.EnemyWeakened, Is.True);
            sandwalker.Ticks((int)(waitSeconds * GameLoop.TicksPerSecond));
            sandwalker.KillCurrentEnemy();
            sandwalker.Ticks(1);
            return sandwalker;
        }

        [Test]
        public void MirageCoolsDownAtOnceWhenTheWeakenedEnemyDiesWithinTwoSeconds()
        {
            Assert.That(MirageThenKill(1).State.Skills[0].CooldownTicks, Is.Zero);
        }

        [Test]
        public void MirageKeepsItsCooldownWhenTheWeakenedEnemyLivesLonger()
        {
            Assert.That(MirageThenKill(3).State.Skills[0].CooldownTicks, Is.GreaterThan(0));
        }

        [Test]
        public void DifferentRoadAddsTwelvePercentForEveryInputKindOfTheLastTenSeconds()
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "a_different_road_each_time");
            sandwalker.Input.PressKeys(1);
            sandwalker.Input.MoveMouse(10);
            sandwalker.Loop.Tick();
            Assert.That(sandwalker.Loop.Combat.RecentInputKinds(), Is.EqualTo(2));

            sandwalker.Input.ClickLeft();
            sandwalker.Loop.Tick();
            Assert.That(sandwalker.Loop.Combat.RecentInputKinds(), Is.EqualTo(3));
            Assert.That(sandwalker.Loop.Hero.Value(CombatStats.DamagePerInputKind, 0), Is.EqualTo(0.12).Within(1e-9));

            sandwalker.Ticks(10 * GameLoop.TicksPerSecond);
            Assert.That(sandwalker.Loop.Combat.RecentInputKinds(), Is.Zero);
        }

        [Test]
        public void WaterSupplyStrikesFivePerInputKindAndReturnsHalfThePath()
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "water_supply");
            Assert.That(sandwalker.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = sandwalker.State.Skills[0];
            var ticks = slot.ActiveTicks;
            for (var i = 0; i < ticks - 1; i++)
            {
                if (i == 0)
                {
                    sandwalker.Input.PressKeys(1);
                    sandwalker.Input.MoveMouse(10);
                }
                sandwalker.Loop.Tick();
            }
            sandwalker.Loop.Events.Clear();
            var resource = sandwalker.State.Resource;

            sandwalker.Loop.Tick();

            Assert.That(slot.Active, Is.False);
            Assert.That(sandwalker.DamageFrom(DamageSource.Skill), Is.EqualTo(sandwalker.SkillDamage("water_supply", 10)).Within(1e-6));
            Assert.That(sandwalker.State.Resource - resource, Is.EqualTo(35).Within(1e-6));
        }

        [Test]
        public void OwnStormThrowsThirtyPercentOfKeyHitsAtTheWholeQueue()
        {
            var sandwalker = SkillTestHero.Of(HeroClass.Sandwalker, "own_storm");
            Assert.That(sandwalker.Cast(0), Is.EqualTo(CastResult.Cast));
            sandwalker.Ticks(1);
            sandwalker.Loop.Events.Clear();

            sandwalker.Input.PressKeys(1);
            sandwalker.Loop.Tick();

            var key = sandwalker.DamageFrom(DamageSource.Keyboard);
            Assert.That(key, Is.GreaterThan(0));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(sandwalker.Queued(i), Is.EqualTo(key * 0.3).Within(key * 1e-9));
        }
    }
}
