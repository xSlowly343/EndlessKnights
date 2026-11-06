using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MuseumTests
    {
        private static MuseumRules Rules => TestWorld.Data.Museum;

        private static void Catch(GameLoop loop, params string[] fish)
        {
            foreach (var id in fish)
                loop.State.Fishing.Album[id] = new FishRecord { Count = 1 };
        }

        [Test]
        public void ACompleteCollectionGivesItsSmallBonusOnce()
        {
            var loop = TestWorld.Loop();
            loop.Tick();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            var forge = Rules.Collection("fish_forge")!;
            Catch(loop, forge.Items.Take(2).ToArray());
            loop.Museum.Check();
            Assert.That(loop.Museum.Complete("fish_forge"), Is.False);
            Assert.That(MuseumView.Collections(loop.Museum).Single(c => c.Id == "fish_forge").Collected, Is.EqualTo(2));
            Catch(loop, forge.Items.ToArray());
            loop.Events.Clear();
            for (var i = 0; i < 10; i++)
                loop.Tick();
            Assert.That(loop.Museum.Complete("fish_forge"), Is.True, "the museum notices within a second");
            Assert.That(loop.Events.CountOf(GameEventKind.CollectionCompleted), Is.EqualTo(1));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.01).Within(0.003));
            loop.Museum.Check();
            Assert.That(loop.Events.CountOf(GameEventKind.CollectionCompleted), Is.EqualTo(1));
        }

        [Test]
        public void EveryActivityFillsItsOwnCollection()
        {
            var loop = TestWorld.Loop();
            foreach (var plant in Rules.Collection("herbarium")!.Items)
                loop.State.Garden.Album.Add(plant);
            foreach (var dish in Rules.Collection("cookbook")!.Items)
                loop.State.Cooking.Book.Add(dish);
            foreach (var recipe in Rules.Collection("alchemy_book")!.Items)
                loop.State.Alchemy.Known.Add(recipe);
            foreach (var find in Rules.Collection("travel_finds")!.Items)
                loop.State.Expeditions.Finds.Add(find);
            loop.Museum.Check();
            Assert.That(loop.State.Museum.Done, Is.EquivalentTo(new[] { "herbarium", "cookbook", "alchemy_book", "travel_finds" }));
        }

        [Test]
        public void AllCollectionsTogetherStayUnderFivePercent()
        {
            var total = Rules.Collections.SelectMany(c => c.Bonus).Sum(m => m.Value);
            Assert.That(total, Is.LessThanOrEqualTo(Rules.MaxTotal + 1e-12));
            Assert.That(Rules.Collections.SelectMany(c => c.Bonus).All(m => m.Value <= Rules.MaxEach), Is.True);
            var greedy = Content.TestData.ReadGameDataFile(MuseumRules.FileName).Replace("\"maxTotal\": 0.15", "\"maxTotal\": 0.1");
            var error = Assert.Throws<ContentException>(() => Load(greedy))!;
            Assert.That(error.Message, Does.Contain("all collections give at most 0.1"));
        }

        [Test]
        public void TrophiesGoOnTheShelfAndTitlesCanBeWorn()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.Museum.Place("legends"), Is.EqualTo(MuseumResult.NotCollected));
            Catch(loop, Rules.Collections.Where(c => c.Kind == CollectionKind.Fish).SelectMany(c => c.Items).ToArray());
            loop.Museum.Check();
            Assert.That(loop.Museum.SetTitle("fish_hills"), Is.EqualTo(MuseumResult.NoTitle));
            Assert.That(loop.Museum.SetTitle("legends"), Is.EqualTo(MuseumResult.Done));
            Assert.That(MuseumView.Title(loop.Museum), Is.EqualTo("Легенда водоёмов"));
            Assert.That(loop.Museum.SetTitle("cookbook"), Is.EqualTo(MuseumResult.NotCollected));
            foreach (var id in new[] { "fish_hills", "fish_ruins", "fish_ice", "fish_forge", "fish_sky" })
                Assert.That(loop.Museum.Place(id), Is.EqualTo(MuseumResult.Done));
            Assert.That(loop.Museum.Place("legends"), Is.EqualTo(MuseumResult.ShelfFull));
            loop.Museum.Remove("fish_ice");
            Assert.That(loop.Museum.Place("legends"), Is.EqualTo(MuseumResult.Done));
            Assert.That(MuseumView.Collections(loop.Museum).Count(c => c.OnShelf), Is.EqualTo(Rules.Shelf));
        }

        [Test]
        public void CollectionsAlsoGivePeacefulChances()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.State.Museum.Perk(MuseumPerk.BiteFaster), Is.Zero);
            Catch(loop, Rules.Collection("fish_hills")!.Items.ToArray());
            foreach (var plant in Rules.Collection("herbarium")!.Items)
                loop.State.Garden.Album.Add(plant);
            loop.Museum.Check();
            Assert.That(loop.State.Museum.Perk(MuseumPerk.BiteFaster), Is.EqualTo(0.1));
            Assert.That(loop.State.Museum.Perk(MuseumPerk.MutationChance), Is.EqualTo(0.05));
            loop.State.RecordStage = 51;
            loop.Camp.SetGuilds(new[] { "fishers" });
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(FishingSystem.Activity);
            loop.Tick();
            loop.Tick();
            Assert.That(loop.State.Fishing.Timer, Is.LessThanOrEqualTo(TestWorld.Data.Fishing.BiteMaxSeconds * 0.9 + 1e-9), "the float bites sooner");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            TestWorld.Loop(new FakeInputSource(), copy);
            Assert.That(copy.Museum.Perk(MuseumPerk.BiteFaster), Is.EqualTo(0.1), "perks come back after loading");
        }

        [Test]
        public void TheMuseumSurvivesTheSaveWithItsBonus()
        {
            var loop = TestWorld.Loop();
            Catch(loop, Rules.Collection("fish_forge")!.Items.ToArray());
            loop.Museum.Check();
            loop.Museum.Place("fish_forge");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(JsonWriter.Write(GameStateJson.Write(loop.State))));
            var restored = TestWorld.Loop(new FakeInputSource(), copy);
            Assert.That(restored.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(loop.Hero.Value(CombatStats.Damage, 1)).Within(1e-9));
        }

        [Test]
        public void MuseumChecksDoNotAllocate()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            Catch(loop, Rules.Collection("fish_hills")!.Items.Take(2).ToArray());
            for (var i = 0; i < 200; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [TestCase(@"""kind"": ""finds""", @"""kind"": ""stamps""", "unknown collection kind 'stamps'")]
        [TestCase(@"""fish_hills_roach"", ""fish_hills_carp""", @"""fish_hills_goldfish"", ""fish_hills_carp""", "'fish_hills_goldfish' is not a known fish entry")]
        [TestCase(@"""maxTotal"": 0.15", @"""maxTotal"": 0.2", "0.2")]
        [TestCase(@"""perk"": ""biteFaster""", @"""perk"": ""luck""", "unknown museum perk 'luck'")]
        public void BrokenMuseumIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(MuseumRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => Load(json))!;
            Assert.That(error.Message, Does.Contain(message));
        }

        private static MuseumRules Load(string json)
        {
            var data = TestWorld.Data;
            return MuseumRules.Load(json, data.Fishing, data.Garden, data.Cooking, data.Alchemy, data.Expeditions);
        }
    }
}
