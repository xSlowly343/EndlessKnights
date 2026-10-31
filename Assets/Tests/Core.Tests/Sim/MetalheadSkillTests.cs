using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MetalheadSkillTests
    {
        [Test]
        public void LouderDoublesDriveFromTheMetalheadsHits()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "louder");
            metalhead.State.Resource = 80;
            metalhead.Cast(0);

            metalhead.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("metalhead").PerKey;
            Assert.That(metalhead.State.Resource, Is.EqualTo(5 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void NailChordHitsTheEnemyAndTheNextOne()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "nail_chord");

            metalhead.Cast(0);

            var wave = metalhead.SkillDamage("nail_chord", 3);
            Assert.That(metalhead.DamageFrom(DamageSource.Skill), Is.EqualTo(wave).Within(1e-9));
            Assert.That(metalhead.Queued(0), Is.EqualTo(wave).Within(1e-9));
            Assert.That(metalhead.Queued(1), Is.EqualTo(0));
        }

        [Test]
        public void NailChordReachesTheThirdEnemyWhileTheMetalheadSings()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "nail_chord", "hearth_song");
            metalhead.Cast(1);

            metalhead.Cast(0);

            Assert.That(metalhead.Queued(1), Is.EqualTo(metalhead.SkillDamage("nail_chord", 3)).Within(1e-9));
        }

        [Test]
        public void NailChordOnABorrowedGuitarIgnoresTheSong()
        {
            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Metalhead, "nail_chord", "hearth_song");
            ronin.Cast(1);

            ronin.Cast(0);

            Assert.That(ronin.Queued(0), Is.GreaterThan(0));
            Assert.That(ronin.Queued(1), Is.EqualTo(0));
        }

        [Test]
        public void ColossusLullabySlowsTheBossTimerByAQuarter()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "colossus_lullaby");
            metalhead.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            metalhead.State.BossTicksLeft = 100;

            metalhead.Input.ClickLeft();
            metalhead.Ticks(40);

            Assert.That(metalhead.State.BossTicksLeft, Is.EqualTo(70));
        }

        [Test]
        public void BossTimerRunsAtFullSpeedAfterTheBallad()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "colossus_lullaby");
            metalhead.Cast(0);
            metalhead.Ticks(150);
            metalhead.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            metalhead.State.BossTicksLeft = 100;

            metalhead.Ticks(40);

            Assert.That(metalhead.State.BossTicksLeft, Is.EqualTo(60));
        }

        [Test]
        public void WholeHallAddsTwoPercentPerKeyHitUpToAQuarter()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "whole_hall");
            metalhead.Cast(0);

            metalhead.Ticks(1, keysPerTick: 5);
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(110).Within(1e-9));

            metalhead.Ticks(5, keysPerTick: 5);
            var heardByAll = 100 * TestWorld.Data.Party.Buffs.TypingPartyDamage;
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(125 + heardByAll).Within(1e-9));
        }

        [Test]
        public void WholeHallStacksVanishWhenItEnds()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "whole_hall");
            metalhead.Cast(0);
            metalhead.Ticks(1, keysPerTick: 5);

            metalhead.Ticks(100);

            Assert.That(metalhead.State.Skills[0].Stacks, Is.EqualTo(0));
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void AmpToElevenReservesAQuarterOfTheBarAndSpeedsUpFocus()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "underside_amp");

            Assert.That(metalhead.Cast(0), Is.EqualTo(CastResult.Passive));
            metalhead.Ticks(1);
            Assert.That(metalhead.Loop.Resource.Max, Is.EqualTo(75));
            Assert.That(metalhead.State.Resource, Is.EqualTo(75));
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.FocusGain, 1), Is.EqualTo(1.2).Within(1e-9));

            metalhead.Ticks(300);
            Assert.That(metalhead.State.Skills[0].Active, Is.True);
            Assert.That(metalhead.Loop.Resource.Max, Is.EqualTo(75));
        }

        [Test]
        public void AmpToElevenFreesTheReserveWhenRemoved()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "underside_amp");

            metalhead.Loop.Skills.Unequip(0);

            Assert.That(metalhead.Loop.Resource.Max, Is.EqualTo(100));
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.FocusGain, 1), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AmpToElevenKeepsTheMetalheadsFocusThreeSecondsLonger()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead);
            var plain = metalhead.Loop.Focus.ResetAfterIdleSeconds;
            metalhead.Loop.Skills.Equip(TestWorld.Data.Gems["underside_amp"]);

            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Metalhead, "underside_amp");

            Assert.That(metalhead.Loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(plain + 3).Within(1e-9));
            Assert.That(ronin.Loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void StillAliveHitsForTenStrikesAndRefundsHalfOnAKill()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "still_alive");
            metalhead.State.EnemyHp = BigNumber.FromDouble(1e-6);

            metalhead.Cast(0);

            Assert.That(metalhead.State.Resource, Is.EqualTo(70));
        }

        [Test]
        public void StillAliveWithoutAKillRefundsNothing()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "still_alive");

            metalhead.Cast(0);

            Assert.That(metalhead.DamageFrom(DamageSource.Skill), Is.EqualTo(metalhead.SkillDamage("still_alive", 10)).Within(1e-9));
            Assert.That(metalhead.State.Resource, Is.EqualTo(40));
        }

        [Test]
        public void StillAliveKillRenewsTheMetalheadsSongs()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "still_alive", "hearth_song");
            metalhead.Cast(1);
            metalhead.Ticks(100);
            var full = metalhead.State.Skills[1].ActiveTotalTicks;
            Assert.That(metalhead.State.Skills[1].ActiveTicks, Is.LessThan(full));
            metalhead.State.Resource = 100;
            metalhead.State.EnemyHp = BigNumber.FromDouble(1e-6);

            metalhead.Cast(0);

            Assert.That(metalhead.State.Skills[1].ActiveTicks, Is.EqualTo(full));
        }
    }
}
