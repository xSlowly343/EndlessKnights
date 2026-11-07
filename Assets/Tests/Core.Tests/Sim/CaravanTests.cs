using Game.Core.Content;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CaravanTests
    {
        private static CaravanRules Rules => TestWorld.Real.Caravan;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static GameLoop Road(long record)
        {
            var loop = TestWorld.RealLoop();
            loop.State.RecordStage = record;
            loop.Tick();
            return loop;
        }

        private static void Mouse(GameLoop loop, double meters)
        {
            loop.State.TotalInput.MouseMeters += meters;
            loop.Caravan.Tick();
        }

        [Test]
        public void BrokenCaravanRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(CaravanRules.FileName);
            var biomes = TestWorld.Real.Stages.Biomes;
            Assert.That(CaravanRules.Load(json, biomes).Stops.Count, Is.EqualTo(7));
            Assert.Throws<ContentException>(() => CaravanRules.Load(json.Replace("\"biome\": \"ice_peak\"", "\"biome\": \"moon\""), biomes));
            Assert.Throws<ContentException>(() => CaravanRules.Load(json.Replace("\"meters\": 2500", "\"meters\": 500"), biomes));
        }

        [Test]
        public void MouseMetersDriveTheCaravanToAStopAndItsGoodsWaitForTheCamp()
        {
            var loop = Road(1);
            var first = Rules.Stops[0];
            Mouse(loop, first.Meters - 1);
            Assert.That(loop.State.Caravan.Arrivals, Is.Zero);
            Mouse(loop, 2);
            Assert.That(loop.State.Caravan.Arrivals, Is.EqualTo(1));
            Assert.That(loop.State.Caravan.LastStop, Is.EqualTo(first.Id));
            Assert.That(loop.Camp.Count(first.Goods[0].Item), Is.Zero, "the goods come home in the camp");
            loop.Caravan.Collect();
            Assert.That(loop.Camp.Count(first.Goods[0].Item), Is.EqualTo(first.Goods[0].Count));
            Assert.That(loop.State.Caravan.Waiting, Is.Zero);
        }

        [Test]
        public void TheCaravanWaitsAtTheEdgeOfTheBiomesYouHaveReached()
        {
            var loop = Road(1);
            Mouse(loop, 100_000);
            Assert.That(loop.Caravan.NextStop.Biome, Is.EqualTo("sand_ruins"));
            Assert.That(loop.Caravan.Blocked, Is.True);
            Assert.That(loop.State.Caravan.Arrivals, Is.EqualTo(2));
            Assert.That(CaravanView.Road(loop.Caravan), Does.Contain("караван ждёт у края"));
        }

        [Test]
        public void AFullLapStartsTheRoadAgainAndTheSandwalkerGoesFarther()
        {
            var loop = Road(300);
            Mouse(loop, Rules.LapMeters + 10);
            Assert.That(loop.State.Caravan.Laps, Is.EqualTo(1));
            Assert.That(loop.State.Caravan.Arrivals, Is.EqualTo(Rules.Stops.Count));
            Assert.That(loop.Caravan.TotalMeters, Is.EqualTo(Rules.LapMeters + 10).Within(1e-6));
            loop.Caravan.Collect();
            Assert.That(loop.Camp.Count("seed_moonmoss"), Is.EqualTo(2));

            Assert.That(TestWorld.Real.Talents.Of(HeroClass.Sandwalker)!.Works, Is.True);
            var walker = Road(300);
            walker.State.TalentPerks[(int)MuseumPerk.LongerRoad] = 0.5;
            Mouse(walker, 1000);
            Assert.That(walker.State.Caravan.Traveled, Is.EqualTo(1500).Within(1e-6));
        }

        [Test]
        public void TheSaveKeepsTheRoadAndAnOldSaveDoesNotCountPastMeters()
        {
            var loop = Road(1);
            loop.State.Caravan.SeenMouseMeters = -1;
            loop.State.TotalInput.MouseMeters = 50_000;
            loop.Caravan.Tick();
            Assert.That(loop.State.Caravan.Traveled, Is.Zero, "meters before the caravan are not counted");
            Mouse(loop, 1200);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Caravan.Traveled, Is.EqualTo(1200).Within(1e-6));
            Assert.That(copy.Caravan.Waiting, Is.EqualTo(1));
            Assert.That(copy.Caravan.LastStop, Is.EqualTo(Rules.Stops[0].Id));
            Assert.That(copy.Caravan.SeenMouseMeters, Is.EqualTo(51_200).Within(1e-6));
        }
    }
}
