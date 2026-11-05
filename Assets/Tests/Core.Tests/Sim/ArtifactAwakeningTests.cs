using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ArtifactAwakeningTests
    {
        private static ArtifactCatalog Catalog => TestWorld.Real.Artifacts;
        private static ArtifactAwakening Rules => Catalog.Awakening;

        private static GameLoop WithAllArtifacts()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            foreach (var artifact in loop.Artifacts.Owned.Keys.ToList())
                loop.State.Artifacts.Remove(artifact);
            foreach (var artifact in TestWorld.Data.Artifacts.All)
                loop.State.Artifacts[artifact.Id] = 1;
            loop.Artifacts.Rebuild();
            loop.Tick();
            return loop;
        }

        [Test]
        public void EachArtifactWakesFromItsOwnInput()
        {
            Assert.That(Rules.Slots, Is.EqualTo(3));
            Assert.That(Catalog.TryGet("typewriter", out var typewriter) && typewriter.Awaken == AwakeningInput.Keys, Is.True);
            Assert.That(Catalog.TryGet("wind_compass", out var compass) && compass.Awaken == AwakeningInput.MouseMeters, Is.True);
            Assert.That(Catalog.TryGet("hourglass", out var hourglass) && hourglass.Awaken == AwakeningInput.Bosses, Is.True);
            for (var input = 0; input < ArtifactAwakening.InputCount; input++)
                Assert.That(Catalog.All.Any(a => (int)a.Awaken == input), Is.True, ((AwakeningInput)input).ToString());
        }

        [Test]
        public void LevelsNeedMoreInputEachTimeAndStopAtTheCap()
        {
            var first = Rules.First(AwakeningInput.Keys);
            Assert.That(Rules.LevelAt(AwakeningInput.Keys, first - 1), Is.EqualTo(0));
            Assert.That(Rules.LevelAt(AwakeningInput.Keys, first), Is.EqualTo(1));
            Assert.That(Rules.Needed(AwakeningInput.Keys, 2), Is.EqualTo(first + first * Rules.Growth).Within(1e-6));
            Assert.That(Rules.LevelAt(AwakeningInput.Keys, Rules.Needed(AwakeningInput.Keys, 2) - 1), Is.EqualTo(1));
            Assert.That(Rules.LevelAt(AwakeningInput.Keys, double.MaxValue / 2), Is.EqualTo(Rules.MaxLevel));
        }

        [Test]
        public void EnchantingOpensAfterTheFullSetAndHasThreeSlots()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Artifacts["typewriter"] = 1;
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.Closed));

            loop = WithAllArtifacts();
            Assert.That(loop.Artifacts.EnchantOpen, Is.True);
            Assert.That(loop.Artifacts.Enchant("nothing"), Is.EqualTo(EnchantResult.Unknown));
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.Done));
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.AlreadyEnchanted));
            Assert.That(loop.Artifacts.Enchant("wind_compass"), Is.EqualTo(EnchantResult.Done));
            Assert.That(loop.Artifacts.Enchant("hourglass"), Is.EqualTo(EnchantResult.Done));
            Assert.That(loop.Artifacts.Enchant("titan_skull"), Is.EqualTo(EnchantResult.NoSlot));
            Assert.That(loop.Artifacts.Disenchant("hourglass"), Is.EqualTo(EnchantResult.Done));
            Assert.That(loop.Artifacts.Disenchant("hourglass"), Is.EqualTo(EnchantResult.NotEnchanted));
            Assert.That(loop.Artifacts.Enchant("titan_skull"), Is.EqualTo(EnchantResult.Done));
        }

        [Test]
        public void OnlyEnchantedArtifactsGrowFromTheirInputAndGetStronger()
        {
            var loop = WithAllArtifacts();
            loop.Artifacts.Enchant("typewriter");
            var before = loop.Artifacts.Modifiers.Where(m => m.Stat == CombatStats.Damage && m.Tags == StatTags.Strike).Sum(m => m.Value);

            loop.State.TotalInput.Keys += (ulong)Rules.Needed(AwakeningInput.Keys, 2);
            loop.State.TotalInput.MouseMeters += 1e6;
            loop.Tick();

            Assert.That(loop.Artifacts.AwakeningLevel("typewriter"), Is.EqualTo(2));
            Assert.That(loop.Artifacts.AwakeningProgress("wind_compass"), Is.EqualTo(0), "not enchanted, does not grow");
            var after = loop.Artifacts.Modifiers.Where(m => m.Stat == CombatStats.Damage && m.Tags == StatTags.Strike).Sum(m => m.Value);
            Assert.That(after, Is.EqualTo(before * Rules.Scale(2)).Within(1e-9));

            loop.Artifacts.Disenchant("typewriter");
            Assert.That(loop.Artifacts.Modifiers.Where(m => m.Stat == CombatStats.Damage && m.Tags == StatTags.Strike).Sum(m => m.Value), Is.EqualTo(before).Within(1e-9));
            loop.Artifacts.Enchant("typewriter");
            Assert.That(loop.Artifacts.AwakeningLevel("typewriter"), Is.EqualTo(2), "progress stays with the artifact");
        }

        [Test]
        public void AwakeningIsSavedAndInputBeforeEnchantingDoesNotCount()
        {
            var loop = WithAllArtifacts();
            loop.State.TotalInput.Keys += 1_000_000;
            loop.Tick();
            loop.Artifacts.Enchant("typewriter");
            loop.Tick();
            Assert.That(loop.Artifacts.AwakeningProgress("typewriter"), Is.EqualTo(0));
            loop.State.TotalInput.Keys += 500;
            loop.Tick();
            Assert.That(loop.Artifacts.AwakeningProgress("typewriter"), Is.EqualTo(500));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Awakening.Enchanted, Is.EqualTo(new[] { "typewriter" }));
            Assert.That(copy.Awakening.Progress["typewriter"], Is.EqualTo(500));
        }

        [Test]
        public void TickingAwakeningDoesNotAllocate()
        {
            var loop = WithAllArtifacts();
            loop.Artifacts.Enchant("typewriter");
            loop.Artifacts.Enchant("wind_compass");
            loop.Artifacts.Enchant("hourglass");
            loop.Artifacts.TickAwakening();
            loop.State.TotalInput.Keys += 10;
            loop.Artifacts.TickAwakening();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                loop.State.TotalInput.Keys += 10;
                loop.State.TotalInput.MouseMeters += 0.01;
                loop.Artifacts.TickAwakening();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
