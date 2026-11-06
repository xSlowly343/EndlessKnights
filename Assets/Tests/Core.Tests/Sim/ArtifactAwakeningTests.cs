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
            loop.State.Echo = BigNumber.FromDouble(1e300);
            loop.Artifacts.Rebuild();
            loop.Tick();
            return loop;
        }

        [Test]
        public void EachArtifactWakesFromItsOwnInput()
        {
            Assert.That(Rules.Open, Is.True);
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
        public void EnchantingOpensAfterTheFullSetAndEveryArtifactCanBeEnchanted()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Artifacts["typewriter"] = 1;
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.Closed));

            loop = WithAllArtifacts();
            Assert.That(loop.Artifacts.EnchantOpen, Is.True);
            Assert.That(loop.Artifacts.Enchant("nothing"), Is.EqualTo(EnchantResult.Unknown));
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.Done));
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.AlreadyEnchanted));
            foreach (var artifact in TestWorld.Data.Artifacts.All)
            {
                if (!loop.Artifacts.IsEnchanted(artifact.Id))
                    Assert.That(loop.Artifacts.Enchant(artifact.Id), Is.EqualTo(EnchantResult.Done), artifact.Id);
            }
            Assert.That(loop.Artifacts.AllEnchanted, Is.True);
        }

        [Test]
        public void EachEnchantCostsEchoLikeTheNextDiscovery()
        {
            var loop = WithAllArtifacts();
            var count = TestWorld.Data.Artifacts.All.Count;
            var first = loop.Artifacts.EnchantCost;
            var raw = TestWorld.Data.Artifacts.UnlockCost.At(count + 1).ToDouble();
            Assert.That(first.ToDouble(), Is.InRange(raw * 0.1, raw * (1 + 1e-9)), "the next discovery cost, with the artifact discount");

            loop.State.Echo = first;
            Assert.That(loop.Artifacts.Enchant("typewriter"), Is.EqualTo(EnchantResult.Done));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(0).Within(first.ToDouble() * 1e-9));
            var second = loop.Artifacts.EnchantCost;
            Assert.That(second.ToDouble() / first.ToDouble(), Is.EqualTo(TestWorld.Data.Artifacts.UnlockCost.At(count + 2).ToDouble() / raw).Within(1e-9));
            Assert.That(loop.Artifacts.Enchant("wind_compass"), Is.EqualTo(EnchantResult.NotEnoughEcho));
            Assert.That(loop.Artifacts.IsEnchanted("wind_compass"), Is.False);
        }

        [Test]
        public void OnlyEnchantedArtifactsGrowFromTheirInputAndGetStronger()
        {
            var loop = WithAllArtifacts();
            loop.Artifacts.Enchant("typewriter");
            var plain = WithAllArtifacts().Artifacts.Modifiers.Where(m => m.Stat == CombatStats.Damage && m.Tags == StatTags.Strike).Sum(m => m.Value);
            var before = loop.Artifacts.Modifiers.Where(m => m.Stat == CombatStats.Damage && m.Tags == StatTags.Strike).Sum(m => m.Value);
            Assert.That(before, Is.EqualTo(plain * (1 + Rules.EnchantBonus)).Within(1e-9), "enchanting alone gives a flat boost");

            loop.State.TotalInput.Keys += (ulong)Rules.Needed(AwakeningInput.Keys, 2);
            loop.State.TotalInput.MouseMeters += 1e6;
            loop.Tick();

            Assert.That(loop.Artifacts.AwakeningLevel("typewriter"), Is.EqualTo(2));
            Assert.That(loop.Artifacts.AwakeningProgress("wind_compass"), Is.EqualTo(0), "not enchanted, does not grow");
            var after = loop.Artifacts.Modifiers.Where(m => m.Stat == CombatStats.Damage && m.Tags == StatTags.Strike).Sum(m => m.Value);
            Assert.That(after, Is.EqualTo(before * Rules.Scale(2) / Rules.Scale(0)).Within(1e-9));
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
