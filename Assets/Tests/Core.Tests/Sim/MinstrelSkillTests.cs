using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MinstrelSkillTests
    {
        [Test]
        public void LouderDoublesDriveFromTheMetalheadsHits()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "louder");
            minstrel.State.Resource = 80;
            minstrel.Cast(0);

            minstrel.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("minstrel").PerKey;
            Assert.That(minstrel.State.Resource, Is.EqualTo(5 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void NailChordHitsTheEnemyAndTheNextOne()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "nail_chord");

            minstrel.Cast(0);

            var wave = minstrel.SkillDamage("nail_chord", 3);
            Assert.That(minstrel.DamageFrom(DamageSource.Skill), Is.EqualTo(wave).Within(1e-9));
            Assert.That(minstrel.Queued(0), Is.EqualTo(wave).Within(1e-9));
            Assert.That(minstrel.Queued(1), Is.EqualTo(0));
        }

        [Test]
        public void NailChordReachesTheThirdEnemyWhileTheMetalheadSings()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "nail_chord", "hearth_song");
            minstrel.Cast(1);

            minstrel.Cast(0);

            Assert.That(minstrel.Queued(1), Is.EqualTo(minstrel.SkillDamage("nail_chord", 3)).Within(1e-9));
        }

        [Test]
        public void NailChordOnABorrowedGuitarIgnoresTheSong()
        {
            var duelist = SkillTestHero.Borrowing(HeroClass.Duelist, HeroClass.Minstrel, "nail_chord", "hearth_song");
            duelist.Cast(1);

            duelist.Cast(0);

            Assert.That(duelist.Queued(0), Is.GreaterThan(0));
            Assert.That(duelist.Queued(1), Is.EqualTo(0));
        }

        [Test]
        public void ColossusLullabySlowsTheBossTimerByAQuarter()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "colossus_lullaby");
            minstrel.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            minstrel.State.BossTicksLeft = 100;

            minstrel.Input.ClickLeft();
            minstrel.Ticks(40);

            Assert.That(minstrel.State.BossTicksLeft, Is.EqualTo(70));
        }

        [Test]
        public void BossTimerRunsAtFullSpeedAfterTheBallad()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "colossus_lullaby");
            minstrel.Cast(0);
            minstrel.Ticks(150);
            minstrel.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            minstrel.State.BossTicksLeft = 100;

            minstrel.Ticks(40);

            Assert.That(minstrel.State.BossTicksLeft, Is.EqualTo(60));
        }

        [Test]
        public void WholeHallAddsTwoPercentPerKeyHitUpToAQuarter()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "whole_hall");
            minstrel.Cast(0);

            minstrel.Ticks(1, keysPerTick: 5);
            Assert.That(minstrel.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(110).Within(1e-9));

            minstrel.Ticks(5, keysPerTick: 5);
            var heardByAll = 100 * TestWorld.Data.Party.Buffs.TypingPartyDamage;
            Assert.That(minstrel.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(125 + heardByAll).Within(1e-9));
        }

        [Test]
        public void WholeHallStacksVanishWhenItEnds()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "whole_hall");
            minstrel.Cast(0);
            minstrel.Ticks(1, keysPerTick: 5);

            minstrel.Ticks(100);

            Assert.That(minstrel.State.Skills[0].Stacks, Is.EqualTo(0));
            Assert.That(minstrel.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void AmpToElevenReservesAQuarterOfTheBarAndSpeedsUpFocus()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "underside_amp");

            Assert.That(minstrel.Cast(0), Is.EqualTo(CastResult.Passive));
            minstrel.Ticks(1);
            Assert.That(minstrel.Loop.Resource.Max, Is.EqualTo(75));
            Assert.That(minstrel.State.Resource, Is.EqualTo(75));
            Assert.That(minstrel.Loop.Hero.Value(CombatStats.FocusGain, 1), Is.EqualTo(1.2).Within(1e-9));

            minstrel.Ticks(300);
            Assert.That(minstrel.State.Skills[0].Active, Is.True);
            Assert.That(minstrel.Loop.Resource.Max, Is.EqualTo(75));
        }

        [Test]
        public void AmpToElevenFreesTheReserveWhenRemoved()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "underside_amp");

            minstrel.Loop.Skills.Unequip(0);

            Assert.That(minstrel.Loop.Resource.Max, Is.EqualTo(100));
            Assert.That(minstrel.Loop.Hero.Value(CombatStats.FocusGain, 1), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AmpToElevenKeepsTheMetalheadsFocusThreeSecondsLonger()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel);
            var plain = minstrel.Loop.Focus.ResetAfterIdleSeconds;
            minstrel.Loop.Skills.Equip(TestWorld.Data.Gems["underside_amp"]);

            var duelist = SkillTestHero.Borrowing(HeroClass.Duelist, HeroClass.Minstrel, "underside_amp");

            Assert.That(minstrel.Loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(plain + 3).Within(1e-9));
            Assert.That(duelist.Loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void StillAliveHitsForTenStrikesAndRefundsHalfOnAKill()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "still_alive");
            minstrel.State.EnemyHp = BigNumber.FromDouble(1e-6);

            minstrel.Cast(0);

            Assert.That(minstrel.State.Resource, Is.EqualTo(70));
        }

        [Test]
        public void StillAliveWithoutAKillRefundsNothing()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "still_alive");

            minstrel.Cast(0);

            Assert.That(minstrel.DamageFrom(DamageSource.Skill), Is.EqualTo(minstrel.SkillDamage("still_alive", 10)).Within(1e-9));
            Assert.That(minstrel.State.Resource, Is.EqualTo(40));
        }

        [Test]
        public void StillAliveKillRenewsTheMetalheadsSongs()
        {
            var minstrel = SkillTestHero.Of(HeroClass.Minstrel, "still_alive", "hearth_song");
            minstrel.Cast(1);
            minstrel.Ticks(100);
            var full = minstrel.State.Skills[1].ActiveTotalTicks;
            Assert.That(minstrel.State.Skills[1].ActiveTicks, Is.LessThan(full));
            minstrel.State.Resource = 100;
            minstrel.State.EnemyHp = BigNumber.FromDouble(1e-6);

            minstrel.Cast(0);

            Assert.That(minstrel.State.Skills[1].ActiveTicks, Is.EqualTo(full));
        }
    }
}
