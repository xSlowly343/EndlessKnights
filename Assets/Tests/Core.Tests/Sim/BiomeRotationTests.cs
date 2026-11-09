using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BiomeRotationTests
    {
        private static StageTable Stages => TestWorld.Data.Stages;
        private const long AllFive = 1000;

        [Test]
        public void BiomesOpenOneByOneAndTheRotationStartsWithTheThird()
        {
            Assert.That(Enumerable.Range(0, Stages.Pool.Count).Select(i => Stages.OpensAt(i)), Is.EqualTo(new long[] { 1, 100, 250, 500, 1000, 20_000 }));
            Assert.That(Stages.Biome(1), Is.EqualTo("green_hills"));
            Assert.That(Stages.Biome(99), Is.EqualTo("green_hills"));
            Assert.That(Stages.Biome(100), Is.EqualTo("green_hills"));
            Assert.That(Stages.Biome(101), Is.EqualTo("green_hills"));
            Assert.That(Stages.Biome(151), Is.EqualTo("sand_ruins"));
            Assert.That(Stages.RotatesAt(249), Is.False);
            Assert.That(Stages.RotatesAt(250), Is.True);

            var state = new GameState { Stage = 120, Biome = "ice_peak", Tick = BiomeRotation.TicksPerTurn(Stages) * 3 };
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.False, "two open biomes do not rotate");
            Assert.That(BiomeRotation.Current(Stages, state, 120), Is.EqualTo("green_hills"), "a closed biome from the save is not shown");
        }

        [Test]
        public void OnlyOpenBiomesComeInTheRotation()
        {
            var state = new GameState { Stage = 300 };
            var seen = new HashSet<string>();
            for (var i = 0; i < 200; i++)
            {
                state.Tick += BiomeRotation.TicksPerTurn(Stages);
                BiomeRotation.TryTurn(Stages, state);
                seen.Add(state.Biome);
            }
            Assert.That(seen.OrderBy(b => b), Is.EqualTo(new[] { "green_hills", "ice_peak", "sand_ruins" }));
        }

        [Test]
        public void TheBiomeTurnsOnlyAfterFiveMinutesAndABossWin()
        {
            var state = new GameState { Stage = AllFive };
            var first = Stages.Biome(AllFive);
            Assert.That(BiomeRotation.Current(Stages, state, AllFive), Is.EqualTo(first));
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.False);
            Assert.That(state.Biome, Is.EqualTo(first));

            state.Tick += BiomeRotation.TicksPerTurn(Stages) - 1;
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.False, "five minutes have not passed");
            state.Tick += 1;
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.True);
            Assert.That(state.Biome, Is.Not.EqualTo(first));
            Assert.That(state.PreviousBiome, Is.EqualTo(first));
            Assert.That(BiomeRotation.Current(Stages, state, AllFive), Is.EqualTo(state.Biome), "the biome no longer follows the stage");
        }

        [Test]
        public void AnyThreeBiomesInARowAreDifferentAndAllBiomesCome()
        {
            var state = new GameState { Stage = AllFive };
            var seen = new List<string>();
            for (var i = 0; i < 300; i++)
            {
                state.Tick += BiomeRotation.TicksPerTurn(Stages);
                BiomeRotation.TryTurn(Stages, state);
                seen.Add(state.Biome);
            }
            for (var i = 2; i < seen.Count; i++)
                Assert.That(new[] { seen[i - 2], seen[i - 1], seen[i] }.Distinct().Count(), Is.EqualTo(3), "turn " + i);
            Assert.That(seen.Distinct().OrderBy(b => b), Is.EqualTo(Stages.Rotation.OrderBy(b => b)));
            Assert.That(seen, Does.Not.Contain("rift_heart"));
        }

        [Test]
        public void TheHeartJoinsTheRotationAtStageTwentyThousand()
        {
            var state = new GameState { Stage = Stages.HeartFromStage, Biome = "ice_peak" };
            Assert.That(BiomeRotation.Current(Stages, state, state.Stage), Is.EqualTo("ice_peak"));
            var seen = new HashSet<string>();
            for (var i = 0; i < 300; i++)
            {
                state.Tick += BiomeRotation.TicksPerTurn(Stages);
                Assert.That(BiomeRotation.TryTurn(Stages, state), Is.True);
                seen.Add(state.Biome);
            }
            Assert.That(seen, Does.Contain("rift_heart"));
            Assert.That(seen.Count, Is.EqualTo(Stages.Pool.Count));
        }

        [Test]
        public void BeatingABossLateEnoughTurnsTheBiomeInTheGameAndTheSaveKeepsIt()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            var loop = TestWorld.Loop(input, hero: hero);
            const long start = AllFive + 1;
            loop.State.Stage = start;
            loop.State.RecordStage = start;
            loop.State.Biome = "green_hills";
            loop.State.BiomeSinceTick = -BiomeRotation.TicksPerTurn(Stages);
            for (var key = 0; key <= Stages.EnemiesOnStage(start) * 4 && loop.State.Stage == start; key++)
            {
                loop.State.EnemyHp = BigNumber.FromDouble(1);
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            Assert.That(loop.State.Stage, Is.EqualTo(start + 1));
            Assert.That(loop.State.Biome, Is.Not.EqualTo("green_hills"));
            Assert.That(loop.Events.CountOf(GameEventKind.BiomeChanged), Is.EqualTo(1));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Biome, Is.EqualTo(loop.State.Biome));
            Assert.That(copy.PreviousBiome, Is.EqualTo("green_hills"));
            Assert.That(copy.BiomeSinceTick, Is.EqualTo(loop.State.BiomeSinceTick));
        }

        [Test]
        public void ThePumpkinheadLivesInTheHills()
        {
            Assert.That(TestWorld.Data.Enemies.Normals("green_hills").Select(e => e.Id), Does.Contain("pumpkinhead"));
            Assert.That(TestWorld.Data.Bestiary.All.Any(e => e.Enemy == "pumpkinhead"), Is.True);
        }
    }
}
