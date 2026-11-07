using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionTemperTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void EightTempersEachWithAPlus()
        {
            var tempers = TestWorld.Data.Companions.Tempers.Tempers;
            Assert.That(tempers.Count, Is.EqualTo(8));
            foreach (var temper in tempers)
                Assert.That(temper.Plus.Count, Is.GreaterThan(0), temper.Id);
        }

        [Test]
        public void ACompanionWithoutATemperIsNeutral()
        {
            var loop = Camp();
            Assert.That(loop.Companions.TemperOf("camp_dog"), Is.Null);
            Assert.That(loop.Companions.Temper("camp_dog", TemperStat.TripTime), Is.EqualTo(0));
        }

        [Test]
        public void AnOwnedCompanionIsBornWithATemperThatStaysPut()
        {
            var loop = Camp();
            loop.Companions.Grant("jackal_pup");
            var born = loop.Companions.TemperOf("jackal_pup");
            Assert.That(born, Is.Not.Null);
            loop.Tick();
            Assert.That(loop.Companions.TemperOf("jackal_pup"), Is.SameAs(born));
        }

        [Test]
        public void TheQuickTemperShortensTripsAndTrimsLoot()
        {
            var loop = Camp();
            var companions = loop.Companions;
            var before = companions.DurationFactor("camp_dog", 0);
            var items = companions.TravelMultiplier("camp_dog", TravelStat.Items);
            Assert.That(companions.ChangeTemper("camp_dog", "quick"), Is.EqualTo(CompanionResult.Done));
            Assert.That(companions.DurationFactor("camp_dog", 0), Is.EqualTo(before * 0.92).Within(1e-9));
            Assert.That(companions.TravelMultiplier("camp_dog", TravelStat.Items), Is.LessThan(items));
        }

        [Test]
        public void TheFirstChangeIsFreeThenItCostsSoulsUntilAMonthPasses()
        {
            var loop = Camp();
            var companions = loop.Companions;
            loop.State.Companions.Souls = 25;
            Assert.That(companions.ChangeTemper("camp_dog", "glutton"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(25), "the first change is free");
            Assert.That(companions.ChangeTemper("camp_dog", "glutton"), Is.EqualTo(CompanionResult.Repeat));
            Assert.That(companions.ChangeTemper("camp_dog", "curious"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(5));
            Assert.That(companions.ChangeTemper("camp_dog", "quick"), Is.EqualTo(CompanionResult.NoSouls));
            loop.SetLocalDay(130);
            Assert.That(companions.ChangeTemper("camp_dog", "quick"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(5), "free again after 30 days");
            Assert.That(companions.ChangeTemper("camp_dog", "nobody"), Is.EqualTo(CompanionResult.UnknownTrait));
        }

        [Test]
        public void TheGluttonEatsMoreButLessOften()
        {
            var loop = Camp();
            var companions = loop.Companions;
            var left = companions.FoodLeft("camp_dog");
            companions.ChangeTemper("camp_dog", "glutton");
            Assert.That(companions.FoodLeft("camp_dog"), Is.EqualTo(left - TestWorld.Data.Companions.FoodPerDay * 0.1).Within(1e-9));
        }

        [Test]
        public void TheAffectionateBonusStopsAtFiveHearts()
        {
            var loop = Camp();
            var companions = loop.Companions;
            companions.ChangeTemper("camp_dog", "affectionate");
            Assert.That(companions.Temper("camp_dog", TemperStat.Bond), Is.EqualTo(0.2).Within(1e-9));
            companions.Progress("camp_dog").Bond = TestWorld.Data.Companions.Bond.PointsTo(5);
            Assert.That(companions.Temper("camp_dog", TemperStat.Bond), Is.EqualTo(0));
        }

        [Test]
        public void TheSleepyTemperLikesLongTripsAndDislikesShortOnes()
        {
            var loop = Camp();
            var companions = loop.Companions;
            var shortTrip = companions.DurationFactor("camp_dog", 0, "", 2);
            var longTrip = companions.DurationFactor("camp_dog", 0, "", 8);
            companions.ChangeTemper("camp_dog", "sleepy");
            Assert.That(companions.DurationFactor("camp_dog", 0, "", 2), Is.EqualTo(shortTrip * 1.05).Within(1e-9));
            Assert.That(companions.DurationFactor("camp_dog", 0, "", 8), Is.EqualTo(longTrip).Within(1e-9));
        }

        [Test]
        public void TheTemperNodeStrengthensThePlusAndSoftensTheMinus()
        {
            var loop = Camp();
            var companions = loop.Companions;
            companions.ChangeTemper("camp_dog", "quick");
            var plus = companions.Temper("camp_dog", TemperStat.TripTime);
            var minus = companions.Temper("camp_dog", TemperStat.TripLoot);
            loop.State.Companions.TreeRanks[26] = 3;
            loop.Menagerie.Changed();
            Assert.That(companions.Temper("camp_dog", TemperStat.TripTime), Is.EqualTo(plus * 1.1).Within(1e-9));
            Assert.That(companions.Temper("camp_dog", TemperStat.TripLoot), Is.EqualTo(minus * 0.9).Within(1e-9));
        }

        [Test]
        public void TheTemperSurvivesASave()
        {
            var loop = Camp();
            loop.Companions.ChangeTemper("camp_dog", "stubborn");
            var copy = Game.Core.Save.GameStateJson.Read(new ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Temper, Is.EqualTo("stubborn"));
            Assert.That(copy.Companions.Progress["camp_dog"].TemperFreeDay, Is.EqualTo(100));
        }
    }
}
