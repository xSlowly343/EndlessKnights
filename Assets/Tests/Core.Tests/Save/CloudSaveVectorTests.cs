using System;
using System.IO;
using Game.Core.Clock;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Party;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class CloudSaveVectorTests
    {
        private const string VectorPath = "Server/test-vectors/cloud-save.json";
        private const string RiftVectorPath = "Server/test-vectors/cloud-save-rift.json";
        private static readonly long RiftOpen = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        [Test]
        public void TheServerReadsTheSameSaveTheGameWrites()
        {
            var text = Build();
            var path = PartyVectorTests.RepoFile(VectorPath);
            if (Environment.GetEnvironmentVariable(PartyVectorTests.UpdateVariable) == "1")
            {
                File.WriteAllText(path, text);
                Assert.Pass("vector written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {PartyVectorTests.UpdateVariable}=1 to create {VectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(text), $"the save format changed, run the tests with {PartyVectorTests.UpdateVariable}=1 and check the server reader");
            var stored = SaveCodec.Decode(File.ReadAllText(path), TestWorld.Data).State;
            Assert.That(stored.RecordStage, Is.EqualTo(321), "the vector still loads in the game");
            Assert.That(stored.Camp.Bag["herb_windwort"], Is.EqualTo(40));
        }

        [Test]
        public void ARealSaveFitsTheCloudLimit()
        {
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(Build()), Is.LessThan(1_000_000));
        }

        [Test]
        public void TheServerReadsASaveMadeInsideTheRift()
        {
            var text = BuildInRift();
            var path = PartyVectorTests.RepoFile(RiftVectorPath);
            if (Environment.GetEnvironmentVariable(PartyVectorTests.UpdateVariable) == "1")
            {
                File.WriteAllText(path, text);
                Assert.Pass("vector written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {PartyVectorTests.UpdateVariable}=1 to create {RiftVectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(text), $"the save format changed, run the tests with {PartyVectorTests.UpdateVariable}=1 and check the server reader");
            var stored = SaveCodec.Decode(text, TestWorld.Data).State;
            Assert.That(stored.Rift.Inside, Is.True);
            Assert.That(stored.RecordStage, Is.EqualTo(321), "the record is shared");
            Assert.That(stored.Camp.Bag["herb_windwort"], Is.EqualTo(40), "the peaceful bag is shared");
            Assert.That(stored.Gold.ToDouble(), Is.EqualTo(777), "the Rift hero's gold is on top");
            Assert.That(stored.Rift.OtherHero!.Gold.ToDouble(), Is.EqualTo(HomeGold).Within(1e-6), "the home gold waits in rift.otherHero");
        }

        [Test]
        public void ASaveInsideTheRiftStillFitsTheCloudLimit()
        {
            var home = System.Text.Encoding.UTF8.GetByteCount(Build());
            var inside = System.Text.Encoding.UTF8.GetByteCount(BuildInRift());
            TestContext.WriteLine($"home {home} bytes, inside the Rift {inside} bytes");
            Assert.That(inside, Is.LessThan(1_000_000));
            Assert.That(inside - home, Is.LessThan(100_000), "the second hero adds less than 100 KB");
        }

        [Test]
        public void GoingInAndOutOfTheRiftGrowsNothingAtHome()
        {
            var loop = Filled();
            loop.State.Gold = Game.Core.Math.BigNumber.FromDouble(HomeGold);
            loop.State.Drops.Dust = 55;
            var gold = loop.State.Gold;
            var items = loop.State.Drops.Gear.Count;
            var bag = loop.State.Camp.Bag["herb_windwort"];
            var dust = loop.State.Drops.Dust;
            var clock = ClockStamp.First(new ClockReading(RiftOpen, 5, 1));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(loop.Rift.Enter(RiftOpen), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
                loop.State.Gold = loop.State.Gold + Game.Core.Math.BigNumber.FromDouble(500);
                var reloaded = TestWorld.Loop(new FakeInputSource(), SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State);
                Assert.That(reloaded.Rift.Leave(), Is.True);
                Assert.That(reloaded.State.Gold, Is.EqualTo(gold), "home gold");
                Assert.That(reloaded.State.Drops.Gear.Count, Is.EqualTo(items), "home items");
                Assert.That(reloaded.State.Drops.Dust, Is.EqualTo(dust), "home dust");
                Assert.That(reloaded.State.Camp.Bag["herb_windwort"], Is.EqualTo(bag), "the peaceful bag");
                loop = reloaded;
            }
        }

        private const double HomeGold = 123456;

        private static string BuildInRift()
        {
            var loop = Filled();
            loop.State.Gold = Game.Core.Math.BigNumber.FromDouble(HomeGold);
            Assert.That(loop.Rift.Enter(RiftOpen), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            loop.State.Gold = Game.Core.Math.BigNumber.FromDouble(777);
            return SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1_800_000_000_000, 5, 1)));
        }

        private static string Build() => SaveCodec.Encode(Filled().State, ClockStamp.First(new ClockReading(1_800_000_000_000, 5, 1)));

        private static GameLoop Filled()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var state = loop.State;
            state.RecordStage = 321;
            state.TotalInput.PlaySeconds = 7200;
            state.Camp.Bag["herb_windwort"] = 40;
            state.Camp.Bag["ore_hills"] = 12;
            state.Cooking.Meals.Add("carp_pie");
            state.Cooking.Meals.Add("scout_ration");
            state.Cooking.Book.Add("carp_pie");
            state.Alchemy.Potions.Add(new Potion { Recipe = TestWorld.Data.Alchemy.Recipes[0].Id, Fine = true });
            state.Fishing.Album["fish_hills_roach"] = new FishRecord { Count = 2, BestCm = 20 };
            state.Garden.Album.Add("knockbloom");
            state.Expeditions.Finds.Add("find_old_coin");
            return loop;
        }
    }
}
