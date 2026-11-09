using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionCircleTests
    {
        private static GameLoop Awake()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            loop.State.RecordStage = 100;
            var companions = loop.Companions;
            companions.Progress("camp_dog").Stage = TestWorld.Data.Companions.Stages.Count - 1;
            return loop;
        }

        private static void Trip(GameLoop loop, string companion, string biome)
        {
            Assert.That(loop.Expeditions.Send(companion, biome, 1), Is.EqualTo(ExpeditionResult.Done));
            var trip = loop.State.Expeditions.Active[loop.State.Expeditions.Active.Count - 1];
            trip.Elapsed = trip.Planned;
            loop.Companions.TripDone(trip);
            loop.State.Expeditions.Active.Remove(trip);
        }

        [Test]
        public void FifteenCirclesCostAboutTwoThousandTwoHundredEssence()
        {
            var circles = TestWorld.Data.Companions.Circles.Circles;
            Assert.That(circles.Count, Is.EqualTo(15));
            var total = 0L;
            foreach (var circle in circles)
                total += circle.Essence;
            Assert.That(total, Is.InRange(2000, 2400));
        }

        [Test]
        public void OnlyAnAwakenedCompanionWalksTheCircles()
        {
            var loop = Awake();
            Assert.That(loop.Companions.AdvanceCircle("camp_crow"), Is.EqualTo(CompanionResult.NotAwakened));
            Assert.That(loop.Companions.NextCircle("camp_dog")!.Trial, Is.EqualTo(CircleTrial.OwnBiomeTrips));
        }

        [Test]
        public void TheFirstCircleNeedsTenTripsHomeAndTenEssence()
        {
            var loop = Awake();
            var companions = loop.Companions;
            for (var i = 0; i < 9; i++)
                Trip(loop, "camp_dog", "green_hills");
            Trip(loop, "camp_dog", "sand_ruins");
            Assert.That(companions.CircleProgress("camp_dog"), Is.EqualTo(9), "trips to another biome do not count");
            Trip(loop, "camp_dog", "green_hills");
            Assert.That(companions.AdvanceCircle("camp_dog"), Is.EqualTo(CompanionResult.NoEssence));
            loop.State.Companions.Essence = 10;
            var factor = companions.TraitFactor("camp_dog");
            Assert.That(companions.AdvanceCircle("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(companions.CircleOf("camp_dog"), Is.EqualTo(1));
            Assert.That(loop.State.Companions.Essence, Is.EqualTo(0));
            Assert.That(companions.TraitFactor("camp_dog"), Is.EqualTo(factor * 1.05).Within(1e-9));
            Assert.That(companions.CircleProgress("camp_dog"), Is.EqualTo(0), "the next trial starts from zero");
        }

        [Test]
        public void FavouriteDaysMustFollowEachOther()
        {
            var loop = Awake();
            var companions = loop.Companions;
            companions.Progress("camp_dog").Circle = 1;
            var food = TestWorld.Data.Companions.Companion("camp_dog")!.Favourite;
            loop.State.Cooking.Meals.Clear();
            for (var day = 100; day < 103; day++)
            {
                loop.SetLocalDay(day);
                loop.State.Cooking.Meals.Add(food);
                Assert.That(companions.Feed("camp_dog", food), Is.EqualTo(CompanionResult.Done));
            }
            Assert.That(companions.CircleProgress("camp_dog"), Is.EqualTo(3));
            loop.SetLocalDay(105);
            loop.State.Cooking.Meals.Add(food);
            companions.Feed("camp_dog", food);
            Assert.That(companions.CircleProgress("camp_dog"), Is.EqualTo(1), "a missed day restarts the streak");
        }

        [Test]
        public void TheHostOfItsBiomeCountsAndGivesEssenceOnceADay()
        {
            var loop = Awake();
            var companions = loop.Companions;
            companions.Progress("camp_dog").Circle = 6;
            loop.Strip.Show("camp_dog");
            companions.HostDefeated("green_hills");
            companions.HostDefeated("green_hills");
            companions.HostDefeated("sand_ruins");
            Assert.That(companions.CircleProgress("camp_dog"), Is.EqualTo(2));
            Assert.That(loop.State.Companions.Essence, Is.EqualTo(TestWorld.Data.Companions.Circles.HostEssence));
        }

        [Test]
        public void LaterCirclesOpenLevelsAboveSixty()
        {
            var loop = Awake();
            var companions = loop.Companions;
            companions.Progress("camp_dog").Stars = TestWorld.Data.Companions.Stars.MaxStars;
            var cap = companions.MaxLevelOf("camp_dog");
            companions.Progress("camp_dog").Circle = 15;
            Assert.That(companions.MaxLevelOf("camp_dog"), Is.EqualTo(cap + 20));
            Assert.That(companions.Unlocked("camp_dog", "hall"), Is.True);
            Assert.That(companions.AdvanceCircle("camp_dog"), Is.EqualTo(CompanionResult.LastStage));
        }

        [Test]
        public void TheBurrowPaysEssenceByFloor()
        {
            var rules = TestWorld.Data.Companions.Circles;
            Assert.That(rules.BurrowEssence(0), Is.EqualTo(0));
            Assert.That(rules.BurrowEssence(2), Is.EqualTo(rules.BurrowFloorEssence[0] + rules.BurrowFloorEssence[1]));
        }

        [Test]
        public void CirclesAndEssenceSurviveASave()
        {
            var loop = Awake();
            loop.Companions.Progress("camp_dog").Circle = 4;
            loop.Companions.Progress("camp_dog").CircleCount = 2;
            loop.State.Companions.Essence = 77;
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Circle, Is.EqualTo(4));
            Assert.That(copy.Companions.Progress["camp_dog"].CircleCount, Is.EqualTo(2));
            Assert.That(copy.Companions.Essence, Is.EqualTo(77));
        }
    }
}
