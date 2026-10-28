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
    public class NomadNativeTests
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
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "fathers_scimitar");
            nomad.State.Resource = 10;

            Assert.That(nomad.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(nomad.State.Resource, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void SandCompanionRepeatsTheMouseOnlyForTheNomad()
        {
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "companion_of_sand");
            var still = SkillTestHero.Of(HeroClass.Nomad, "companion_of_sand");
            var viking = SkillTestHero.Of(HeroClass.Viking);
            viking.Loop.Skills.Equip(TestWorld.Data.Gems["companion_of_sand"]);
            ArmLeftWith(viking, "companion_of_sand");
            viking.State.Resource = 100;
            Assert.That(nomad.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(viking.Cast(0), Is.EqualTo(CastResult.Cast));

            double Mouse(SkillTestHero hero) => Dealt(hero, () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    hero.Input.MoveMouse(1500);
                    hero.Loop.Tick();
                }
            });

            var plain = Mouse(still);
            Assert.That(Mouse(nomad) / plain, Is.EqualTo(1.5).Within(1e-6));
            Assert.That(Mouse(viking), Is.GreaterThan(0));
            Assert.That(viking.State.Skills[0].HitCount, Is.Zero);
        }

        [Test]
        public void SandFromThePocketsHitsTheWholeQueueAndGivesPath()
        {
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "sand_from_the_pockets");
            var plain = SkillTestHero.Of(HeroClass.Nomad, "sand_from_the_pockets");
            Assert.That(nomad.Cast(0), Is.EqualTo(CastResult.Cast));
            nomad.State.Resource = 20;
            plain.State.Resource = 20;

            foreach (var hero in new[] { nomad, plain })
            {
                for (var i = 0; i < 5; i++)
                {
                    hero.Input.MoveMouse(200);
                    hero.Loop.Tick();
                }
            }

            Assert.That(nomad.State.Resource - plain.State.Resource, Is.EqualTo(5).Within(1e-6));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(nomad.Queued(i), Is.EqualTo(nomad.SkillDamage("sand_from_the_pockets", 5)).Within(1e-6));
        }

        private static SkillTestHero MirageThenKill(double waitSeconds)
        {
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "mirage_at_the_well");
            Assert.That(nomad.Cast(0), Is.EqualTo(CastResult.Cast));
            nomad.KillCurrentEnemy();
            nomad.Ticks(2);
            Assert.That(nomad.State.EnemyWeakened, Is.True);
            nomad.Ticks((int)(waitSeconds * GameLoop.TicksPerSecond));
            nomad.KillCurrentEnemy();
            nomad.Ticks(1);
            return nomad;
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
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "a_different_road_each_time");
            nomad.Input.PressKeys(1);
            nomad.Input.MoveMouse(10);
            nomad.Loop.Tick();
            Assert.That(nomad.Loop.Combat.RecentInputKinds(), Is.EqualTo(2));

            nomad.Input.ClickLeft();
            nomad.Loop.Tick();
            Assert.That(nomad.Loop.Combat.RecentInputKinds(), Is.EqualTo(3));
            Assert.That(nomad.Loop.Hero.Value(CombatStats.DamagePerInputKind, 0), Is.EqualTo(0.12).Within(1e-9));

            nomad.Ticks(10 * GameLoop.TicksPerSecond);
            Assert.That(nomad.Loop.Combat.RecentInputKinds(), Is.Zero);
        }

        [Test]
        public void WaterSupplyStrikesFivePerInputKindAndReturnsHalfThePath()
        {
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "water_supply");
            Assert.That(nomad.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = nomad.State.Skills[0];
            var ticks = slot.ActiveTicks;
            for (var i = 0; i < ticks - 1; i++)
            {
                if (i == 0)
                {
                    nomad.Input.PressKeys(1);
                    nomad.Input.MoveMouse(10);
                }
                nomad.Loop.Tick();
            }
            nomad.Loop.Events.Clear();
            var resource = nomad.State.Resource;

            nomad.Loop.Tick();

            Assert.That(slot.Active, Is.False);
            Assert.That(nomad.DamageFrom(DamageSource.Skill), Is.EqualTo(nomad.SkillDamage("water_supply", 10)).Within(1e-6));
            Assert.That(nomad.State.Resource - resource, Is.EqualTo(35).Within(1e-6));
        }

        [Test]
        public void OwnStormThrowsThirtyPercentOfKeyHitsAtTheWholeQueue()
        {
            var nomad = SkillTestHero.Of(HeroClass.Nomad, "own_storm");
            Assert.That(nomad.Cast(0), Is.EqualTo(CastResult.Cast));
            nomad.Ticks(1);
            nomad.Loop.Events.Clear();

            nomad.Input.PressKeys(1);
            nomad.Loop.Tick();

            var key = nomad.DamageFrom(DamageSource.Keyboard);
            Assert.That(key, Is.GreaterThan(0));
            for (var i = 0; i < GameState.QueueDepth; i++)
                Assert.That(nomad.Queued(i), Is.EqualTo(key * 0.3).Within(key * 1e-9));
        }
    }
}
