using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionPackTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            loop.State.RecordStage = 60;
            return loop;
        }

        private static GameLoop HillsPack()
        {
            var loop = Camp();
            loop.Companions.Grant("egg_boarlet");
            loop.Companions.Grant("hills_piglet");
            Assert.That(loop.Expeditions.SendPack("camp_dog", "egg_boarlet", "hills_piglet", "green_hills", 4), Is.EqualTo(ExpeditionResult.Done));
            return loop;
        }

        [Test]
        public void APackTakesThreeTripSlotsAndKeepsAllThreeBusy()
        {
            var loop = HillsPack();
            Assert.That(loop.Expeditions.UsedSlots(), Is.EqualTo(3));
            Assert.That(loop.Expeditions.Busy("hills_piglet"), Is.True);
            Assert.That(loop.Expeditions.Send("camp_crow", "green_hills", 1), Is.EqualTo(ExpeditionResult.NoTripSlot));
            Assert.That(loop.Expeditions.SendPack("camp_crow", "camp_crow", "pet_fox", "green_hills", 1), Is.EqualTo(ExpeditionResult.UnknownCompanion));
        }

        [Test]
        public void APackNeedsThreeFreeSlots()
        {
            var loop = Camp();
            loop.Companions.Grant("egg_boarlet");
            loop.Companions.Grant("hills_piglet");
            loop.Expeditions.Send("camp_crow", "green_hills", 1);
            Assert.That(loop.Expeditions.SendPack("camp_dog", "egg_boarlet", "hills_piglet", "green_hills", 4), Is.EqualTo(ExpeditionResult.NoTripSlot));
        }

        [Test]
        public void AllThreeTraitsWorkAndOneFamilyBringsMore()
        {
            var loop = HillsPack();
            var trip = loop.State.Expeditions.Active[0];
            var companions = loop.Companions;
            Assert.That(companions.OneFamily(trip), Is.True);
            var sum = companions.TravelMultiplier("camp_dog", TravelStat.Items) + companions.TravelMultiplier("egg_boarlet", TravelStat.Items)
                + companions.TravelMultiplier("hills_piglet", TravelStat.Items) - 2;
            var pack = companions.Rules.Pack;
            var expected = sum * pack.Loot * (1 + pack.FamilyBonus) * (1 + companions.Temper("camp_dog", CompanionStat.JointLoot) + companions.Temper("egg_boarlet", CompanionStat.JointLoot)
                + companions.Temper("hills_piglet", CompanionStat.JointLoot));
            Assert.That(companions.StatOf(trip, TravelStat.Items), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void EveryoneInThePackLearnsFromTheTrip()
        {
            var loop = HillsPack();
            var trip = loop.State.Expeditions.Active[0];
            trip.Elapsed = trip.Planned;
            loop.Companions.TripDone(trip);
            Assert.That(loop.Companions.Progress("hills_piglet").Trips, Is.EqualTo(1));
            Assert.That(loop.Companions.Progress("egg_boarlet").Trips, Is.EqualTo(1));
        }

        [Test]
        public void AMentorWithAStarPassesAShareOfTripExperience()
        {
            var loop = Camp();
            var companions = loop.Companions;
            Assert.That(companions.TakeStudent("camp_dog", "camp_crow"), Is.EqualTo(CompanionResult.Locked));
            companions.Progress("camp_dog").Stars = 1;
            Assert.That(companions.TakeStudent("camp_dog", "camp_crow"), Is.EqualTo(CompanionResult.Done));
            Assert.That(companions.MentorOf("camp_crow"), Is.EqualTo("camp_dog"));
            companions.Grant("jackal_pup");
            companions.Progress("jackal_pup").Stars = 1;
            Assert.That(companions.TakeStudent("jackal_pup", "camp_crow"), Is.EqualTo(CompanionResult.Busy));
            loop.Expeditions.Send("camp_dog", "green_hills", 4);
            var trip = loop.State.Expeditions.Active[0];
            trip.Elapsed = trip.Planned;
            var before = companions.Progress("camp_crow").Experience;
            companions.TripDone(trip);
            Assert.That(companions.Progress("camp_crow").Experience, Is.GreaterThan(before));
        }

        [Test]
        public void ALessonEndsWhenTheStudentGrowsUp()
        {
            var loop = Camp();
            var companions = loop.Companions;
            companions.Progress("camp_dog").Stars = 1;
            companions.TakeStudent("camp_dog", "camp_crow");
            companions.Progress("camp_crow").Level = 20;
            loop.Expeditions.Send("camp_dog", "green_hills", 1);
            var trip = loop.State.Expeditions.Active[0];
            trip.Elapsed = trip.Planned;
            companions.TripDone(trip);
            Assert.That(companions.MentorOf("camp_crow"), Is.Empty);
            Assert.That(companions.TakeStudent("camp_dog", "camp_crow"), Is.EqualTo(CompanionResult.LowLevel));
        }

        [Test]
        public void SparksOfAFullStarCompanionGoToItsStudentOrTheLowestStar()
        {
            var loop = Camp();
            var companions = loop.Companions;
            companions.Grant("jackal_pup");
            companions.Grant("egg_scarab");
            companions.Progress("jackal_pup").Stars = companions.Rules.Stars.MaxStars;
            companions.Progress("egg_scarab").Stars = 2;
            Assert.That(companions.SparkHeir("egg_scarab"), Is.EqualTo("egg_scarab"));
            Assert.That(companions.SparkHeir("jackal_pup"), Is.EqualTo("egg_scarab"));
            companions.Progress("jackal_pup").Student = "camp_crow";
            Assert.That(companions.SparkHeir("jackal_pup"), Is.EqualTo("camp_crow"));
        }

        [Test]
        public void ThePackAndTheStudentSurviveASave()
        {
            var loop = HillsPack();
            loop.Companions.Progress("camp_crow").Stars = 1;
            loop.Companions.TakeStudent("camp_crow", "egg_boarlet");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Expeditions.Active[0].Third, Is.EqualTo("hills_piglet"));
            Assert.That(copy.Companions.Progress["camp_crow"].Student, Is.EqualTo("egg_boarlet"));
        }
    }
}
