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
    public class ClockmakerNativeTests
    {
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

        private static void Boss(SkillTestHero hero, int ticksLeft)
        {
            hero.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            hero.State.BossTicksLeft = ticksLeft;
        }

        [Test]
        public void StretchTheSecondGivesOneSandForEverySavedSecond()
        {
            var clockmaker = SkillTestHero.Of(HeroClass.Clockmaker, "stretch_second");
            var plain = SkillTestHero.Of(HeroClass.Clockmaker, "stretch_second");
            Boss(clockmaker, 1000);
            Boss(plain, 1000);
            Assert.That(clockmaker.Cast(0), Is.EqualTo(CastResult.Cast));
            clockmaker.State.Resource = 0;
            plain.State.Resource = 0;

            clockmaker.Ticks(5 * GameLoop.TicksPerSecond);
            plain.Ticks(5 * GameLoop.TicksPerSecond);

            var saved = (5 * GameLoop.TicksPerSecond - (1000 - clockmaker.State.BossTicksLeft)) * GameLoop.TickSeconds;
            Assert.That(saved, Is.GreaterThan(0.5));
            Assert.That(clockmaker.State.Resource - plain.State.Resource, Is.EqualTo(saved).Within(0.15));
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
            var viking = Stranger("the_chimes_strike_again");
            var (lastFive, _) = Fight(viking);

            Assert.That(Chime(viking), Is.EqualTo(lastFive).Within(lastFive * 1e-9));
        }

        [Test]
        public void TheClockmakersChimesRepeatSevenSeconds()
        {
            var clockmaker = SkillTestHero.Of(HeroClass.Clockmaker, "the_chimes_strike_again");
            var (_, lastSeven) = Fight(clockmaker);

            Assert.That(Chime(clockmaker), Is.EqualTo(lastSeven).Within(lastSeven * 1e-9));
        }

        [Test]
        public void HandsFrozeStopTheTimerAndAddHalfTheDamage()
        {
            var clockmaker = SkillTestHero.Of(HeroClass.Clockmaker, "the_hands_froze");
            Assert.That(clockmaker.Cast(0), Is.EqualTo(CastResult.Cast));

            var modifiers = clockmaker.State.Skills[0].AuraModifiers;
            Assert.That(modifiers.Any(m => m.Stat == CombatStats.BossTimerSpeed && m.Value == -1), Is.True);
            Assert.That(modifiers.Any(m => m.Stat == CombatStats.Damage && m.Kind == ModifierKind.More && m.Value == 0.5), Is.True);
        }

        [Test]
        public void SandInTheGlassBanksASecondPerFastKillUpToTwentyFive()
        {
            var clockmaker = SkillTestHero.Of(HeroClass.Clockmaker, "sand_in_the_glass");
            clockmaker.Ticks(1);

            clockmaker.KillCurrentEnemy();
            Assert.That(clockmaker.State.BankedSeconds, Is.EqualTo(1).Within(1e-9));

            for (var i = 0; i < 40; i++)
                clockmaker.Loop.Stages.KillTimeListener!.OnKillTimed(EnemyType.Normal, 1, false);
            Assert.That(clockmaker.State.BankedSeconds, Is.EqualTo(25).Within(1e-9));
        }

        [Test]
        public void SandInTheGlassStopsAtFifteenForAnotherHero()
        {
            var viking = Stranger("sand_in_the_glass");
            viking.Ticks(1);
            for (var i = 0; i < 40; i++)
                viking.Loop.Stages.KillTimeListener!.OnKillTimed(EnemyType.Normal, 1, false);

            Assert.That(viking.State.BankedSeconds, Is.EqualTo(15).Within(1e-9));
        }

        [Test]
        public void TimeWithCharacterTurnsTheTimerBackTenSecondsOncePerFightAndRestoresFocus()
        {
            var clockmaker = SkillTestHero.Of(HeroClass.Clockmaker, "time_with_character");
            Boss(clockmaker, 300);
            clockmaker.State.Focus = 1;

            Assert.That(clockmaker.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(clockmaker.State.BossTicksLeft, Is.EqualTo(400));
            clockmaker.Loop.Tick();
            Assert.That(clockmaker.State.Focus, Is.GreaterThan(1));

            clockmaker.State.Skills[0].CooldownTicks = 0;
            clockmaker.State.Resource = 100;
            var left = clockmaker.State.BossTicksLeft;
            Assert.That(clockmaker.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(clockmaker.State.BossTicksLeft, Is.EqualTo(left));
        }

        [Test]
        public void CrackedGlassReturnsThirtyPercentOfTheDamage()
        {
            var clockmaker = SkillTestHero.Of(HeroClass.Clockmaker, "cracked_glass");

            Assert.That(clockmaker.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(clockmaker.State.WishShare, Is.EqualTo(0.3).Within(1e-9));
        }
    }
}
