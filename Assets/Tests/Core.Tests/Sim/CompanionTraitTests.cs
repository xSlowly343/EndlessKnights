using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionTraitTests
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
        public void RanksGrowFromTripsAndStopAtTheStageCap()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            Assert.That(loop.Companions.RankOf("camp_dog", "herbalist"), Is.EqualTo(1));
            dog.TraitTrips["herbalist"] = 5;
            Assert.That(loop.Companions.RankOf("camp_dog", "herbalist"), Is.EqualTo(2));
            dog.TraitTrips["herbalist"] = 100;
            Assert.That(loop.Companions.RankOf("camp_dog", "herbalist"), Is.EqualTo(3), "the cub caps ranks at III");
            dog.Stage = 2;
            Assert.That(loop.Companions.RankOf("camp_dog", "herbalist"), Is.EqualTo(4));
            dog.Stage = 3;
            Assert.That(loop.Companions.RankOf("camp_dog", "herbalist"), Is.EqualTo(5));
            Assert.That(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items), Is.EqualTo(2.5));
        }

        [Test]
        public void ATripCountsForTheTravelTraits()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_crow", "green_hills", 1);
            loop.Expeditions.Advance(Hour);
            Assert.That(loop.Companions.Progress("camp_crow").TraitTrips["digger"], Is.EqualTo(1));
        }

        [Test]
        public void RankOneKeepsTheOldTraitNumbers()
        {
            var loop = Camp();
            Assert.That(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Items), Is.EqualTo(1.5));
            Assert.That(loop.Companions.TravelMultiplier("camp_crow", TravelStat.Shards), Is.EqualTo(2));
            loop.Companions.Grant("egg_griffin");
            var companions = loop.Companions;
            Assert.That(companions.TravelMultiplier("egg_griffin", TravelStat.Rare), Is.EqualTo(2 * (1 + companions.Temper("egg_griffin", CompanionStat.TripRare))).Within(1e-9));
            Assert.That(companions.TravelMultiplier("egg_griffin", TravelStat.Items),
                Is.EqualTo(1 + companions.Temper("egg_griffin", CompanionStat.TripLoot) + companions.Temper("egg_griffin", CompanionStat.TripItems)).Within(1e-9), "only its temper moves the numbers");
        }

        [Test]
        public void ASecondTraitIsChosenAfterTheYoungsterWithoutRepeats()
        {
            var loop = Camp();
            Assert.That(loop.Companions.ChooseTrait("camp_dog", "fisher"), Is.EqualTo(CompanionResult.NoSlot));
            loop.Companions.Progress("camp_dog").Stage = 1;
            Assert.That(loop.Companions.ChooseTrait("camp_dog", "herbalist"), Is.EqualTo(CompanionResult.Repeat));
            Assert.That(loop.Companions.ChooseTrait("camp_dog", "nonsense"), Is.EqualTo(CompanionResult.UnknownTrait));
            Assert.That(loop.Companions.TraitChoices("camp_dog"), Does.Not.Contain("herbalist"));
            Assert.That(loop.Companions.ChooseTrait("camp_dog", "digger"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.ChooseTrait("camp_dog", "fisher"), Is.EqualTo(CompanionResult.NoSlot));
            Assert.That(loop.Companions.TravelMultiplier("camp_dog", TravelStat.Shards), Is.EqualTo(2));
            loop.Companions.Progress("camp_dog").Stage = 3;
            Assert.That(loop.Companions.FreeTraitSlots("camp_dog"), Is.EqualTo(1));
        }

        [Test]
        public void ChangingATraitCostsShardsAndResetsItsRank()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 1;
            loop.Companions.ChooseTrait("camp_dog", "digger");
            dog.TraitTrips["digger"] = 40;
            Assert.That(loop.Companions.ChangeTrait("camp_dog", 0, "fisher"), Is.EqualTo(CompanionResult.NoShards));
            loop.State.Drops.Shards = 60;
            Assert.That(loop.Companions.ChangeTrait("camp_dog", 0, "fisher"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Drops.Shards, Is.EqualTo(10));
            Assert.That(dog.Traits, Is.EqualTo(new[] { "fisher" }));
            Assert.That(dog.TraitTrips.ContainsKey("digger"), Is.False);
            Assert.That(loop.Companions.RankOf("camp_dog", "fisher"), Is.EqualTo(1));
        }

        [Test]
        public void AHomeCompanionFeedsThePerkPoolAndCannotTravel()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 1;
            loop.Companions.ChooseTrait("camp_dog", "fisher");
            Assert.That(loop.State.Perk(MuseumPerk.BiteFaster), Is.EqualTo(0));
            Assert.That(loop.Companions.StayHome("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.BiteFaster], Is.EqualTo(0.05).Within(1e-9));
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 1), Is.EqualTo(ExpeditionResult.CompanionBusy));
            Assert.That(loop.Companions.StayHome("camp_crow"), Is.EqualTo(CompanionResult.NoHomeSlot));
            Assert.That(loop.Companions.LeaveHome("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.BiteFaster], Is.EqualTo(0));
            Assert.That(loop.Companions.LeaveHome("camp_dog"), Is.EqualTo(CompanionResult.NotHome));
        }

        [Test]
        public void ACompanionOnATripCannotStayHome()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_dog", "green_hills", 1);
            Assert.That(loop.Companions.StayHome("camp_dog"), Is.EqualTo(CompanionResult.Busy));
        }

        [Test]
        public void HomeTraitsRankUpWhileHomeAndTheAdultDoublesHalf()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 2;
            loop.Companions.ChooseTrait("camp_dog", "gardener");
            loop.Companions.StayHome("camp_dog");
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.GrowFaster], Is.EqualTo(0.075).Within(1e-9), "5% x1.5 at home for the adult");
            loop.Companions.Advance(5 * 8 * Hour);
            Assert.That(dog.TraitTrips["gardener"], Is.EqualTo(5));
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.GrowFaster], Is.EqualTo(0.075 * 1.5).Within(1e-9));
            Assert.That(dog.TraitTrips.ContainsKey("herbalist"), Is.False, "travel traits do not grow at home");
        }

        [Test]
        public void TraitsAndHomeSurviveTheSave()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Stage = 3;
            loop.Companions.ChooseTrait("camp_dog", "cook");
            loop.Companions.ChooseTrait("camp_dog", "wanderer");
            dog.TraitTrips["cook"] = 7;
            loop.Companions.StayHome("camp_dog");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Traits, Is.EqualTo(new[] { "cook", "wanderer" }));
            Assert.That(copy.Companions.Progress["camp_dog"].TraitTrips["cook"], Is.EqualTo(7));
            Assert.That(copy.Companions.Home, Is.EqualTo(new[] { "camp_dog" }));
            var reloaded = TestWorld.Loop(new FakeInputSource(), copy);
            Assert.That(reloaded.State.CompanionPerks[(int)MuseumPerk.MealLonger], Is.EqualTo(0.075 * 1.5).Within(1e-9));
        }
    }
}
