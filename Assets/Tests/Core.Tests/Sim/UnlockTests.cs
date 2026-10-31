using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class UnlockTests
    {
        private static GameLoop Real(long record, int prestiges = 0)
        {
            var state = new GameState(5) { Class = HeroClass.Viking, RecordStage = record, Prestiges = prestiges };
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), state, new StatSheet());
            loop.State.Echo = BigNumber.FromDouble(1e30);
            return loop;
        }

        [Test]
        public void MechanicsOpenStepByStep()
        {
            var rules = TestWorld.Real.Unlocks;

            Assert.That(rules.For(Feature.Artifacts).Prestiges, Is.EqualTo(1));
            Assert.That(rules.For(Feature.Cutting).Stage, Is.LessThan(rules.For(Feature.Resonance).Stage));
            Assert.That(rules.For(Feature.Resonance).Stage, Is.LessThan(rules.For(Feature.Spirits).Stage));
            Assert.That(rules.For(Feature.Spirits).Stage, Is.LessThan(rules.For(Feature.Atlas).Stage));
            Assert.That(rules.For(Feature.Atlas).Stage, Is.LessThan(rules.For(Feature.Chronicle).Stage));
            Assert.That(rules.ArtifactGroupStage(3), Is.EqualTo(50000));
            Assert.That(rules.ArtifactGroupStage(4), Is.GreaterThan(50000));
        }

        [Test]
        public void EchoSystemsRefuseUntilTheirStage()
        {
            var early = Real(200, prestiges: 1);
            var late = Real(10000, prestiges: 1);

            Assert.That(early.RaiseResonance("keys"), Is.EqualTo(ResonanceResult.Locked));
            Assert.That(early.RaiseSpirit("wind_spirit"), Is.EqualTo(SpiritResult.Locked));
            Assert.That(early.AllocateAtlasNode("map_of_the_rim"), Is.EqualTo(AtlasResult.Locked));
            Assert.That(late.RaiseResonance("keys"), Is.EqualTo(ResonanceResult.Raised));
            Assert.That(late.RaiseSpirit("wind_spirit"), Is.EqualTo(SpiritResult.Awakened));
            Assert.That(late.AllocateAtlasNode("map_of_the_rim"), Is.EqualTo(AtlasResult.Allocated));
        }

        [Test]
        public void ArtifactsNeedTheFirstPrestigeAndLateGroupsNeedTheirStage()
        {
            var fresh = Real(500, prestiges: 0);
            Assert.That(fresh.Artifacts.TryUnlock(), Is.Null);

            var loop = Real(500, prestiges: 1);
            for (var i = 0; i < 60; i++)
                Assert.That(loop.Artifacts.TryUnlock(), Is.Not.Null);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);
            Assert.That(loop.Artifacts.WaitsForGroup, Is.True);
            Assert.That(loop.Artifacts.AllUnlocked, Is.False);

            loop.State.RecordStage = 50000;
            Assert.That(loop.Artifacts.TryUnlock()!.Group, Is.EqualTo(3));
        }

        [Test]
        public void CuttingAndSetCraftWaitForTheirStage()
        {
            var early = Real(50);
            early.State.Drops.UncutGems = 1;
            var gem = TestWorld.Real.Gems.All[0];

            Assert.That(early.CutGem(gem), Is.EqualTo(CutResult.Locked));
            Assert.That(early.State.Drops.UncutGems, Is.EqualTo(1));
            Assert.That(early.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.Locked));
        }
    }
}
