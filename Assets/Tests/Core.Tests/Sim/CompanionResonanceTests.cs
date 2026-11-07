using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionResonanceTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        private static void Level(GameLoop loop, string companion, int level)
        {
            loop.Companions.Grant(companion);
            loop.Companions.Progress(companion).Level = level;
        }

        [Test]
        public void EveryHundredLevelsAddTwoPercentToTripsAndPeacefulWork()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Resonance(), Is.EqualTo(2));
            Level(loop, "camp_dog", 60);
            Level(loop, "camp_crow", 60);
            loop.Companions.ApplyPerks();
            Assert.That(loop.Companions.Resonance(), Is.EqualTo(120));
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.ExpeditionLoot], Is.EqualTo(0.02).Within(1e-9));
            Assert.That(loop.State.CompanionPerks[(int)MuseumPerk.GrowFaster], Is.EqualTo(0.02).Within(1e-9));
        }

        [Test]
        public void ResonanceIsCappedAndOpensSlots()
        {
            var loop = Camp();
            var slots = loop.Companions.TripSlots;
            var home = loop.Companions.HomeSlots;
            foreach (var def in loop.Companions.Rules.Companions)
                Level(loop, def.Id, 50);
            Assert.That(loop.Companions.Resonance(), Is.EqualTo(60 * 50));
            Assert.That(loop.Companions.ResonanceBonus(), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(loop.Companions.HomeSlots, Is.EqualTo(home + 1));
            Assert.That(loop.Companions.CampLook, Is.True);
            Assert.That(loop.Companions.TripSlots, Is.GreaterThanOrEqualTo(slots + 1));
        }

        [Test]
        public void FiftyTripsGiveMasteryAndMoreExperience()
        {
            var loop = Camp();
            var dog = loop.Companions.Progress("camp_dog");
            dog.Trips = 49;
            Assert.That(loop.Companions.MasteryOf("camp_dog"), Is.Zero);
            dog.Trips = 50;
            Assert.That(loop.Companions.MasteryOf("camp_dog"), Is.EqualTo(1));
            loop.Companions.Gain("camp_dog", 10);
            Assert.That(dog.Experience, Is.EqualTo(10.5).Within(1e-9));
            dog.Trips = 500;
            Assert.That(loop.Companions.MasteryOf("camp_dog"), Is.EqualTo(3));
        }

        [Test]
        public void ATripCountsForMastery()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_dog", "green_hills", 4);
            loop.Expeditions.Advance(4 * ExpeditionSystem.SecondsPerHour);
            Assert.That(loop.Companions.Progress("camp_dog").Trips, Is.EqualTo(1));
        }
    }
}
