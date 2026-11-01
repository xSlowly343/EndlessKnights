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
    public class WhispererNativeTests
    {
        private static SkillTestHero Stranger(params string[] gems)
        {
            var runedancer = SkillTestHero.Of(HeroClass.Runedancer);
            var type = TestWorld.Data.Gems[gems[0]].Skill!.Weapon;
            var weaponBase = runedancer.Loop.Data.Weapons.Bases.First(weapon => weapon.Type.Id == type);
            runedancer.State.Weapons.Left = new WeaponItem(weaponBase, ItemRarity.Simple, gems);
            foreach (var gem in gems)
                runedancer.Loop.Skills.Equip(TestWorld.Data.Gems[gem]);
            runedancer.State.Resource = 100;
            runedancer.Loop.Events.Clear();
            return runedancer;
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
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "long_wish");
            Assert.That(whisperer.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = whisperer.State.Skills[0];
            var ticks = slot.ActiveTicks;
            whisperer.Loop.Events.Clear();
            for (var i = 0; i < ticks - 1; i++)
                whisperer.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);
            var dealt = Damage(whisperer);
            var stored = whisperer.State.WishStored.ToDouble();
            whisperer.Loop.Events.Clear();

            whisperer.Loop.Tick();

            Assert.That(stored, Is.EqualTo(dealt * 0.3).Within(dealt * 1e-9));
            Assert.That(slot.Active, Is.False);
            Assert.That(whisperer.DamageFrom(DamageSource.Skill), Is.EqualTo(stored).Within(stored * 1e-9));
            Assert.That(whisperer.State.WishShare, Is.Zero);
        }

        [Test]
        public void LongWishPassesTheStoreToTheNextEnemyOnlyForTheVoodoo()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "long_wish");
            var runedancer = Stranger("long_wish");
            foreach (var hero in new[] { whisperer, runedancer })
            {
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                for (var i = 0; i < 10; i++)
                    hero.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);
            }
            var whispererStored = whisperer.State.WishStored.ToDouble();
            Assert.That(whispererStored, Is.GreaterThan(0));

            foreach (var hero in new[] { whisperer, runedancer })
            {
                hero.KillCurrentEnemy();
                hero.Loop.Events.Clear();
                hero.Ticks(1);
            }

            Assert.That(whisperer.DamageFrom(DamageSource.Skill), Is.GreaterThanOrEqualTo(whispererStored));
            Assert.That(runedancer.DamageFrom(DamageSource.Skill), Is.Zero);
            Assert.That(runedancer.State.WishStored.ToDouble(), Is.Zero);
        }

        [Test]
        public void PipeSmokeAddsThreeSecondsToTheBossTimerAndFiveForTheVoodoo()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "pipe_smoke");
            var runedancer = Stranger("pipe_smoke");
            foreach (var hero in new[] { whisperer, runedancer })
            {
                hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
                hero.State.BossTicksLeft = 300;
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            }

            Assert.That(whisperer.State.BossTicksLeft, Is.EqualTo(350));
            Assert.That(runedancer.State.BossTicksLeft, Is.EqualTo(330));
        }

        [Test]
        public void SwampDwellerAddsTwoPercentPerHitUpToTwenty()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "swamp_dweller");
            Assert.That(whisperer.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = whisperer.State.Skills[0];

            whisperer.Ticks(3 * GameLoop.TicksPerSecond);
            Assert.That(slot.StackModifier.Value, Is.EqualTo(0.06).Within(1e-9));

            whisperer.Ticks(15 * GameLoop.TicksPerSecond);
            Assert.That(slot.StackModifier.Value, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void SwampDwellerLivesWhileTheTaintStaysAboveFifty()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "swamp_dweller");
            Assert.That(whisperer.Cast(0), Is.EqualTo(CastResult.Cast));
            var slot = whisperer.State.Skills[0];
            var ticks = slot.ActiveTicks;

            for (var i = 0; i < ticks + 20; i++)
            {
                whisperer.State.Resource = 80;
                whisperer.Loop.Tick();
            }
            Assert.That(slot.Active, Is.True);

            whisperer.State.Resource = 10;
            whisperer.Ticks(2);
            Assert.That(slot.Active, Is.False);
            Assert.That(slot.Stacks, Is.Zero);
        }

        [Test]
        public void JamRipensMakesEveryCurseHitOncePerSecondForTheVoodoo()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "the_jam_ripens");
            whisperer.Ticks(1);

            Assert.That(whisperer.Loop.Hero.Value(CombatStats.CurseHitsPerSecond, 0, StatTags.DoT), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void CurioSendsTheNextEnemyInVulnerableAndWeakerForTheVoodoo()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "curio_from_the_counter");
            Assert.That(whisperer.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(whisperer.State.WeakenShare, Is.EqualTo(0.05).Within(1e-9));

            whisperer.KillCurrentEnemy();
            whisperer.Ticks(2);

            Assert.That(whisperer.State.EnemyWeakened, Is.True);
            Assert.That(whisperer.State.VulnerabilityShare, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(whisperer.State.VulnerabilityTicks, Is.GreaterThan(9 * GameLoop.TicksPerSecond));
        }

        [Test]
        public void SkullOnTheStaffStrikesFivePerCurseAndRefreshesThem()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "skull_on_the_staff", "charm_prick");
            Assert.That(whisperer.Cast(1), Is.EqualTo(CastResult.Cast));
            whisperer.Ticks(20);
            var curse = whisperer.State.Skills[1];
            Assert.That(curse.ActiveTicks, Is.LessThan(curse.ActiveTotalTicks));
            whisperer.State.Resource = 100;
            whisperer.Loop.Events.Clear();

            Assert.That(whisperer.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(whisperer.DamageFrom(DamageSource.Skill), Is.EqualTo(whisperer.SkillDamage("skull_on_the_staff", 5) * (1 + whisperer.Loop.Combat.Class.EvilEyeShare)).Within(1e-6));
            Assert.That(curse.ActiveTicks, Is.EqualTo(curse.ActiveTotalTicks));
            Assert.That(whisperer.State.Skills[0].Active, Is.True);
        }

        [Test]
        public void SkullWithoutCursesDoesNoDamage()
        {
            var whisperer = SkillTestHero.Of(HeroClass.Whisperer, "skull_on_the_staff");

            Assert.That(whisperer.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(whisperer.DamageFrom(DamageSource.Skill), Is.Zero);
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
