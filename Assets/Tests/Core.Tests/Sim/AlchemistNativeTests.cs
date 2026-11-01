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
    public class AlchemistNativeTests
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

        private static double PoisonAfterKill(SkillTestHero hero)
        {
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            hero.State.PoisonStacks = 20;
            hero.KillCurrentEnemy();
            hero.Ticks(2);
            return hero.State.PoisonStacks;
        }

        [Test]
        public void WorseFirstSendsHalfThePoisonOnAndThePlagueDoctorSendsAll()
        {
            Assert.That(PoisonAfterKill(Stranger("worse_first")), Is.EqualTo(10).Within(1));
            Assert.That(PoisonAfterKill(SkillTestHero.Of(HeroClass.Alchemist, "worse_first")), Is.EqualTo(20).Within(1));
        }

        [Test]
        public void CaneJabDoublesPoisonDamageForFiveSeconds()
        {
            var doctor = SkillTestHero.Of(HeroClass.Alchemist, "cane_jab");
            Assert.That(doctor.Cast(0), Is.EqualTo(CastResult.Cast));
            doctor.Ticks(1);
            Assert.That(doctor.Loop.Hero.Value(CombatStats.DotDamage, 1, StatTags.DoT), Is.EqualTo(2).Within(1e-9));

            doctor.Ticks(5 * GameLoop.TicksPerSecond);
            Assert.That(doctor.Loop.Hero.Value(CombatStats.DotDamage, 1, StatTags.DoT), Is.EqualTo(1).Within(1e-9));
        }

        [TestCase(25, 95)]
        [TestCase(0, 90)]
        public void CaneJabGivesFiveReagentsOnTwentyStacks(double stacks, double resource)
        {
            var doctor = SkillTestHero.Of(HeroClass.Alchemist, "cane_jab");
            doctor.State.PoisonStacks = stacks;

            Assert.That(doctor.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(doctor.State.Resource, Is.EqualTo(resource).Within(1e-9));
        }

        [Test]
        public void LyovaLeavesWithTheEnemyUnlessHeIsThePlagueDoctorsAssistant()
        {
            var runedancer = Stranger("assistant_lyova");
            var doctor = SkillTestHero.Of(HeroClass.Alchemist, "assistant_lyova");
            foreach (var hero in new[] { runedancer, doctor })
            {
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                hero.KillCurrentEnemy();
                hero.Ticks(1);
            }

            Assert.That(runedancer.State.Skills[0].Active, Is.False);
            Assert.That(doctor.State.Skills[0].Active, Is.True);
        }

        private static double FirstKeyHit(SkillTestHero hero, double poison)
        {
            hero.Ticks(1);
            hero.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            hero.State.PoisonStacks = poison;
            hero.Loop.Events.Clear();
            hero.Input.PressKeys(1);
            hero.Loop.Tick();
            return Enumerable.Range(0, hero.Loop.Events.Count).Select(i => hero.Loop.Events[i])
                .First(e => e.Kind == GameEventKind.Damage && e.Source == DamageSource.Keyboard).Amount.ToDouble();
        }

        [Test]
        public void DiagnosisAddsFivePercentPerTenPoisonStacksForThePlagueDoctor()
        {
            var sick = SkillTestHero.Of(HeroClass.Alchemist, "diagnosis");
            var healthy = SkillTestHero.Of(HeroClass.Alchemist, "diagnosis");

            Assert.That(FirstKeyHit(sick, 25) / FirstKeyHit(healthy, 0), Is.EqualTo(1.1).Within(1e-6));
        }

        [Test]
        public void NotEnoughPatienceKeepsTheStacksAndThePlagueDoctorGrowsThem()
        {
            var doctor = SkillTestHero.Of(HeroClass.Alchemist, "not_enough_patience");
            var runedancer = Stranger("not_enough_patience");
            foreach (var hero in new[] { doctor, runedancer })
            {
                hero.State.PoisonStacks = 10;
                hero.Loop.Events.Clear();
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                Assert.That(hero.DamageFrom(DamageSource.Skill), Is.GreaterThan(0));
            }

            Assert.That(doctor.State.PoisonStacks, Is.EqualTo(12).Within(1e-9));
            Assert.That(runedancer.State.PoisonStacks, Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void QuarantineRoundAndRatTideOpen()
        {
            var doctor = SkillTestHero.Of(HeroClass.Alchemist, "quarantine_round", "rat_tide");
            Assert.That(doctor.Cast(0), Is.EqualTo(CastResult.Cast));
            doctor.State.Resource = 100;
            Assert.That(doctor.Cast(1), Is.EqualTo(CastResult.Cast));
            doctor.Ticks(1);

            Assert.That(doctor.Loop.Hero.Value(CombatStats.DotSpread, 0, StatTags.DoT), Is.EqualTo(0.75).Within(1e-9));
            Assert.That(doctor.Loop.Hero.Value(CombatStats.DotQueueShare, 0, StatTags.DoT), Is.EqualTo(1).Within(1e-9));
        }
    }
}
