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

        private static string Build()
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
            return SaveCodec.Encode(state, ClockStamp.First(new ClockReading(1_800_000_000_000, 5, 1)));
        }
    }
}
