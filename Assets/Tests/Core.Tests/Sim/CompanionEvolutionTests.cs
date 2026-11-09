using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionEvolutionTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void FourStagesAtLevelsOneTenTwentyThirty()
        {
            var stages = TestWorld.Data.Companions.Stages;
            Assert.That(stages.Count, Is.EqualTo(4));
            Assert.That(new[] { stages[0].Level, stages[1].Level, stages[2].Level, stages[3].Level }, Is.EqualTo(new[] { 1, 10, 20, 30 }));
            Assert.That(stages[3].TraitSlots, Is.EqualTo(3));
            Assert.That(stages[3].RankCap, Is.EqualTo(5));
        }

        [Test]
        public void TheYoungsterNeedsLevelTenBonesOfItsBiomeAndShards()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.LowLevel));
            loop.Companions.Progress("camp_dog").Level = 10;
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.NoBones));
            loop.Camp.Add("bone_ruins", 3);
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.NoBones), "bones of another biome");
            loop.Camp.Add("bone_hills", 3);
            loop.State.Drops.Shards = 19;
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.NoShards));
            loop.State.Drops.Shards = 25;
            loop.Events.Clear();
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Stage("camp_dog").Id, Is.EqualTo("youngster"));
            Assert.That(loop.Camp.Count("bone_hills"), Is.EqualTo(0));
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(5));
            Assert.That(loop.Events.CountOf(GameEventKind.CompanionEvolved), Is.EqualTo(1));
        }

        [Test]
        public void TheYoungsterOpensTwelveHourTrips()
        {
            var loop = Camp();
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 12), Is.EqualTo(ExpeditionResult.BadHours));
            loop.Companions.Progress("camp_dog").Stage = 1;
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 12), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.Expeditions.Send("camp_crow", "green_hills", 12), Is.EqualTo(ExpeditionResult.BadHours));
        }

        [Test]
        public void TheAdultNeedsABloomShardsAndTheFavouriteDish()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 20;
            dog.Stage = 1;
            loop.State.Drops.Shards = 50;
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.NoBlooms));
            loop.Camp.Add("herb_colossus_bloom", 1);
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.NoFavourite));
            loop.State.Cooking.Meals.Clear();
            for (var i = 0; i < 3; i++)
                loop.State.Cooking.Meals.Add("herb_balls");
            loop.State.Cooking.Meals.Add("carp_pie");
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Cooking.Meals, Is.EqualTo(new[] { "carp_pie" }));
            Assert.That(loop.Camp.Count("herb_colossus_bloom"), Is.EqualTo(0));
        }

        [Test]
        public void AFoodKindFavouriteTakesTenOfThatKind()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("flood_companion");
            var frog = loop.Companions.Progress("flood_companion");
            frog.Level = 20;
            frog.Stage = 1;
            loop.State.Drops.Shards = 50;
            loop.Camp.Add("herb_colossus_bloom", 1);
            loop.Camp.Add("fish_hills_roach", 6);
            Assert.That(loop.Companions.Evolve("flood_companion"), Is.EqualTo(CompanionResult.NoFavourite));
            loop.Camp.Add("fish_ice_char", 6);
            Assert.That(loop.Companions.Evolve("flood_companion"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Camp.Count("fish_hills_roach") + loop.Camp.Count("fish_ice_char"), Is.EqualTo(2));
        }

        [Test]
        public void TheAwakenedNeedsTheMuseumTrophyOfItsBiome()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Level = 30;
            dog.Stage = 2;
            loop.Camp.Add("herb_colossus_bloom", 3);
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.NoTrophy));
            loop.State.Museum.Done.Add("fish_hills");
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(CompanionView.Card(loop.Companions, "camp_dog").Stage, Is.EqualTo("awakened"));
            Assert.That(loop.Companions.Evolve("camp_dog"), Is.EqualTo(CompanionResult.LastStage));
        }

        [Test]
        public void TheOwnBiomeGivesAQuarterMoreLoot()
        {
            var home = Camp(5);
            var away = Camp(5);
            away.State.RecordStage = 100;
            home.State.RecordStage = 100;
            home.Expeditions.Send("camp_crow", "sand_ruins", 8);
            away.Expeditions.Send("camp_dog", "sand_ruins", 8);
            Assert.That(home.Companions.BiomeBonus("camp_crow", "sand_ruins"), Is.EqualTo(0.25));
            Assert.That(away.Companions.BiomeBonus("camp_dog", "sand_ruins"), Is.EqualTo(0));
            home.Expeditions.Advance(8 * Hour);
            away.Expeditions.Advance(8 * Hour);
            Assert.That(home.State.Expeditions.Reports[0].Items[0], Is.EqualTo(20), "2 per hour, 8 hours, +25%");
            Assert.That(away.State.Expeditions.Reports[0].Items[0], Is.EqualTo(24), "herbalist x1.5 without the biome bonus");
        }

        [Test]
        public void TheStageSurvivesTheSave()
        {
            var loop = Camp();
            loop.Companions.Progress("camp_crow").Stage = 2;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_crow"].Stage, Is.EqualTo(2));
        }
    }
}
