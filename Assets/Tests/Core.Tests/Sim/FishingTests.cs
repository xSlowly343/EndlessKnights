using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class FishingTests
    {
        private static FishingRules Rules => TestWorld.Data.Fishing;
        private const float PixelsPerMeter = (float)(1 / InputStats.DefaultMetersPerPixel);

        private static (GameLoop Loop, FakeInputSource Input) Angler(ulong seed = 1)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(seed));
            loop.State.RecordStage = 51;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "fishers" });
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(FishingSystem.Activity);
            return (loop, input);
        }

        private static void WaitForBite(GameLoop loop)
        {
            for (var i = 0; i < 10_000 && loop.State.Fishing.Phase != FishingPhase.Bite; i++)
            {
                if (loop.State.Fishing.Phase == FishingPhase.Waiting)
                    loop.State.Fishing.Timer = 0.05;
                loop.Tick();
            }
            Assert.That(loop.State.Fishing.Phase, Is.EqualTo(FishingPhase.Bite));
        }

        private static void Land(GameLoop loop, FakeInputSource input)
        {
            input.ClickLeft();
            loop.Tick();
            Assert.That(loop.State.Fishing.Phase, Is.EqualTo(FishingPhase.Fight));
            for (var i = 0; i < 100 && loop.State.Fishing.Phase == FishingPhase.Fight; i++)
            {
                input.MoveMouse(PixelsPerMeter);
                loop.Tick();
            }
        }

        [Test]
        public void ThePierWaitsForTheFishersGuildAndSpotsForReachedBiomes()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.State.RecordStage = 51;
            loop.Tick();
            Assert.That(loop.Fishing.Open, Is.False, "no fishers guild in the town yet");
            loop.Camp.SetGuilds(new[] { "fishers" });
            Assert.That(loop.Fishing.Open, Is.True);
            Assert.That(loop.Fishing.SetSpot("sand_ruins"), Is.EqualTo(FishingResult.Done));
            Assert.That(loop.Fishing.SetSpot("ice_peak"), Is.EqualTo(FishingResult.SpotClosed));
            Assert.That(loop.Fishing.SetSpot("moon"), Is.EqualTo(FishingResult.UnknownSpot));
            Assert.That(FishingView.Spots(loop.Fishing).Count(s => s.Open), Is.EqualTo(2));
            loop.Peaceful.SetActivity(GardenSystem.Activity);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Tick();
            Assert.That(loop.State.Fishing.Phase, Is.EqualTo(FishingPhase.Idle), "the line is only cast while fishing");
        }

        [Test]
        public void AFloatBitesAClickHooksAndTheMouseReelsTheFishIn()
        {
            var (loop, input) = Angler();
            loop.Tick();
            Assert.That(loop.State.Fishing.Phase, Is.EqualTo(FishingPhase.Waiting));
            Assert.That(loop.State.Fishing.Timer, Is.InRange(Rules.BiteMinSeconds - 0.1, Rules.BiteMaxSeconds));
            loop.Events.Clear();
            WaitForBite(loop);
            Assert.That(loop.Events.CountOf(GameEventKind.FishBite), Is.EqualTo(1));
            var hooked = loop.State.Fishing.HookedKind;
            Land(loop, input);
            Assert.That(loop.Events.CountOf(GameEventKind.FishCaught), Is.EqualTo(1));
            if (hooked == FishCatchKind.Fish)
            {
                var id = loop.State.Fishing.LastCatch;
                Assert.That(Rules.Spot("green_hills")!.Fish.Any(f => f.Id == id));
                Assert.That(loop.Camp.Count(id), Is.EqualTo(1));
                Assert.That(loop.Fishing.Record(id)!.BestCm, Is.EqualTo(loop.State.Fishing.LastCm));
                Assert.That(FishingView.Album(loop.Fishing).Single(l => l.Id == id).Name, Is.Not.Empty);
            }
        }

        [Test]
        public void AMissedBiteOrAnEscapedFishCostsNothing()
        {
            var (loop, input) = Angler();
            WaitForBite(loop);
            for (var i = 0; i <= Rules.BiteWindowSeconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
            Assert.That(loop.State.Fishing.Missed, Is.EqualTo(1));
            WaitForBite(loop);
            loop.State.Fishing.HookedKind = FishCatchKind.Fish;
            loop.State.Fishing.Hooked = Rules.Spots[0].Fish[0].Id;
            input.ClickLeft();
            loop.Tick();
            for (var i = 0; i <= Rules.FightSeconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
            Assert.That(loop.State.Fishing.Escaped, Is.EqualTo(1));
            Assert.That(loop.State.Fishing.Caught, Is.Zero);
            Assert.That(loop.State.Camp.Bag, Is.Empty);
        }

        [Test]
        public void TheLegendTiresAcrossSessionsAndIsCaughtOnce()
        {
            var (loop, input) = Angler();
            var legend = Rules.Spots[0].Legend;
            var sessions = 0;
            while (loop.Fishing.Record(legend.Id) == null && sessions < 50)
            {
                WaitForBite(loop);
                loop.State.Fishing.HookedKind = FishCatchKind.Legend;
                loop.State.Fishing.Hooked = legend.Id;
                input.ClickLeft();
                loop.Tick();
                for (var i = 0; i <= Rules.FightSeconds * GameLoop.TicksPerSecond && loop.State.Fishing.Phase == FishingPhase.Fight; i++)
                {
                    input.MoveMouse(PixelsPerMeter * 0.03f);
                    loop.Tick();
                }
                sessions++;
                if (sessions == 1)
                    Assert.That(loop.Fishing.LegendLeft(legend), Is.LessThan(legend.Stamina), "the first session tires the legend");
            }
            Assert.That(sessions, Is.InRange(2, 49));
            Assert.That(loop.Fishing.Record(legend.Id)!.BestCm, Is.EqualTo(legend.Cm));
            Assert.That(FishingView.Spots(loop.Fishing)[0].LegendCaught, Is.True);
            for (var i = 0; i < 200; i++)
            {
                WaitForBite(loop);
                Assert.That(loop.State.Fishing.HookedKind, Is.Not.EqualTo(FishCatchKind.Legend), "a caught legend does not bite again");
                loop.State.Fishing.Phase = FishingPhase.Idle;
            }
        }

        [Test]
        public void RiftFishBiteOnlyDuringARiftWindowAndBottlesBringShards()
        {
            var (loop, input) = Angler();
            int Rift()
            {
                var rift = 0;
                for (var i = 0; i < 300; i++)
                {
                    WaitForBite(loop);
                    rift += Rules.RiftFish.Any(f => f.Id == loop.State.Fishing.Hooked) ? 1 : 0;
                    loop.State.Fishing.Phase = FishingPhase.Idle;
                }
                return rift;
            }
            Assert.That(Rift(), Is.Zero);
            loop.Fishing.RiftWindow = true;
            Assert.That(Rift(), Is.InRange(30, 120));

            WaitForBite(loop);
            loop.State.Fishing.HookedKind = FishCatchKind.Bottle;
            var shards = loop.State.Drops.Shards;
            Land(loop, input);
            Assert.That(loop.State.Drops.Shards - shards, Is.EqualTo(Rules.BottleShards));
            Assert.That(loop.State.Fishing.Bottles, Is.EqualTo(1));
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(Rules.BottleDust));
        }

        [Test]
        public void TheAlbumAndTheTiredLegendSurviveTheSave()
        {
            var (loop, _) = Angler();
            loop.State.Fishing.Album["fish_hills_carp"] = new FishRecord { Count = 3, BestCm = 41.5 };
            loop.State.Fishing.LegendTired["legend_hills_boar_catfish"] = 12.5;
            loop.Fishing.SetSpot("sand_ruins");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(JsonWriter.Write(GameStateJson.Write(loop.State))));
            Assert.That(copy.Fishing.Album["fish_hills_carp"].BestCm, Is.EqualTo(41.5));
            Assert.That(copy.Fishing.Spot, Is.EqualTo("sand_ruins"));
        }

        [Test]
        public void FishingTicksDoNotAllocate()
        {
            var (loop, input) = Angler();
            foreach (var spot in Rules.Spots)
                foreach (var fish in spot.Fish)
                    loop.State.Fishing.Album[fish.Id] = new FishRecord();
            foreach (var spot in Rules.Spots)
                loop.State.Fishing.Album[spot.Legend.Id] = new FishRecord();
            void Run(int ticks)
            {
                for (var i = 0; i < ticks; i++)
                {
                    if (loop.State.Fishing.Phase == FishingPhase.Waiting)
                        loop.State.Fishing.Timer = System.Math.Min(loop.State.Fishing.Timer, 0.3);
                    input.ClickLeft();
                    input.MoveMouse(PixelsPerMeter * 0.3f);
                    loop.Tick();
                    loop.Events.Clear();
                }
            }
            Run(100);
            var caught = loop.State.Fishing.Caught;
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            Run(200);
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
            Assert.That(loop.State.Fishing.Caught + loop.State.Fishing.Bottles, Is.GreaterThan(caught));
        }

        [TestCase(@"""building"": ""pier""", @"""building"": ""boat""", "unknown building 'boat'")]
        [TestCase(@"""biome"": ""ice_peak""", @"""biome"": ""moon""", "unknown biome 'moon'")]
        [TestCase(@"""id"": ""fish_hills_carp""", @"""id"": ""fish_hills_roach""", "duplicate fish 'fish_hills_roach'")]
        public void BrokenFishingIsReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(FishingRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => FishingRules.Load(json, TestWorld.Data.Camp, TestWorld.Data.Stages.Biomes))!;
            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void FishingTogetherRaisesTheRareCatchAndCountsCatchesForTheServer()
        {
            var rules = Rules;
            Assert.That(rules.TogetherLegendChance, Is.GreaterThan(0));
            Assert.That(rules.TogetherBottleChance, Is.GreaterThan(0));

            int Bottles(bool together)
            {
                var (loop, input) = Angler(7);
                loop.Fishing.Together = together;
                var bottles = 0;
                for (var i = 0; i < 300; i++)
                {
                    WaitForBite(loop);
                    if (loop.State.Fishing.HookedKind == FishCatchKind.Bottle)
                        bottles++;
                    loop.State.Fishing.Timer = 0;
                    loop.Tick();
                    loop.Tick();
                }
                return bottles;
            }

            Assert.That(Bottles(true), Is.GreaterThan(Bottles(false)));

            var (angler, hands) = Angler();
            Assert.That(angler.Fishing.Fishing, Is.True);
            WaitForBite(angler);
            Land(angler, hands);
            Assert.That(angler.Fishing.TakeCaught(), Is.EqualTo(1));
            Assert.That(angler.Fishing.TakeCaught(), Is.Zero);
        }
    }
}
