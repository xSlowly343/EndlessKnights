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
    public class VoodooNativeTests
    {
        private static SkillTestHero Stranger(params string[] gems)
        {
            var viking = SkillTestHero.Of(HeroClass.Viking);
            var type = TestWorld.Data.Gems[gems[0]].Skill!.Weapon;
            var weaponBase = viking.Loop.Data.Weapons.Bases.First(weapon => weapon.Type.Id == type);
            viking.State.Weapons.Left = new WeaponItem(weaponBase, ItemRarity.Simple, gems);
            foreach (var gem in gems)
                viking.Loop.Skills.Equip(TestWorld.Data.Gems[gem]);
            viking.State.Resource = 100;
            viking.Loop.Events.Clear();
            return viking;
        }

        private static double Damage(SkillTestHero hero)
        {
            var total = 0.0;
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                if (hero.Loop.Events[i].Kind == GameEventKind.Damage)
                    total += hero.Loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        [Test]
        public void LongWishGivesThirtyPercentOfAllDamageBackAtTheEnd()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "long_wish");
            Assert.That(voodoo.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = voodoo.State.Skills[0];
            var ticks = slot.ActiveTicks;
            voodoo.Loop.Events.Clear();
            for (var i = 0; i < ticks - 1; i++)
                voodoo.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);
            var dealt = Damage(voodoo);
            var stored = voodoo.State.WishStored.ToDouble();
            voodoo.Loop.Events.Clear();

            voodoo.Loop.Tick();

            Assert.That(stored, Is.EqualTo(dealt * 0.3).Within(dealt * 1e-9));
            Assert.That(slot.Active, Is.False);
            Assert.That(voodoo.DamageFrom(DamageSource.Skill), Is.EqualTo(stored).Within(stored * 1e-9));
            Assert.That(voodoo.State.WishShare, Is.Zero);
        }

        [Test]
        public void LongWishPassesTheStoreToTheNextEnemyOnlyForTheVoodoo()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "long_wish");
            var viking = Stranger("long_wish");
            foreach (var hero in new[] { voodoo, viking })
            {
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                for (var i = 0; i < 10; i++)
                    hero.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);
            }
            var voodooStored = voodoo.State.WishStored.ToDouble();
            Assert.That(voodooStored, Is.GreaterThan(0));

            foreach (var hero in new[] { voodoo, viking })
            {
                hero.KillCurrentEnemy();
                hero.Loop.Events.Clear();
                hero.Ticks(1);
            }

            Assert.That(voodoo.DamageFrom(DamageSource.Skill), Is.GreaterThanOrEqualTo(voodooStored));
            Assert.That(viking.DamageFrom(DamageSource.Skill), Is.Zero);
            Assert.That(viking.State.WishStored.ToDouble(), Is.Zero);
        }

        [Test]
        public void PipeSmokeAddsThreeSecondsToTheBossTimerAndFiveForTheVoodoo()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "pipe_smoke");
            var viking = Stranger("pipe_smoke");
            foreach (var hero in new[] { voodoo, viking })
            {
                hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
                hero.State.BossTicksLeft = 300;
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            }

            Assert.That(voodoo.State.BossTicksLeft, Is.EqualTo(350));
            Assert.That(viking.State.BossTicksLeft, Is.EqualTo(330));
        }

        [Test]
        public void SwampDwellerAddsTwoPercentPerHitUpToTwenty()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "swamp_dweller");
            Assert.That(voodoo.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = voodoo.State.Skills[0];

            voodoo.Ticks(3 * GameLoop.TicksPerSecond);
            Assert.That(slot.StackModifier.Value, Is.EqualTo(0.06).Within(1e-9));

            voodoo.Ticks(15 * GameLoop.TicksPerSecond);
            Assert.That(slot.StackModifier.Value, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void SwampDwellerLivesWhileTheTaintStaysAboveFifty()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "swamp_dweller");
            Assert.That(voodoo.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = voodoo.State.Skills[0];
            var ticks = slot.ActiveTicks;

            for (var i = 0; i < ticks + 20; i++)
            {
                voodoo.State.Resource = 80;
                voodoo.Loop.Tick();
            }
            Assert.That(slot.Active, Is.True);

            voodoo.State.Resource = 10;
            voodoo.Ticks(2);
            Assert.That(slot.Active, Is.False);
            Assert.That(slot.Stacks, Is.Zero);
        }

        [Test]
        public void JamRipensMakesEveryCurseHitOncePerSecondForTheVoodoo()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "the_jam_ripens");
            voodoo.Ticks(1);

            Assert.That(voodoo.Loop.Hero.Value(CombatStats.CurseHitsPerSecond, 0, StatTags.DoT), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void CurioSendsTheNextEnemyInVulnerableAndWeakerForTheVoodoo()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "curio_from_the_counter");
            Assert.That(voodoo.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(voodoo.State.WeakenShare, Is.EqualTo(0.05).Within(1e-9));

            voodoo.KillCurrentEnemy();
            voodoo.Ticks(2);

            Assert.That(voodoo.State.EnemyWeakened, Is.True);
            Assert.That(voodoo.State.VulnerabilityShare, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(voodoo.State.VulnerabilityTicks, Is.GreaterThan(9 * GameLoop.TicksPerSecond));
        }

        [Test]
        public void SkullOnTheStaffStrikesFivePerCurseAndRefreshesThem()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "skull_on_the_staff", "charm_prick");
            Assert.That(voodoo.Cast(1), Is.EqualTo(CastResult.Cast));
            voodoo.Ticks(20);
            var curse = voodoo.State.Skills[1];
            Assert.That(curse.ActiveTicks, Is.LessThan(curse.ActiveTotalTicks));
            voodoo.State.Resource = 100;
            voodoo.Loop.Events.Clear();

            Assert.That(voodoo.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(voodoo.DamageFrom(DamageSource.Skill), Is.EqualTo(voodoo.SkillDamage("skull_on_the_staff", 5) * (1 + voodoo.Loop.Combat.Class.EvilEyeShare)).Within(1e-6));
            Assert.That(curse.ActiveTicks, Is.EqualTo(curse.ActiveTotalTicks));
            Assert.That(voodoo.State.Skills[0].Active, Is.True);
        }

        [Test]
        public void SkullWithoutCursesDoesNoDamage()
        {
            var voodoo = SkillTestHero.Of(HeroClass.Voodoo, "skull_on_the_staff");

            Assert.That(voodoo.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(voodoo.DamageFrom(DamageSource.Skill), Is.Zero);
        }

        [Test]
        public void PatchworkDollIsAGolemThatHitsTwiceAsHardOnceASecond()
        {
            var gem = TestWorld.Data.Gems["patchwork_doll"];
            var summon = (SummonEffect)gem.Skill!.Effect;

            Assert.That(gem.Locked, Is.False);
            Assert.That(summon.HitsPerSecond, Is.EqualTo(1));
            Assert.That(summon.Multiplier, Is.EqualTo(2));
            Assert.That(summon.Seconds, Is.EqualTo(30));
        }
    }
}
