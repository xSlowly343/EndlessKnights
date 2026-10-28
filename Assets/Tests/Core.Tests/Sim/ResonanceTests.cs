using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ResonanceTests
    {
        private static ResonanceCatalog Catalog => TestWorld.Data.Resonance;

        private static (GameLoop Loop, FakeInputSource Input) Hero(string branch, int level)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var state = TestWorld.Classless(new GameState { Class = HeroClass.Oracle });
            if (level > 0)
                state.Resonance[branch] = level;
            var loop = TestWorld.Loop(input, state, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static List<double> Damage(GameLoop loop, DamageSource source) =>
            Enumerable.Range(0, loop.Events.Count).Select(i => loop.Events[i])
                .Where(e => e.Kind == GameEventKind.Damage && e.Source == source).Select(e => e.Amount.ToDouble()).ToList();

        [Test]
        public void FourBranchesOfFifteenLevelsWithMilestonesAtFiveTenAndFifteen()
        {
            Assert.That(Catalog.All.Select(branch => branch.Id), Is.EquivalentTo(new[] { "keys", "mouse", "clicks", "idle" }));
            foreach (var branch in Catalog.All)
            {
                Assert.That(branch.MaxLevel, Is.EqualTo(15), branch.Id);
                Assert.That(branch.Milestones.Select(milestone => milestone.Level), Is.EqualTo(new[] { 5, 10, 15 }), branch.Id);
            }
        }

        [Test]
        public void RaisingABranchCostsEchoAndAddsDamageOfItsInput()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(100) });
            var before = loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard);

            Assert.That(loop.RaiseResonance("keys"), Is.EqualTo(ResonanceResult.Raised));
            Assert.That(loop.RaiseResonance("keys"), Is.EqualTo(ResonanceResult.Raised));

            Assert.That(loop.Resonance.Level("keys"), Is.EqualTo(2));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(100 - 10 - 13).Within(1e-6));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard) - before, Is.EqualTo(0.06).Within(1e-9));
        }

        [Test]
        public void BranchesStopAtTheMaximumAndNeedEcho()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(5) });
            loop.State.Resonance["mouse"] = 15;

            Assert.That(loop.RaiseResonance("nothing"), Is.EqualTo(ResonanceResult.UnknownBranch));
            Assert.That(loop.RaiseResonance("mouse"), Is.EqualTo(ResonanceResult.MaxLevel));
            Assert.That(loop.RaiseResonance("keys"), Is.EqualTo(ResonanceResult.NotEnoughEcho));
        }

        [Test]
        public void KeyResonanceFiveMakesEveryHundredthKeyHitFiveTimesStronger()
        {
            var (loop, input) = Hero("keys", 5);
            for (var i = 0; i < 200; i++)
            {
                if (i % 2 == 0)
                    input.PressKeys(1);
                loop.Tick();
            }

            var hits = Damage(loop, DamageSource.Keyboard);
            Assert.That(hits.Count, Is.EqualTo(100));
            Assert.That(hits[99] / hits[98], Is.EqualTo(5).Within(0.05));
            Assert.That(Enumerable.Range(1, hits.Count - 1).Count(i => hits[i] > hits[i - 1] * 3), Is.EqualTo(1));
        }

        [Test]
        public void KeyResonanceTenMakesTheBurstTenTimesStronger()
        {
            var (loop, _) = Hero("keys", 10);

            Assert.That(loop.Hero.Value(CombatStats.KeyBurstMultiplier, 0, StatTags.Keyboard), Is.EqualTo(10));
        }

        [Test]
        public void MouseResonanceFiveAddsABurstForEveryFiveThousandPixels()
        {
            var (loop, input) = Hero("mouse", 5);
            for (var i = 0; i < 5; i++)
            {
                input.MoveMouse(1000);
                loop.Tick();
            }

            var hits = Damage(loop, DamageSource.Mouse);
            Assert.That(hits.Count, Is.EqualTo(6));
            Assert.That(hits[5] / hits[4], Is.EqualTo(5).Within(1e-6));
        }

        [Test]
        public void ClickResonanceTenTakesTwoTenthsOfASecondOffEveryCooldownPerClick()
        {
            var hero = SkillTestHero.Of(HeroClass.Oracle, "by_the_gait");
            hero.State.Resonance["clicks"] = 10;
            hero.Loop.Resonance.UseCatalog(Catalog);
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
            var cooldown = hero.State.Skills[0].CooldownTicks;

            for (var i = 0; i < 5; i++)
            {
                hero.Input.ClickMiddle();
                hero.Loop.Tick();
            }

            Assert.That(hero.State.Skills[0].CooldownTicks, Is.EqualTo(cooldown - 5 - 5 * 2));
        }

        [Test]
        public void IdleResonanceFiveStrikesDuringPauses()
        {
            var (loop, _) = Hero("idle", 5);

            Assert.That(loop.Hero.Value(CombatStats.IdleHitsPerSecond, 0, StatTags.Idle), Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void ResonanceSurvivesSavingAndPrestige()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(100) });
            Assert.That(loop.RaiseResonance("idle"), Is.EqualTo(ResonanceResult.Raised));
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Resonance["idle"], Is.EqualTo(1));
            loop.State.HighestStage = 500;
            Assert.That(loop.Prestige.TryPrestige(), Is.True);
            Assert.That(loop.Resonance.Level("idle"), Is.EqualTo(1));
        }
    }
}
