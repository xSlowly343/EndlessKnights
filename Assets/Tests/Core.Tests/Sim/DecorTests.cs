using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DecorTests
    {
        private static DecorRules Rules => TestWorld.Data.Decor;

        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop();
            loop.Tick();
            return loop;
        }

        [Test]
        public void StarterDecorGoesOnTheStreetAndSpotsGrowWithTheTier()
        {
            var loop = Camp();
            Assert.That(loop.Decor.Spots, Is.EqualTo(Rules.SpotsOf("camp_site")));
            Assert.That(loop.Decor.Place(0, "decor_lantern"), Is.EqualTo(DecorResult.Done));
            Assert.That(loop.Decor.Place(1, "decor_brazier"), Is.EqualTo(DecorResult.NotOwned));
            Assert.That(loop.Decor.Place(Rules.SpotsOf("camp_site"), "decor_bench"), Is.EqualTo(DecorResult.NoSpot));
            Assert.That(loop.Decor.Place(2, "decor_lantern"), Is.EqualTo(DecorResult.Done), "moving keeps one of each");
            Assert.That(DecorView.Street(loop.Decor).Select(s => s.Decor), Is.EqualTo(new[] { "", "", "decor_lantern", "" }));
            loop.State.RecordStage = 101;
            loop.Tick();
            Assert.That(loop.Decor.Spots, Is.EqualTo(Rules.SpotsOf("outpost")));
        }

        [Test]
        public void DecorIsMadeFromHarvestButNeverFromOre()
        {
            var loop = Camp();
            Assert.That(loop.Decor.Craft("decor_brazier"), Is.EqualTo(DecorResult.NoItems));
            loop.Camp.Add("ember_coal", 8);
            Assert.That(DecorView.Items(loop.Decor).Single(i => i.Id == "decor_brazier").CanCraft, Is.True);
            Assert.That(loop.Decor.Craft("decor_brazier"), Is.EqualTo(DecorResult.Done));
            Assert.That(loop.Camp.Count("ember_coal"), Is.Zero);
            Assert.That(loop.Decor.Craft("decor_brazier"), Is.EqualTo(DecorResult.AlreadyOwned));
            Assert.That(loop.Decor.Place(1, "decor_brazier"), Is.EqualTo(DecorResult.Done));
            var json = Content.TestData.ReadGameDataFile(DecorRules.FileName).Replace(@"""ember_coal"": 8", @"""ore_ember"": 8");
            var error = Assert.Throws<ContentException>(() => DecorRules.Load(json, TestWorld.Data.Camp))!;
            Assert.That(error.Message, Does.Contain("ore is only for the forge"));
        }

        [Test]
        public void TheThemeIsAnyReachedBiome()
        {
            var loop = Camp();
            Assert.That(loop.Decor.SetTheme("ice_peak"), Is.EqualTo(DecorResult.BiomeClosed));
            loop.State.RecordStage = 120;
            Assert.That(loop.Decor.SetTheme("ice_peak"), Is.EqualTo(DecorResult.Done));
            Assert.That(loop.State.Decor.Theme, Is.EqualTo("ice_peak"));
        }

        [Test]
        public void MonumentsGrowWithTheInputOfAllTime()
        {
            var loop = Camp();
            Assert.That(DecorView.Monuments(loop.Decor).All(m => m.Level == 0), Is.True);
            loop.State.TotalInput.MouseMeters = 42_500;
            loop.State.TotalInput.Keys = 150_000;
            var monuments = DecorView.Monuments(loop.Decor);
            Assert.That(monuments.Single(m => m.Id == "milestone").Level, Is.EqualTo(3), "a marathon of mouse");
            Assert.That(monuments.Single(m => m.Id == "milestone").Next, Is.EqualTo(100));
            Assert.That(monuments.Single(m => m.Id == "anvil").Level, Is.EqualTo(2));
        }

        [Test]
        public void GuestsWaterABedAndStrikeTheAnvil()
        {
            var loop = Camp();
            loop.Garden.Plant(0, "knockbloom");
            Assert.That(loop.Decor.Guest(GuestAct.Water, 5), Is.True, "a wrong bed waters the first growing one");
            Assert.That(loop.State.Garden.Beds[0].Grown, Is.GreaterThanOrEqualTo(Rules.WaterSeconds));
            loop.State.RecordStage = 51;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "smiths" });
            Assert.That(loop.Decor.Guest(GuestAct.Hammer, -1), Is.True);
            loop.Camp.Add("ore_hills", 10);
            loop.Forge.TemperWeapon();
            Assert.That(loop.State.Forge.Hammers, Is.EqualTo(Rules.GuestHammers), "the strike waits for the next work");
            loop.Decor.Guest(GuestAct.Hammer, -1);
            Assert.That(loop.State.Forge.Hammers, Is.EqualTo(2 * Rules.GuestHammers));
            Assert.That(loop.Decor.Guest(GuestAct.Gift, -1), Is.True);
            Assert.That(loop.State.Decor.Gifts, Is.EqualTo(1));
        }

        [Test]
        public void TheSnapshotShowsTheCampToGuestsAndTheDecorSurvivesTheSave()
        {
            var loop = Camp();
            loop.Decor.Place(1, "decor_bench");
            foreach (var fish in TestWorld.Data.Museum.Collection("fish_hills")!.Items)
                loop.State.Fishing.Album[fish] = new FishRecord { Count = 1 };
            loop.Museum.Check();
            loop.Museum.Place("fish_hills");
            var snapshot = DecorView.Snapshot(loop.Decor, loop.Museum, loop.State);
            Assert.That(snapshot.Decor, Is.EqualTo(new[] { "", "decor_bench", "", "" }));
            Assert.That(snapshot.Trophies, Is.EqualTo(new[] { "trophy_hills_pike" }));
            Assert.That(snapshot.Monuments.Count, Is.EqualTo(Rules.Monuments.Count));
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(JsonWriter.Write(GameStateJson.Write(loop.State))));
            Assert.That(copy.Decor.Placed[1], Is.EqualTo("decor_bench"));
        }

        [TestCase(@"""stronghold"": 16", @"""fortress"": 16", "camp tier 'stronghold' has no decor spots")]
        [TestCase(@"""input"": ""clicks""", @"""input"": ""blinks""", "unknown monument input 'blinks'")]
        [TestCase(@"[10000, 100000, 1000000, 5000000]", @"[10000, 1000, 1000000, 5000000]", "monument steps must grow")]
        public void BrokenDecorIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(DecorRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => DecorRules.Load(json, TestWorld.Data.Camp))!;
            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
