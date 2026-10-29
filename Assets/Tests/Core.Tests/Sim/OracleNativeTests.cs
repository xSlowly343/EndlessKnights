using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class OracleNativeTests
    {
        private static double FirstAuraValue(SkillTestHero hero) => hero.State.Skills[0].AuraModifiers[0].Value;

        [Test]
        public void ByTheGaitIsHalfAgainStrongerAfterFiveSecondsWithoutKeys()
        {
            var reading = SkillTestHero.Of(HeroClass.Oracle, "by_the_gait");
            var typing = SkillTestHero.Of(HeroClass.Oracle, "by_the_gait");
            for (var i = 0; i < 6 * GameLoop.TicksPerSecond; i++)
            {
                reading.Input.MoveMouse(50);
                reading.Loop.Tick();
                typing.Input.MoveMouse(50);
                if (i % 2 == 0)
                    typing.Input.PressKeys(1);
                typing.Loop.Tick();
            }
            reading.State.Resource = 100;
            typing.State.Resource = 100;

            Assert.That(reading.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(typing.Cast(0), Is.EqualTo(CastResult.Cast));

            Assert.That(FirstAuraValue(typing), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(FirstAuraValue(reading), Is.EqualTo(0.15).Within(1e-9));
        }

        [Test]
        public void GoodForecastKeepsTheStreakAndTheFocus()
        {
            var calm = SkillTestHero.Of(HeroClass.Oracle, "good_forecast");
            var plain = SkillTestHero.Of(HeroClass.Oracle, "good_forecast");
            Assert.That(calm.Cast(0), Is.EqualTo(CastResult.Cast));
            calm.Ticks(1);
            foreach (var hero in new[] { calm, plain })
            {
                hero.State.Combo = 10;
                hero.State.Focus = 2;
                hero.State.IdleTicks = 1000;
                hero.Ticks(5 * GameLoop.TicksPerSecond);
            }

            Assert.That(calm.State.Combo, Is.EqualTo(10));
            Assert.That(calm.State.Focus, Is.EqualTo(2).Within(1e-9));
            Assert.That(plain.State.Combo, Is.LessThan(10));
            Assert.That(plain.State.Focus, Is.LessThan(2));
        }

        private static double GoldShareOfNextKill(SkillTestHero hero)
        {
            var expected = hero.Loop.Loot.GoldFor(hero.State.Stage, hero.State.EnemyType, hero.State.Enemy).ToDouble();
            hero.Loop.Events.Clear();
            hero.KillCurrentEnemy();
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                if (hero.Loop.Events[i].Kind == GameEventKind.GoldGained)
                    return hero.Loop.Events[i].Amount.ToDouble() / expected;
            }
            return 0;
        }

        [Test]
        public void MistakeInTheDetailsGivesTwentyPercentMoreGoldFromTheWeakenedEnemy()
        {
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "mistake_in_the_details");
            Assert.That(oracle.Cast(0), Is.EqualTo(CastResult.Cast));
            oracle.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(10));

            Assert.That(GoldShareOfNextKill(oracle), Is.EqualTo(1).Within(1e-9));
            oracle.Ticks(2);
            Assert.That(oracle.State.EnemyWeakened, Is.True);
            Assert.That(GoldShareOfNextKill(oracle), Is.EqualTo(1.2).Within(1e-9));
            oracle.Ticks(2);
            Assert.That(GoldShareOfNextKill(oracle), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void UnknownPotionPicksOneOfFourEffects()
        {
            var seen = new bool[4];
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var hero = SkillTestHero.Of(HeroClass.Viking);
                hero.State.Rng = new Rng(seed);
                hero.Loop.Skills.Equip(TestWorld.Data.Gems["unknown_potion"]);
                ArmLeftWithPotion(hero);
                hero.State.Resource = 100;
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
                seen[hero.State.Skills[0].PotionChoice] = true;
            }

            Assert.That(seen, Has.All.True);
        }

        private static void ArmLeftWithPotion(SkillTestHero hero)
        {
            foreach (var weapon in hero.Loop.Data.Weapons.Bases)
            {
                if (weapon.Type.Id != "holy_orb")
                    continue;
                hero.State.Weapons.Left = new Game.Core.Content.Weapons.WeaponItem(weapon, Game.Core.Content.Items.ItemRarity.Simple, new[] { "unknown_potion" });
                return;
            }
        }

        [Test]
        public void TheOracleTakesTheBetterOfTwoPotions()
        {
            var potion = (PotionEffect)TestWorld.Data.Gems["unknown_potion"].Skill!.Effect;
            var worse = 0;
            var tries = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var oracle = SkillTestHero.Of(HeroClass.Oracle, "unknown_potion");
                oracle.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
                oracle.State.Rng = new Rng(seed);
                Assert.That(oracle.Cast(0), Is.EqualTo(CastResult.Cast));
                tries++;
                if (potion.Options[oracle.State.Skills[0].PotionChoice].Score(true) < 3)
                    worse++;
            }

            Assert.That(worse / (double)tries, Is.EqualTo(0.25).Within(0.08));
        }

        [Test]
        public void TimerPotionStopsTheBossTimerForTheWholeDuration()
        {
            var potion = (PotionEffect)TestWorld.Data.Gems["unknown_potion"].Skill!.Effect;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var oracle = SkillTestHero.Of(HeroClass.Oracle, "unknown_potion");
                oracle.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
                oracle.State.BossTicksLeft = 300;
                oracle.State.Rng = new Rng(seed);
                Assert.That(oracle.Cast(0), Is.EqualTo(CastResult.Cast));
                if (!potion.Options[oracle.State.Skills[0].PotionChoice].BossTimerStop)
                    continue;
                oracle.Ticks(50);
                Assert.That(oracle.State.BossTicksLeft, Is.EqualTo(300));
                return;
            }
            Assert.Fail("no seed gave the timer potion");
        }

        [Test]
        public void UnderTheBlindfoldMakesTheNextFiveHitsCritForTheOracle()
        {
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "under_the_blindfold");
            Assert.That(oracle.Cast(0), Is.EqualTo(CastResult.Cast));
            oracle.Loop.Events.Clear();

            for (var i = 0; i < 14; i++)
                oracle.Ticks(1, keysPerTick: i % 2 == 0 ? 1 : 0);

            var crits = 0;
            var hits = 0;
            for (var i = 0; i < oracle.Loop.Events.Count; i++)
            {
                var e = oracle.Loop.Events[i];
                if (e.Kind != GameEventKind.Damage || e.Source != DamageSource.Keyboard)
                    continue;
                hits++;
                if ((e.Flags & HitFlags.Crit) != 0)
                    crits++;
            }
            Assert.That(hits, Is.EqualTo(7));
            Assert.That(crits, Is.EqualTo(5));
        }

        [Test]
        public void LightThroughTheLensGrowsWithEveryAlly()
        {
            var alone = SkillTestHero.Of(HeroClass.Oracle, "light_through_the_lens");
            var withTwo = SkillTestHero.Of(HeroClass.Oracle, "light_through_the_lens");
            withTwo.State.PartyAllies = 2;
            Assert.That(alone.Cast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(withTwo.Cast(0), Is.EqualTo(CastResult.Cast));

            alone.Ticks(10);
            withTwo.Ticks(10);

            Assert.That(withTwo.DamageFrom(DamageSource.Skill), Is.EqualTo(3 * alone.DamageFrom(DamageSource.Skill)).Within(1e-6 * alone.DamageFrom(DamageSource.Skill)));
        }

        [Test]
        public void OmensAndResolveGrowFromAllyHits()
        {
            foreach (var heroClass in new[] { HeroClass.Oracle, HeroClass.Bannerbearer })
            {
                var hero = SkillTestHero.Of(heroClass);
                hero.State.Resource = 0;

                hero.Loop.Resource.NoteAllyHits(25);
                hero.Ticks(1);

                Assert.That(hero.State.Resource, Is.EqualTo(2.5).Within(1e-9), heroClass + ": +1 for every 10 ally hits");
            }
            var viking = SkillTestHero.Of(HeroClass.Viking);
            viking.State.Resource = 0;
            viking.Loop.Resource.NoteAllyHits(25);
            viking.Ticks(1);
            Assert.That(viking.State.Resource, Is.LessThan(1e-9), "other classes do not count ally hits");
        }

        [Test]
        public void TheSeersOrbAddsAnOmenForAllyHitsOnceInTwoSeconds()
        {
            var hero = SkillTestHero.Of(HeroClass.Oracle);
            hero.Loop.Hero.Add(new Modifier(CombatStats.OmenPerAllyHit, ModifierKind.Flat, 1));
            hero.State.Resource = 0;

            hero.Loop.Resource.NoteAllyHits(1);
            hero.Loop.Resource.NoteAllyHits(1);
            hero.Ticks(1);
            Assert.That(hero.State.Resource, Is.EqualTo(1.2).Within(1e-9), "one omen from the orb and 0.2 from two hits");

            hero.Ticks(2 * GameLoop.TicksPerSecond);
            hero.Loop.Resource.NoteAllyHits(1);
            hero.Ticks(1);
            Assert.That(hero.State.Resource, Is.EqualTo(2.3).Within(1e-9));
        }

        [Test]
        public void AllyResourceFillsTheBarAndCoolsThePressure()
        {
            var oracle = SkillTestHero.Of(HeroClass.Oracle);
            var moonshiner = SkillTestHero.Of(HeroClass.Moonshiner);
            oracle.State.Resource = 10;
            moonshiner.State.Resource = 50;

            oracle.Loop.Resource.GiftFromAlly(20);
            moonshiner.Loop.Resource.GiftFromAlly(20);
            oracle.Ticks(1);

            Assert.That(oracle.State.Resource, Is.EqualTo(30).Within(1e-9));
            Assert.That(moonshiner.State.Resource, Is.EqualTo(30).Within(1e-9), "for the inverse resource a gift lowers the pressure");
            Assert.That(((AuraEffect)TestWorld.Data.Gems["good_forecast"].Skill!.Effect).AllyResource, Is.EqualTo(20));
        }

        [Test]
        public void LightThroughTheLensHitsTheNextEnemyOnlyForTheOracle()
        {
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "light_through_the_lens");
            Assert.That(oracle.Cast(0), Is.EqualTo(CastResult.Cast));

            oracle.Ticks(10);

            Assert.That(oracle.Queued(0), Is.GreaterThan(0));
            Assert.That(oracle.Queued(1), Is.Zero);
            Assert.That(((PulseEffect)TestWorld.Data.Gems["light_through_the_lens"].Skill!.Effect).QueueTargets, Is.Zero);
        }
    }
}
