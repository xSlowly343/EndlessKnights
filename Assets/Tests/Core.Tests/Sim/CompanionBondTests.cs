using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionBondTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;
        private static CompanionBond Bond => TestWorld.Data.Companions.Bond;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void TenHeartsCostSixteenHundredFiftyPoints()
        {
            Assert.That(Bond.PointsTo(1), Is.EqualTo(30));
            Assert.That(Bond.PointsTo(10), Is.EqualTo(1650));
        }

        [Test]
        public void PettingGivesAPointOnceInTenMinutesAndSixTimesADay()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Pet("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Pet("camp_dog"), Is.EqualTo(CompanionResult.TooSoon));
            for (var i = 0; i < 5; i++)
            {
                loop.State.Tick += 6000;
                Assert.That(loop.Companions.Pet("camp_dog"), Is.EqualTo(CompanionResult.Done));
            }
            loop.State.Tick += 6000;
            Assert.That(loop.Companions.Pet("camp_dog"), Is.EqualTo(CompanionResult.Full));
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.EqualTo(6));
            loop.SetLocalDay(101);
            Assert.That(loop.Companions.Pet("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Pet("pet_owl"), Is.EqualTo(CompanionResult.UnknownCompanion));
        }

        [Test]
        public void TheFavouriteFoodGivesThreePointsOnceADayAndATripOne()
        {
            var loop = Camp();
            loop.State.Cooking.Meals.Clear();
            loop.State.Cooking.Meals.Add("herb_balls");
            loop.State.Cooking.Meals.Add("herb_balls");
            loop.Companions.Feed("camp_dog", "herb_balls");
            loop.Companions.Feed("camp_dog", "herb_balls");
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.EqualTo(3));
            loop.Expeditions.Send("camp_dog", "green_hills", 1);
            loop.Expeditions.Advance(Hour);
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.EqualTo(4));
        }

        [Test]
        public void FriendsPetUpToThreeTimesADay()
        {
            var loop = Camp();
            for (var i = 0; i < 3; i++)
                Assert.That(loop.Companions.FriendPet("camp_crow"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.FriendPet("camp_crow"), Is.EqualTo(CompanionResult.Full));
        }

        [Test]
        public void HeartsUnlockTheNameGesturesAccessoryAndBestFriend()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Rename("camp_dog", "Шарик"), Is.EqualTo(CompanionResult.Locked));
            loop.Events.Clear();
            loop.Companions.AddBond("camp_dog", 30);
            Assert.That(loop.Events.CountOf(GameEventKind.CompanionHeart), Is.EqualTo(1));
            Assert.That(loop.Companions.Rename("camp_dog", "  Шарик "), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.Rename("camp_dog", new string('а', 17)), Is.EqualTo(CompanionResult.BadName));
            Assert.That(loop.Companions.ChooseAccessory("camp_dog", "scarf"), Is.EqualTo(CompanionResult.Locked));
            loop.Companions.AddBond("camp_dog", Bond.PointsTo(7));
            Assert.That(loop.Companions.ChooseAccessory("camp_dog", "crown"), Is.EqualTo(CompanionResult.UnknownFood));
            Assert.That(loop.Companions.ChooseAccessory("camp_dog", "scarf"), Is.EqualTo(CompanionResult.Done));
            var card = CompanionView.Card(loop.Companions, "camp_dog");
            Assert.That(card.Name, Is.EqualTo("Шарик"));
            Assert.That(card.Gestures, Is.EqualTo(2));
            Assert.That(card.FireSeat, Is.True);
            Assert.That(card.BestFriend, Is.False);
            loop.Companions.AddBond("camp_dog", 10_000);
            Assert.That(loop.Companions.Hearts("camp_dog"), Is.EqualTo(10));
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.EqualTo(1650));
            Assert.That(CompanionView.Card(loop.Companions, "camp_dog").BestFriend, Is.True);
        }

        [Test]
        public void StoryLettersComeOneByOneAfterTripsOnceTheChapterIsOpen()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_dog", "green_hills", 1);
            loop.Expeditions.Advance(Hour);
            Assert.That(loop.Companions.StoryLetters("camp_dog"), Is.Empty, "one heart is not enough for chapter one");
            loop.Companions.AddBond("camp_dog", Bond.PointsTo(2));
            loop.Events.Clear();
            for (var i = 0; i < 4; i++)
            {
                loop.Expeditions.Send("camp_dog", "green_hills", 1);
                loop.Expeditions.Advance(Hour);
            }
            Assert.That(loop.Events.CountOf(GameEventKind.CompanionStory), Is.EqualTo(3));
            Assert.That(loop.Companions.StoryLetters("camp_dog").Count, Is.EqualTo(3));
            Assert.That(loop.Companions.StoryLetters("camp_dog")[0].Ru, Does.Contain("Разлома"));
        }

        [Test]
        public void TheBondSurvivesTheSave()
        {
            var loop = Camp();
            loop.Companions.AddBond("camp_dog", 100);
            loop.Companions.Rename("camp_dog", "Шарик");
            loop.Companions.Pet("camp_dog");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            var dog = copy.Companions.Progress["camp_dog"];
            Assert.That(dog.Bond, Is.EqualTo(101));
            Assert.That(dog.Name, Is.EqualTo("Шарик"));
            Assert.That(dog.PetsToday, Is.EqualTo(1));
        }
    }
}
