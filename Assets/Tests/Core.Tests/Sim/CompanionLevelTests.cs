using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionLevelTests
    {
        private static CompanionRules Rules => TestWorld.Data.Companions;
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void EveryCompanionInTheGameHasABiomeAndAFavouriteFood()
        {
            Assert.That(Rules.Companion("camp_dog"), Is.Not.Null);
            Assert.That(Rules.Companion("camp_crow"), Is.Not.Null);
            foreach (var cosmetic in TestWorld.Data.Cosmetics.All)
            {
                if (cosmetic.Slot == ExpeditionSystem.CompanionSlot)
                    Assert.That(Rules.Companion(cosmetic.Id), Is.Not.Null, cosmetic.Id);
            }
        }

        [Test]
        public void ThirtyLevelsCostAboutNineThousandExperience()
        {
            Assert.That(Rules.MaxLevel, Is.EqualTo(30));
            Assert.That(Rules.ExperienceFor(2), Is.EqualTo(40));
            Assert.That(Rules.ExperienceTo(30), Is.EqualTo(9280));
        }

        [Test]
        public void ATripGivesTenExperiencePerPlannedHourAndLevelsUp()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            loop.Events.Clear();
            loop.Expeditions.Advance(8 * Hour);
            var dog = loop.Companions.Progress("camp_dog");
            Assert.That(dog.Level, Is.EqualTo(2));
            Assert.That(dog.Experience, Is.EqualTo(40).Within(1e-6));
            Assert.That(loop.Events.CountOf(GameEventKind.CompanionLevelUp), Is.EqualTo(1));
        }

        [Test]
        public void ShorterTripsDoNotCutExperience()
        {
            var loop = Camp();
            loop.State.CampPerks[(int)MuseumPerk.ShorterTrips] = 0.3;
            loop.Expeditions.Send("camp_dog", "green_hills", 4);
            loop.Expeditions.Advance(4 * Hour);
            Assert.That(loop.State.Expeditions.Active, Is.Empty);
            Assert.That(loop.Companions.LevelOf("camp_dog"), Is.EqualTo(2));
            Assert.That(loop.Companions.Progress("camp_dog").Experience, Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void FoodHerbFishAndDishWithFavouriteDoubled()
        {
            var loop = Camp();
            loop.Camp.Add("herb_windwort", 2);
            loop.Camp.Add("fish_hills_roach", 1);
            loop.State.Cooking.Meals.Add("carp_pie");
            loop.State.Cooking.Meals.Add("herb_balls");
            Assert.That(loop.Companions.Feed("camp_dog", "herb_windwort"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Progress("camp_dog").Experience, Is.EqualTo(2));
            loop.Companions.Feed("camp_dog", "fish_hills_roach");
            loop.Companions.Feed("camp_dog", "carp_pie");
            loop.Companions.Feed("camp_dog", "herb_balls");
            Assert.That(loop.Companions.Progress("camp_dog").FedToday, Is.EqualTo(2 + 5 + 15 + 30));
            Assert.That(loop.State.Cooking.Meals, Is.Empty);
            Assert.That(loop.Companions.Feed("camp_dog", "fish_hills_roach"), Is.EqualTo(CompanionResult.NoFood));
            Assert.That(loop.Companions.Feed("camp_dog", "ruin_shard"), Is.EqualTo(CompanionResult.UnknownFood));
            Assert.That(loop.Companions.Feed("pet_owl", "herb_windwort"), Is.EqualTo(CompanionResult.UnknownCompanion));
        }

        [Test]
        public void AFoodKindCanBeTheFavourite()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("flood_companion");
            loop.Camp.Add("fish_hills_roach", 1);
            loop.Companions.Feed("flood_companion", "fish_hills_roach");
            Assert.That(loop.Companions.Progress("flood_companion").FedToday, Is.EqualTo(10));
        }

        [Test]
        public void FoodIsCappedPerDayAndTheCapResetsNextDay()
        {
            var loop = Camp();
            loop.State.Cooking.Meals.Clear();
            for (var i = 0; i < 8; i++)
                loop.State.Cooking.Meals.Add("herb_balls");
            for (var i = 0; i < 7; i++)
                loop.Companions.Feed("camp_dog", "herb_balls");
            Assert.That(loop.Companions.FoodLeft("camp_dog"), Is.EqualTo(0));
            Assert.That(loop.Companions.Feed("camp_dog", "herb_balls"), Is.EqualTo(CompanionResult.Full));
            Assert.That(loop.State.Cooking.Meals.Count, Is.EqualTo(1));
            loop.SetLocalDay(101);
            Assert.That(loop.Companions.FoodLeft("camp_dog"), Is.EqualTo(200));
        }

        [Test]
        public void TheFavouriteIsShownAfterTheFirstMeal()
        {
            var loop = Camp();
            Assert.That(CompanionView.Card(loop.Companions, "camp_dog").Favourite, Is.Empty);
            loop.Camp.Add("herb_windwort", 1);
            loop.Companions.Feed("camp_dog", "herb_windwort");
            var card = CompanionView.Card(loop.Companions, "camp_dog");
            Assert.That(card.Favourite, Is.EqualTo("herb_balls"));
            Assert.That(card.Biome, Is.EqualTo("green_hills"));
            Assert.That(card.Need, Is.EqualTo(40));
        }

        [Test]
        public void LevelsGiveMoreLootAndShorterTrips()
        {
            var low = Camp(7);
            var high = Camp(7);
            high.Companions.Progress("camp_dog").Level = 30;
            Assert.That(high.Companions.LootBonus("camp_dog"), Is.EqualTo(0.58).Within(1e-9));
            Assert.That(high.Companions.ShorterCut("camp_dog"), Is.EqualTo(0.12).Within(1e-9));
            low.Expeditions.Send("camp_dog", "green_hills", 8);
            high.Expeditions.Send("camp_dog", "green_hills", 8);
            Assert.That(high.State.Expeditions.Active[0].Seconds, Is.EqualTo(8 * Hour * 0.88).Within(1e-6));
            low.Expeditions.Advance(8 * Hour);
            high.Expeditions.Advance(8 * Hour);
            Assert.That(high.State.Expeditions.Reports[0].Items[0], Is.GreaterThan(low.State.Expeditions.Reports[0].Items[0]));
            Assert.That(high.State.Expeditions.Reports[0].Shards, Is.GreaterThan(low.State.Expeditions.Reports[0].Shards));
        }

        [Test]
        public void TheShorterCutStopsAtFortyPercentWithPerks()
        {
            var loop = Camp();
            loop.Companions.Progress("camp_dog").Level = 30;
            loop.State.CampPerks[(int)MuseumPerk.ShorterTrips] = 0.35;
            Assert.That(loop.Expeditions.DurationFactor("camp_dog"), Is.EqualTo(0.6).Within(1e-9));
            loop.State.CampPerks[(int)MuseumPerk.ShorterTrips] = 0.5;
            Assert.That(loop.Expeditions.DurationFactor("camp_dog"), Is.EqualTo(0.5).Within(1e-9), "perks alone keep their old cap");
        }

        [Test]
        public void LevelStopsAtThirty()
        {
            var loop = Camp();
            loop.Companions.Gain("camp_dog", 1_000_000);
            Assert.That(loop.Companions.LevelOf("camp_dog"), Is.EqualTo(30));
            Assert.That(loop.Companions.Progress("camp_dog").Experience, Is.EqualTo(0));
        }

        [Test]
        public void ProgressSurvivesTheSave()
        {
            var loop = Camp();
            loop.Camp.Add("herb_windwort", 1);
            loop.Companions.Feed("camp_dog", "herb_windwort");
            loop.Companions.Gain("camp_crow", 100);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_crow"].Level, Is.EqualTo(3));
            Assert.That(copy.Companions.Progress["camp_dog"].FavouriteKnown, Is.True);
            Assert.That(copy.Companions.Day, Is.EqualTo(100));
            Assert.That(Game.Core.Content.Json.JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(Game.Core.Content.Json.JsonWriter.Write(GameStateJson.Write(loop.State))));
        }
    }
}
