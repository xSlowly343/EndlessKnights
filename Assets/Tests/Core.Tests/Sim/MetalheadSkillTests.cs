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
        public void GuitarSoloDoublesDriveFromTheMetalheadsHits()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "guitar_solo");
            metalhead.State.Resource = 80;
            metalhead.Cast(0);

            metalhead.Ticks(1, keysPerTick: 5);

            var perKey = TestWorld.Data.Combat.Resources.For("metalhead").PerKey;
            Assert.That(metalhead.State.Resource, Is.EqualTo(5 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void PowerChordHitsTheEnemyAndTheNextOne()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "power_chord");

            metalhead.Cast(0);

            var wave = metalhead.SkillDamage("power_chord", 3);
            Assert.That(metalhead.DamageFrom(DamageSource.Skill), Is.EqualTo(wave).Within(1e-9));
            Assert.That(metalhead.Queued(0), Is.EqualTo(wave).Within(1e-9));
            Assert.That(metalhead.Queued(1), Is.EqualTo(0));
        }

        [Test]
        public void PowerChordReachesTheThirdEnemyWhileTheMetalheadSings()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "power_chord", "song");
            metalhead.Cast(1);

            metalhead.Cast(0);

            Assert.That(metalhead.Queued(1), Is.EqualTo(metalhead.SkillDamage("power_chord", 3)).Within(1e-9));
        }

        [Test]
        public void PowerChordOnABorrowedGuitarIgnoresTheSong()
        {
            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Metalhead, "power_chord", "song");
            ronin.Cast(1);

            ronin.Cast(0);

            Assert.That(ronin.Queued(0), Is.GreaterThan(0));
            Assert.That(ronin.Queued(1), Is.EqualTo(0));
        }

        [Test]
        public void BalladSlowsTheBossTimerByAQuarter()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "ballad");
            metalhead.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            metalhead.State.BossTicksLeft = 100;

            metalhead.Input.ClickLeft();
            metalhead.Ticks(40);

            Assert.That(metalhead.State.BossTicksLeft, Is.EqualTo(70));
        }

        [Test]
        public void BossTimerRunsAtFullSpeedAfterTheBallad()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "ballad");
            metalhead.Cast(0);
            metalhead.Ticks(150);
            metalhead.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            metalhead.State.BossTicksLeft = 100;

            metalhead.Ticks(40);

            Assert.That(metalhead.State.BossTicksLeft, Is.EqualTo(60));
        }

        [Test]
        public void MoshPitAddsTwoPercentPerKeyHitUpToAQuarter()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "mosh_pit");
            metalhead.Cast(0);

            metalhead.Ticks(1, keysPerTick: 5);
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(110).Within(1e-9));

            metalhead.Ticks(5, keysPerTick: 5);
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(125).Within(1e-9));
        }

        [Test]
        public void MoshPitStacksVanishWhenItEnds()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "mosh_pit");
            metalhead.Cast(0);
            metalhead.Ticks(1, keysPerTick: 5);

            metalhead.Ticks(100);

            Assert.That(metalhead.State.Skills[0].Stacks, Is.EqualTo(0));
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void AmpToElevenReservesAQuarterOfTheBarAndSpeedsUpFocus()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "amp_eleven");

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
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "amp_eleven");

            metalhead.Loop.Skills.Unequip(0);

            Assert.That(metalhead.Loop.Resource.Max, Is.EqualTo(100));
            Assert.That(metalhead.Loop.Hero.Value(CombatStats.FocusGain, 1), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AmpToElevenKeepsTheMetalheadsFocusThreeSecondsLonger()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead);
            var plain = metalhead.Loop.Focus.ResetAfterIdleSeconds;
            metalhead.Loop.Skills.Equip(TestWorld.Data.Gems["amp_eleven"]);

            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Metalhead, "amp_eleven");

            Assert.That(metalhead.Loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(plain + 3).Within(1e-9));
            Assert.That(ronin.Loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void StageDiveHitsForTenStrikesAndRefundsHalfOnAKill()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "stage_dive");
            metalhead.State.EnemyHp = BigNumber.FromDouble(1e-6);

            metalhead.Cast(0);

            Assert.That(metalhead.State.Resource, Is.EqualTo(70));
        }

        [Test]
        public void StageDiveWithoutAKillRefundsNothing()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "stage_dive");

            metalhead.Cast(0);

            Assert.That(metalhead.DamageFrom(DamageSource.Skill), Is.EqualTo(metalhead.SkillDamage("stage_dive", 10)).Within(1e-9));
            Assert.That(metalhead.State.Resource, Is.EqualTo(40));
        }

        [Test]
        public void StageDiveKillRenewsTheMetalheadsSongs()
        {
            var metalhead = SkillTestHero.Of(HeroClass.Metalhead, "stage_dive", "song");
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
