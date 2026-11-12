using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GardenTests
    {
        private static GardenRules Rules => TestWorld.Data.Garden;

        private static GameLoop Gardener(FakeInputSource input)
        {
            var loop = TestWorld.Loop(input);
            loop.Tick();
            return loop;
        }

        [Test]
        public void StarterPlantsNeedNoSeedAndBedsFollowTheCampTier()
        {
            var loop = Gardener(new FakeInputSource());
            Assert.That(loop.Garden.OpenBeds, Is.EqualTo(4));
            Assert.That(loop.Garden.Plant(0, "knockbloom"), Is.EqualTo(GardenResult.Done));
            Assert.That(loop.Garden.Plant(0, "windwort"), Is.EqualTo(GardenResult.BedTaken));
            Assert.That(loop.Garden.Plant(9, "windwort"), Is.EqualTo(GardenResult.NoBed));
            Assert.That(loop.Garden.Plant(1, "stormfern"), Is.EqualTo(GardenResult.NoSeed), "a mutation needs its seed");
            loop.State.RecordStage = 51;
            loop.State.Camp.Tier = 1;
            loop.Tick();
            Assert.That(loop.Garden.OpenBeds, Is.EqualTo(8));
            Assert.That(GardenView.Seeds(loop.Garden, loop.Camp).Count(s => s.Starter), Is.EqualTo(4));
        }

        [Test]
        public void EachPlantGrowsFasterFromItsOwnInputInTheCampGarden()
        {
            var input = new FakeInputSource();
            var loop = Gardener(input);
            loop.Garden.Plant(0, "knockbloom");
            loop.Garden.Plant(1, "windwort");
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(GardenSystem.Activity);
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(2);
                loop.Tick();
            }
            var keys = loop.State.Garden.Beds[0].Grown;
            var mouse = loop.State.Garden.Beds[1].Grown;
            Assert.That(mouse, Is.EqualTo(10).Within(0.01), "ten seconds of real time only");
            Assert.That(keys, Is.EqualTo(10 + 200 * Rules.Plant("knockbloom")!.SecondsPerUnit).Within(0.01), "every key press waters the knockbloom");
        }

        [Test]
        public void ARipePlantIsHarvestedIntoTheBagAndTheOfflineTimeCounts()
        {
            var loop = Gardener(new FakeInputSource());
            loop.Garden.Plant(0, "hushbell");
            Assert.That(loop.Garden.Harvest(0), Is.EqualTo(GardenResult.NotReady));
            loop.Garden.Advance(Rules.Plant("hushbell")!.GrowSeconds);
            Assert.That(loop.State.Garden.Beds[0].Ready, Is.True);
            Assert.That(GardenView.Beds(loop.Garden)[0].Progress, Is.EqualTo(1));
            Assert.That(loop.Garden.Harvest(0), Is.EqualTo(GardenResult.Done));
            Assert.That(loop.Camp.Count("herb_hushbell"), Is.EqualTo(Rules.Plant("hushbell")!.Yield));
            Assert.That(loop.State.Garden.Beds[0].Empty, Is.True);
            Assert.That(loop.State.Garden.Album, Does.Contain("hushbell"));
        }

        [Test]
        public void NeighboursCanCrossIntoAMutationSeed()
        {
            var found = 0;
            for (var seed = 1UL; seed <= 40; seed++)
            {
                var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
                loop.Tick();
                loop.Garden.Plant(0, "knockbloom");
                loop.Garden.Plant(1, "clickweed");
                loop.Garden.Advance(Rules.Plant("knockbloom")!.GrowSeconds);
                found += loop.Camp.Count("seed_stormfern") > 0 ? 1 : 0;
            }
            Assert.That(found, Is.InRange(2, 25), "a fair share of gardens find the stormfern");
        }

        [Test]
        public void TickingTheCampGardenAndAnEventDoesNotAllocate()
        {
            var input = new FakeInputSource();
            var loop = Gardener(input);
            loop.SetLocalDay(EventRules.DayOf(2026, 10, 31));
            for (var bed = 0; bed < 4; bed++)
                loop.Garden.Plant(bed, Rules.Plants[bed].Id);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(GardenSystem.Activity);
            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(1);
                input.MoveMouse(10);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "camp");

            loop.Peaceful.SetMode(PeacefulMode.Battle);
            for (var i = 0; i < 300; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "battle during an event with a growing garden");
        }

        [Test]
        public void TheGardenIsSaved()
        {
            var loop = Gardener(new FakeInputSource());
            loop.Garden.Plant(2, "clickweed");
            loop.Garden.Advance(100);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Garden.Beds[2].Plant, Is.EqualTo("clickweed"));
            Assert.That(copy.Garden.Beds[2].Grown, Is.EqualTo(loop.State.Garden.Beds[2].Grown));
            Assert.That(copy.Garden.Beds[0].Empty, Is.True);
        }
    }
}
