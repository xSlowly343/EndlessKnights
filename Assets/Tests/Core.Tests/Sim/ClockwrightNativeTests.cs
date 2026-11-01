using System.Collections.Generic;
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
    public class ClockwrightNativeTests
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

        private static void Boss(SkillTestHero hero, int ticksLeft)
        {
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = ticksLeft;
        }

        [Test]
        public void StretchTheSecondGivesOneSandForEverySavedSecond()
        {
            var clockwright = SkillTestHero.Of(HeroClass.Clockwright, "stretch_second");
            var plain = SkillTestHero.Of(HeroClass.Clockwright, "stretch_second");
            Boss(clockwright, 1000);
            Boss(plain, 1000);
            Assert.That(clockwright.Cast(0), Is.EqualTo(CastResult.Cast));
            clockwright.State.Resource = 0;
            plain.State.Resource = 0;

            clockwright.Ticks(5 * GameLoop.TicksPerSecond);
            plain.Ticks(5 * GameLoop.TicksPerSecond);

            var saved = (5 * GameLoop.TicksPerSecond - (1000 - clockwright.State.BossTicksLeft)) * GameLoop.TickSeconds;
            Assert.That(saved, Is.GreaterThan(0.5));
            Assert.That(clockwright.State.Resource - plain.State.Resource, Is.EqualTo(saved).Within(0.15));
        }

        private static (double LastFive, double LastSeven) Fight(SkillTestHero hero)
        {
            var perTick = new List<double>();
            for (var i = 0; i < 80; i++)
            {
                hero.Loop.Events.Clear();
                hero.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);
                perTick.Add(Enumerable.Range(0, hero.Loop.Events.Count)
                    .Select(n => hero.Loop.Events[n])
                    .Where(e => e.Kind == GameEventKind.Damage)
                    .Sum(e => e.Amount.ToDouble()));
            }
            return (perTick.Skip(30).Sum(), perTick.Skip(10).Sum());
        }

        private static double Chime(SkillTestHero hero)
        {
            hero.State.Resource = 100;
            hero.Loop.Events.Clear();
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            return hero.DamageFrom(DamageSource.Skill);
        }

        [Test]
        public void ChimesRepeatTheLastFiveSecondsOfDamage()
        {
            var runedancer = Stranger("the_chimes_strike_again");
            var (lastFive, _) = Fight(runedancer);

            Assert.That(Chime(runedancer), Is.EqualTo(lastFive).Within(lastFive * 1e-9));
        }

        [Test]
        public void TheClockmakersChimesRepeatSevenSeconds()
        {
            var clockwright = SkillTestHero.Of(HeroClass.Clockwright, "the_chimes_strike_again");
            var (_, lastSeven) = Fight(clockwright);

            Assert.That(Chime(clockwright), Is.EqualTo(lastSeven).Within(lastSeven * 1e-9));
        }

        [Test]
        public void HandsFrozeStopTheTimerAndAddHalfTheDamage()
        {
            var clockwright = SkillTestHero.Of(HeroClass.Clockwright, "the_hands_froze");
            Assert.That(clockwright.Cast(0), Is.EqualTo(CastResult.Cast));

            var modifiers = clockwright.State.Skills[0].AuraModifiers;
            Assert.That(modifiers.Any(m => m.Stat == CombatStats.BossTimerSpeed && m.Value == -1), Is.True);
            Assert.That(modifiers.Any(m => m.Stat == CombatStats.Damage && m.Kind == ModifierKind.More && m.Value == 0.5), Is.True);
        }

        [Test]
        public void SandInTheGlassBanksASecondPerFastKillUpToTwentyFive()
        {
            var clockwright = SkillTestHero.Of(HeroClass.Clockwright, "sand_in_the_glass");
            clockwright.Ticks(1);

            clockwright.KillCurrentEnemy();
            Assert.That(clockwright.State.BankedSeconds, Is.EqualTo(1).Within(1e-9));

            for (var i = 0; i < 40; i++)
                clockwright.Loop.Stages.KillTimeListener!.OnKillTimed(EnemyType.Normal, 1, false);
            Assert.That(clockwright.State.BankedSeconds, Is.EqualTo(25).Within(1e-9));
        }

        [Test]
        public void SandInTheGlassStopsAtFifteenForAnotherHero()
        {
            var runedancer = Stranger("sand_in_the_glass");
            runedancer.Ticks(1);
            for (var i = 0; i < 40; i++)
                runedancer.Loop.Stages.KillTimeListener!.OnKillTimed(EnemyType.Normal, 1, false);

            Assert.That(runedancer.State.BankedSeconds, Is.EqualTo(15).Within(1e-9));
        }

        [Test]
        public void TimeWithCharacterTurnsTheTimerBackTenSecondsOncePerFightAndRestoresFocus()
        {
            var clockwright = SkillTestHero.Of(HeroClass.Clockwright, "time_with_character");
            Boss(clockwright, 300);
            clockwright.State.Focus = 1;

            Assert.That(clockwright.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(clockwright.State.BossTicksLeft, Is.EqualTo(400));
            clockwright.Loop.Tick();
            Assert.That(clockwright.State.Focus, Is.GreaterThan(1));

            clockwright.State.Skills[0].CooldownTicks = 0;
            clockwright.State.Resource = 100;
            var left = clockwright.State.BossTicksLeft;
            Assert.That(clockwright.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(clockwright.State.BossTicksLeft, Is.EqualTo(left));
        }

        [Test]
        public void CrackedGlassReturnsThirtyPercentOfTheDamage()
        {
            var clockwright = SkillTestHero.Of(HeroClass.Clockwright, "cracked_glass");

            Assert.That(clockwright.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(clockwright.State.WishShare, Is.EqualTo(0.3).Within(1e-9));
        }
    }
}
