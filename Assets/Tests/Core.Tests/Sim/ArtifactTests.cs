using System.Collections.Generic;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ArtifactTests
    {
        private static ArtifactCatalog Catalog => TestWorld.Data.Artifacts;

        private static GameLoop WithEcho(double echo, ulong seed = 1)
        {
            var loop = TestWorld.Loop(state: new GameState(seed));
            loop.State.Echo = BigNumber.FromDouble(echo);
            return loop;
        }

        [Test]
        public void MvpHasFiveArtifacts()
        {
            Assert.That(Catalog.All.Count, Is.EqualTo(5));
        }

        [Test]
        public void UnlockingNeedsEchoAndSpendsIt()
        {
            var loop = WithEcho(Catalog.UnlockCost.At(1).ToDouble() - 1);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);

            loop.State.Echo = BigNumber.FromDouble(100);
            var artifact = loop.Artifacts.TryUnlock();

            Assert.That(artifact, Is.Not.Null);
            Assert.That(loop.State.Artifacts[artifact!.Id], Is.EqualTo(1));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(100 - Catalog.UnlockCost.At(1).ToDouble()));
        }

        [Test]
        public void EachUnlockCostsMoreThanThePrevious()
        {
            var loop = WithEcho(1e9);
            var previous = BigNumber.Zero;

            for (var i = 0; i < Catalog.All.Count; i++)
            {
                var cost = loop.Artifacts.UnlockCost;
                Assert.That(cost, Is.GreaterThan(previous));
                previous = cost;
                loop.Artifacts.TryUnlock();
            }
        }

        [Test]
        public void NoDuplicatesUntilAllAreOpen()
        {
            var loop = WithEcho(1e9);
            var seen = new HashSet<string>();

            for (var i = 0; i < Catalog.All.Count; i++)
                Assert.That(seen.Add(loop.Artifacts.TryUnlock()!.Id), Is.True);

            Assert.That(loop.Artifacts.AllUnlocked, Is.True);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);
        }

        [Test]
        public void ChancesAreEqualAmongLockedAndMatchStatistics()
        {
            var counts = new Dictionary<string, int>();
            const int runs = 5000;
            for (ulong seed = 1; seed <= runs; seed++)
            {
                var loop = WithEcho(1e9, seed);
                var chances = loop.Artifacts.Chances();
                Assert.That(chances.Count, Is.EqualTo(Catalog.All.Count));
                Assert.That(chances[0].Chance, Is.EqualTo(1.0 / Catalog.All.Count));
                var id = loop.Artifacts.TryUnlock()!.Id;
                counts[id] = counts.TryGetValue(id, out var count) ? count + 1 : 1;
            }

            var expected = runs / (double)Catalog.All.Count;
            foreach (var artifact in Catalog.All)
                Assert.That(counts[artifact.Id], Is.EqualTo(expected).Within(expected * 0.1), artifact.Id);
        }

        [Test]
        public void LevelsCostMoreAndAddEffectLinearly()
        {
            var loop = WithEcho(1e9);
            loop.State.Artifacts["typewriter"] = 1;
            loop.Artifacts.Rebuild();
            var levelOne = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            var costOne = loop.Artifacts.LevelCost("typewriter");

            Assert.That(loop.Artifacts.TryLevelUp("typewriter"), Is.True);
            Assert.That(loop.State.Artifacts["typewriter"], Is.EqualTo(2));
            Assert.That(loop.Artifacts.LevelCost("typewriter"), Is.GreaterThan(costOne));
            Assert.That(levelOne, Is.EqualTo(100 * (1 + 0.10 + Catalog.AllDamagePerLevel)).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100 * (1 + 0.20 + 2 * Catalog.AllDamagePerLevel)).Within(1e-9));
            Assert.That(loop.Artifacts.TryLevelUp("wind_compass"), Is.False);
        }

        [Test]
        public void EveryArtifactAlsoGivesASmallAllDamageBonus()
        {
            var loop = WithEcho(0);
            loop.State.Artifacts["hourglass"] = 3;
            loop.Artifacts.Rebuild();

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Click), Is.EqualTo(100 * (1 + 3 * Catalog.AllDamagePerLevel)).Within(1e-9));
        }

        [Test]
        public void HourglassMakesTheBossTimerLonger()
        {
            var state = new GameState();
            state.Artifacts["hourglass"] = 5;
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            var input = new Game.Core.Input.FakeInputSource();
            var strong = TestWorld.Loop(input, state, hero);
            for (var i = 0; i < TestWorld.Data.Stages.EnemiesPerStage; i++)
            {
                input.PressKeys(1);
                strong.Tick();
                strong.Tick();
            }

            Assert.That(strong.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(strong.State.BossSecondsLeft, Is.EqualTo(TestWorld.Data.Stages.BossTimerSeconds + 10).Within(0.11));
        }

        [Test]
        public void ArtifactsSurvivePrestigeAndSave()
        {
            var loop = WithEcho(1e9);
            loop.Artifacts.TryUnlock();
            loop.State.HighestStage = 40;
            loop.State.RunGoldEarned = BigNumber.FromDouble(1e5);
            loop.Prestige.TryPrestige();

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loop.State.Artifacts.Count, Is.EqualTo(1));
            Assert.That(loaded.Artifacts, Is.EqualTo(loop.State.Artifacts));
        }
    }
}
