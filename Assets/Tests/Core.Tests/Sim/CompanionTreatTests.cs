using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionTreatTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void CookingTakesTheIngredientsFromTheCamp()
        {
            var loop = Camp();
            loop.Camp.Add("fish_ice_crucian", 1);
            Assert.That(loop.Companions.Cook("dried_crucian"), Is.EqualTo(CompanionResult.NoIngredients), "the recipe needs two crucians");
            loop.Camp.Add("fish_ice_crucian", 2);
            Assert.That(loop.Companions.Cook("dried_crucian"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Camp.Count("fish_ice_crucian"), Is.EqualTo(1));
            Assert.That(loop.Camp.Count("treat_dried_crucian"), Is.EqualTo(1));
            Assert.That(loop.Companions.Cook("cake"), Is.EqualTo(CompanionResult.UnknownFood));
        }

        [Test]
        public void ATreatGivesExperienceAndGoesOnTheNextTrip()
        {
            var loop = Camp();
            loop.Camp.Add("treat_roach_crisps", 1);
            Assert.That(loop.Companions.Feed("camp_dog", "treat_roach_crisps"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Camp.Count("treat_roach_crisps"), Is.Zero);
            Assert.That(loop.Companions.Progress("camp_dog").Treat, Is.EqualTo("roach_crisps"));
            Assert.That(loop.Companions.Progress("camp_dog").Experience, Is.GreaterThan(0));
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            var trip = loop.State.Expeditions.Active[0];
            Assert.That(trip.Treat, Is.EqualTo("roach_crisps"));
            Assert.That(loop.Companions.Progress("camp_dog").Treat, Is.Empty, "the treat is eaten on the road");
            Assert.That(loop.Companions.StatOf(trip, TravelStat.Items),
                Is.EqualTo(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items) * 1.1).Within(1e-9));
            Assert.That(loop.Companions.StatOf(trip, TravelStat.Shards),
                Is.EqualTo(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Shards)).Within(1e-9));
        }

        [Test]
        public void ADurationTreatShortensTheTrip()
        {
            var plain = Camp();
            plain.Expeditions.Send("camp_dog", "green_hills", 8);
            var loop = Camp();
            loop.Camp.Add("treat_ember_minnow_bites", 1);
            loop.Companions.Feed("camp_dog", "treat_ember_minnow_bites");
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            Assert.That(loop.State.Expeditions.Active[0].Seconds, Is.EqualTo(plain.State.Expeditions.Active[0].Seconds * 0.95).Within(1e-6));
        }

        [Test]
        public void ADelicacyGivesALevelOnceAWeekUpToThirty()
        {
            var loop = Camp();
            loop.Camp.Add("treat_bloom_delicacy", 3);
            Assert.That(loop.Companions.Feed("camp_dog", "treat_bloom_delicacy"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.LevelOf("camp_dog"), Is.EqualTo(2));
            Assert.That(loop.Companions.Feed("camp_dog", "treat_bloom_delicacy"), Is.EqualTo(CompanionResult.TooSoon));
            loop.SetLocalDay(107);
            loop.Companions.Progress("camp_dog").Level = 30;
            Assert.That(loop.Companions.Feed("camp_dog", "treat_bloom_delicacy"), Is.EqualTo(CompanionResult.Full));
            loop.Companions.Progress("camp_dog").Level = 29;
            Assert.That(loop.Companions.Feed("camp_dog", "treat_bloom_delicacy"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.LevelOf("camp_dog"), Is.EqualTo(30));
        }
    }
}
