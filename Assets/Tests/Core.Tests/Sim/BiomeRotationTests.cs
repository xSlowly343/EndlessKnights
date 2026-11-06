using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BiomeRotationTests
    {
        private static StageTable Stages => TestWorld.Data.Stages;

        [Test]
        public void TheBiomeTurnsOnlyAfterFiveMinutesAndABossWin()
        {
            var state = new GameState { Stage = 3 };
            Assert.That(BiomeRotation.Current(Stages, state, 3), Is.EqualTo("green_hills"));
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.False);
            Assert.That(state.Biome, Is.EqualTo("green_hills"));

            state.Tick += BiomeRotation.TicksPerTurn(Stages) - 1;
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.False, "five minutes have not passed");
            state.Tick += 1;
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.True);
            Assert.That(state.Biome, Is.Not.EqualTo("green_hills"));
            Assert.That(state.PreviousBiome, Is.EqualTo("green_hills"));
            Assert.That(BiomeRotation.Current(Stages, state, 3), Is.EqualTo(state.Biome), "the biome no longer follows the stage");
        }

        [Test]
        public void AnyThreeBiomesInARowAreDifferentAndAllBiomesCome()
        {
            var state = new GameState { Stage = 3 };
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
        public void TheHeartStartsAtStageTwentyThousandAndStopsTheRotation()
        {
            var state = new GameState { Stage = Stages.HeartFromStage, Biome = "ice_peak" };
            Assert.That(BiomeRotation.Current(Stages, state, state.Stage), Is.EqualTo("rift_heart"));
            state.Tick += BiomeRotation.TicksPerTurn(Stages) * 2;
            Assert.That(BiomeRotation.TryTurn(Stages, state), Is.False);
        }

        [Test]
        public void BeatingABossLateEnoughTurnsTheBiomeInTheGameAndTheSaveKeepsIt()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            var loop = TestWorld.Loop(input, hero: hero);
            loop.State.Biome = "green_hills";
            loop.State.BiomeSinceTick = -BiomeRotation.TicksPerTurn(Stages);
            for (var key = 0; key <= Stages.EnemiesPerStage && loop.State.Stage == 1; key++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            Assert.That(loop.State.Stage, Is.EqualTo(2));
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
