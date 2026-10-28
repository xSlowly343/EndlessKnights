using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SpiritTests
    {
        private static SpiritCatalog Catalog => TestWorld.Data.Spirits;

        private static SkillTestHero With(string spirit, int level = 1, params string[] skills)
        {
            var hero = SkillTestHero.Of(HeroClass.Oracle, skills);
            hero.State.Spirits[spirit] = level;
            hero.Loop.Events.Clear();
            return hero;
        }

        [Test]
        public void EightSpiritsEachWithItsOwnRule()
        {
            Assert.That(Catalog.All.Count, Is.EqualTo(8));
            Assert.That(Catalog.All.Select(spirit => spirit.Trigger).Distinct().Count(), Is.EqualTo(8));
        }

        [Test]
        public void AwakeningAndRaisingCostEcho()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(100) });

            Assert.That(loop.RaiseSpirit("wind_spirit"), Is.EqualTo(SpiritResult.Awakened));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(50).Within(1e-9));
            Assert.That(loop.RaiseSpirit("wind_spirit"), Is.EqualTo(SpiritResult.Raised));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(30).Within(1e-9));
            Assert.That(loop.Spirits.Level("wind_spirit"), Is.EqualTo(2));
        }

        [Test]
        public void SpiritsNeedEchoAndStopAtTheMaximum()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(10) });
            loop.State.Spirits["tide_spirit"] = 10;

            Assert.That(loop.RaiseSpirit("ghost"), Is.EqualTo(SpiritResult.UnknownSpirit));
            Assert.That(loop.RaiseSpirit("tide_spirit"), Is.EqualTo(SpiritResult.MaxLevel));
            Assert.That(loop.RaiseSpirit("wind_spirit"), Is.EqualTo(SpiritResult.NotEnoughEcho));
        }

        [Test]
        public void SleepingSpiritsDoNothing()
        {
            var hero = SkillTestHero.Of(HeroClass.Oracle);

            hero.Ticks(20 * GameLoop.TicksPerSecond);

            Assert.That(hero.Loop.Spirits.Strikes, Is.Zero);
        }

        [Test]
        public void WindSpiritStrikesEveryThousandPixels()
        {
            var hero = With("wind_spirit");
            for (var i = 0; i < 3; i++)
            {
                hero.Input.MoveMouse(1000);
                hero.Loop.Tick();
            }

            Assert.That(hero.Loop.Spirits.Strikes, Is.EqualTo(3));
        }

        [Test]
        public void HearthSpiritStrikesEveryThirdSkillCast()
        {
            var hero = With("hearth_spirit", 1, "by_the_gait");
            for (var i = 0; i < 3; i++)
            {
                hero.State.Resource = 100;
                hero.State.Skills[0].CooldownTicks = 0;
                Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            }

            hero.Loop.Tick();

            Assert.That(hero.Loop.Spirits.Strikes, Is.EqualTo(1));
        }

        [Test]
        public void MossSpiritStrikesAfterFiveQuietSecondsAndInputResetsIt()
        {
            var hero = With("moss_spirit");
            hero.Ticks(4 * GameLoop.TicksPerSecond);
            hero.Ticks(1, keysPerTick: 1);
            hero.Ticks(4 * GameLoop.TicksPerSecond);
            Assert.That(hero.Loop.Spirits.Strikes, Is.Zero);

            hero.Ticks(GameLoop.TicksPerSecond);
            Assert.That(hero.Loop.Spirits.Strikes, Is.EqualTo(1));
        }

        [Test]
        public void HuntAndStarSpiritsCountKillsAndCrits()
        {
            var hunt = With("hunt_spirit");
            var star = With("star_spirit");
            hunt.State.Kills += 5;
            star.State.Crits += 40;

            hunt.Loop.Tick();
            star.Loop.Tick();

            Assert.That(hunt.Loop.Spirits.Strikes, Is.EqualTo(1));
            Assert.That(star.Loop.Spirits.Strikes, Is.EqualTo(2));
        }

        [Test]
        public void TideSpiritStrikesEveryTenSeconds()
        {
            var hero = With("tide_spirit");

            hero.Ticks(10 * GameLoop.TicksPerSecond);

            Assert.That(hero.Loop.Spirits.Strikes, Is.EqualTo(1));
        }

        [Test]
        public void LevelsMakeTheStrikeStronger()
        {
            var hero = With("wind_spirit", 3);

            hero.Input.MoveMouse(1000);
            hero.Loop.Tick();

            var strike = Enumerable.Range(0, hero.Loop.Events.Count).Select(i => hero.Loop.Events[i])
                .Where(e => e.Kind == GameEventKind.Damage && e.Source == DamageSource.Skill).Sum(e => e.Amount.ToDouble());
            Assert.That(strike, Is.EqualTo(hero.Loop.Combat.SkillDamage(hero.KeyDamage, 5, StatTags.Skill).ToDouble()).Within(1e-6));
        }

        [Test]
        public void SpiritsSurviveSavingAndPrestige()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(100) });
            Assert.That(loop.RaiseSpirit("owl_spirit"), Is.EqualTo(SpiritResult.Awakened));
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Spirits["owl_spirit"], Is.EqualTo(1));
            loop.State.HighestStage = 500;
            Assert.That(loop.Prestige.TryPrestige(), Is.True);
            Assert.That(loop.Spirits.Level("owl_spirit"), Is.EqualTo(1));
        }
    }
}
