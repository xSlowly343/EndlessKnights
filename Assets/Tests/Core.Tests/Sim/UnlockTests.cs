using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class UnlockTests
    {
        private static GameLoop Real(long record, int prestiges = 0)
        {
            var state = new GameState(5) { Class = HeroClass.Runedancer, RecordStage = record, Prestiges = prestiges };
            var loop = new GameLoop(TestWorld.Real, new FakeInputSource(), state, new StatSheet());
            loop.State.Echo = BigNumber.FromDouble(1e30);
            return loop;
        }

        [Test]
        public void MechanicsOpenStepByStep()
        {
            var rules = TestWorld.Real.Unlocks;

            Assert.That(rules.For(Feature.Artifacts).Prestiges, Is.EqualTo(1));
            Assert.That(rules.For(Feature.Resonance).Stage, Is.LessThan(rules.For(Feature.Spirits).Stage));
            Assert.That(rules.For(Feature.Spirits).Stage, Is.LessThan(rules.For(Feature.Atlas).Stage));
            Assert.That(rules.For(Feature.Atlas).Stage, Is.LessThan(rules.For(Feature.Chronicle).Stage));
            for (var group = 2; group <= 5; group++)
                Assert.That(rules.ArtifactGroupStage(group), Is.GreaterThan(rules.ArtifactGroupStage(group - 1)));
            Assert.That(rules.ArtifactGroupStage(5), Is.LessThanOrEqualTo(101000));
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
            for (var i = 0; i < 35; i++)
                Assert.That(loop.Artifacts.TryUnlock(), Is.Not.Null);
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);
            Assert.That(loop.Artifacts.WaitsForGroup, Is.True);
            Assert.That(loop.Artifacts.AllUnlocked, Is.False);

            loop.State.RecordStage = 5000;
            Assert.That(loop.Artifacts.TryUnlock()!.Group, Is.EqualTo(2));
        }

        [Test]
        public void SetCraftWaitsForItsStage()
        {
            var early = Real(50);

            Assert.That(early.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.Locked));
        }

        [Test]
        public void ADungeonTierOpensSlatesWhateverTheStage()
        {
            var loop = Real(60000);
            Assert.That(loop.Unlocks.IsOpen(Feature.Slates), Is.False);
            var missing = loop.Unlocks.Missing(Feature.Slates)!.Value;
            Assert.That(missing.Kind, Is.EqualTo(UnlockNeedKind.DungeonTier));
            Assert.That(missing.Value, Is.EqualTo(5));
            Assert.That(loop.Unlocks.Have(missing), Is.Zero);

            loop.State.Dungeons.BestTier = 5;

            Assert.That(loop.Unlocks.IsOpen(Feature.Slates), Is.True);
            Assert.That(loop.Unlocks.Missing(Feature.Slates), Is.Null);
        }

        [Test]
        public void ACampBuildingOpensSpiritsEarlierThanTheStageAlone()
        {
            var loop = Real(2000);
            Assert.That(loop.Unlocks.IsOpen(Feature.Spirits), Is.False);
            Assert.That(loop.Unlocks.Missing(Feature.Spirits)!.Value.Id, Is.EqualTo("bonfire"));

            loop.State.Camp.Levels["bonfire"] = 2;
            Assert.That(loop.Unlocks.IsOpen(Feature.Spirits), Is.True);

            var noCamp = Real(4999);
            Assert.That(noCamp.Unlocks.IsOpen(Feature.Spirits), Is.False);
            noCamp.State.RecordStage = 5000;
            Assert.That(noCamp.Unlocks.IsOpen(Feature.Spirits), Is.True);
        }

        [Test]
        public void ACampBuildingCountsOnlyOnceItsTierIsReached()
        {
            var loop = Real(10000);
            loop.State.Camp.Levels["forge"] = 2;
            loop.Camp.SetGuilds(new[] { "smiths" });

            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.False, "the forge is not built at the camp site");

            loop.State.Camp.Tier = 1;
            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.True);
        }

        [Test]
        public void WhatAHideoutBuildingOpenedStaysOpenAfterLeavingThePartyAndInTheSave()
        {
            var loop = Real(10000);
            loop.Tick();
            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.False);
            var before = loop.Events.CountOf(GameEventKind.FeatureOpened);

            loop.State.Hideout.Buildings["shared_forge"] = 1;
            loop.Tick();
            loop.Tick();
            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.True);
            Assert.That(loop.Events.CountOf(GameEventKind.FeatureOpened), Is.EqualTo(before + 1));

            loop.State.Hideout.Buildings.Clear();
            Assert.That(loop.Unlocks.IsOpen(Feature.SetCraft), Is.True);

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Real.Enemies, TestWorld.Real.Gems, TestWorld.Real.Weapons, TestWorld.Real.Items, TestWorld.Real.Slates);
            Assert.That(copy.OpenedFeatures, Does.Contain(Feature.SetCraft));
        }

        [Test]
        public void MechanicsWithoutConditionsRaiseNoOpeningEvent()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.FeatureOpened), Is.Zero);
            Assert.That(loop.Unlocks.IsOpen(Feature.Atlas), Is.True);
        }

        [Test]
        public void ConditionsReadEveryKindAndRoutes()
        {
            var rules = UnlockRules.Load(@"{ ""version"": 1, ""features"": {
                ""atlas"": { ""heroLevel"": 100, ""limits"": 1, ""trials"": 3, ""riftDepth"": 25, ""campTier"": 2, ""hideoutTier"": 4 },
                ""spirits"": { ""anyOf"": [ { ""stage"": 10, ""hideoutBuilding"": { ""dummy"": 2 } }, { ""stage"": 40 } ] },
                ""slates"": { ""stage"": 0 } }, ""artifactGroups"": [] }");
            var state = new GameState();
            var unlocks = new Unlocks(rules, state);

            Assert.That(rules.For(Feature.Slates).Always, Is.True);
            Assert.That(rules.For(Feature.Spirits).Routes, Is.EqualTo(2));
            Assert.That(rules.For(Feature.Spirits).Stage, Is.EqualTo(10));
            Assert.That(unlocks.IsOpen(Feature.Atlas), Is.False);

            state.HeroLevel = 100;
            state.GatesCleared.Add(50000);
            state.Chronicle.Completed.AddRange(new[] { "a", "b", "c" });
            state.Rift.Deepest = 25;
            state.Camp.Tier = 1;
            state.Hideout.Tier = 3;
            Assert.That(unlocks.Missing(Feature.Atlas)!.Value.Kind, Is.EqualTo(UnlockNeedKind.HideoutTier));
            state.Hideout.Tier = 4;
            Assert.That(unlocks.IsOpen(Feature.Atlas), Is.True);

            state.RecordStage = 10;
            Assert.That(unlocks.IsOpen(Feature.Spirits), Is.False);
            state.Hideout.Buildings["dummy"] = 2;
            Assert.That(unlocks.IsOpen(Feature.Spirits), Is.True);
        }

        [Test]
        public void BrokenConditionsAreReported()
        {
            const string json = @"{ ""version"": 1, ""features"": { ""atlas"": { ""stage"": 5 } }, ""artifactGroups"": [] }";

            Assert.Throws<ContentException>(() => UnlockRules.Load(json.Replace(@"""stage"": 5", @"""moonPhase"": 5")));
            Assert.Throws<ContentException>(() => UnlockRules.Load(json.Replace(@"""stage"": 5", @"""stage"": 5, ""anyOf"": [ { ""stage"": 9 } ]")));
            Assert.Throws<ContentException>(() => UnlockRules.Load(json.Replace(@"""stage"": 5", @"""anyOf"": []")));
            var unknown = UnlockRules.Load(json.Replace(@"""stage"": 5", @"""campBuilding"": { ""lighthouse"": 1 }"));
            Assert.Throws<ContentException>(() => unknown.CheckBuildings(TestWorld.Real.Camp, TestWorld.Real.Hideout));
        }
    }
}
