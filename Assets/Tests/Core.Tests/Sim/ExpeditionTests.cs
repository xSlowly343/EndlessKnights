using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ExpeditionTests
    {
        private static ExpeditionRules Rules => TestWorld.Data.Expeditions;
        private const double Hour = ExpeditionSystem.SecondsPerHour;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            return loop;
        }

        [Test]
        public void TwoFreeCompanionsAndEveryOwnedCosmeticCompanion()
        {
            var loop = Camp();
            Assert.That(loop.Expeditions.Companions(), Is.EqualTo(new[] { "camp_dog", "camp_crow" }));
            loop.State.Cosmetics.Owned.Add("pet_fox");
            loop.State.Cosmetics.Owned.Add("theme_hills");
            Assert.That(loop.Expeditions.Companions(), Is.EqualTo(new[] { "camp_dog", "camp_crow", "pet_fox" }));
            Assert.That(loop.Expeditions.TraitOf("pet_fox")!.Id, Is.EqualTo(Rules.CosmeticTrait));
            Assert.That(loop.Expeditions.TraitOf("pet_owl"), Is.Null, "a companion that is not owned");
        }

        [Test]
        public void OnlyReachedBiomesAndListedHoursAndOneTripPerCompanion()
        {
            var loop = Camp();
            Assert.That(loop.Expeditions.Send("camp_dog", "sand_ruins", 1), Is.EqualTo(ExpeditionResult.BiomeClosed));
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 3), Is.EqualTo(ExpeditionResult.BadHours));
            Assert.That(loop.Expeditions.Send("camp_dog", "rift_heart", 1), Is.EqualTo(ExpeditionResult.UnknownBiome));
            Assert.That(loop.Expeditions.Send("pet_owl", "green_hills", 1), Is.EqualTo(ExpeditionResult.UnknownCompanion));
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 1), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 2), Is.EqualTo(ExpeditionResult.CompanionBusy));
            loop.State.RecordStage = 51;
            Assert.That(loop.Expeditions.Send("camp_crow", "sand_ruins", 2), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(ExpeditionView.Companions(loop.Expeditions).All(c => c.Busy), Is.True);
        }

        [Test]
        public void ALetterAtHalfWayAndLootByBiomeAndTraitOnReturn()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_dog", "green_hills", 4);
            loop.Expeditions.Send("camp_crow", "green_hills", 4);
            loop.Events.Clear();
            loop.Expeditions.Advance(2 * Hour);
            Assert.That(loop.Events.CountOf(GameEventKind.ExpeditionLetter), Is.EqualTo(2));
            Assert.That(ExpeditionView.Active(loop.Expeditions)[0].Letter, Is.Not.Empty);
            for (var i = loop.State.Expeditions.Active.Count - 1; i >= 0; i--)
                loop.Expeditions.Answer(i, true);
            loop.Events.Clear();
            var shards = loop.State.Drops.Shards;
            loop.Expeditions.Advance(10 * Hour);
            Assert.That(loop.State.Expeditions.Active, Is.Empty);
            Assert.That(loop.State.Expeditions.Returned, Is.EqualTo(2));
            Assert.That(loop.Events.CountOf(GameEventKind.ExpeditionReturned), Is.EqualTo(2));
            var dog = loop.State.Expeditions.Reports.First(r => r.Companion == "camp_dog");
            var crow = loop.State.Expeditions.Reports.First(r => r.Companion == "camp_crow");
            Assert.That(dog.Items[0], Is.GreaterThan(crow.Items[0]), "the herbalist brings more herbs");
            Assert.That(crow.Shards, Is.GreaterThan(dog.Shards), "the digger brings more shards");
            Assert.That(loop.State.Drops.Shards - shards, Is.EqualTo(dog.Shards + crow.Shards));
            Assert.That(loop.Camp.Count("herb_knockbloom"), Is.EqualTo(dog.Items[0] + crow.Items[0]));
        }

        [Test]
        public void WaitingStretchesTheTripAndReturningCutsItShort()
        {
            var waited = 0.0;
            var cut = 0.0;
            for (var seed = 1UL; seed <= 60 && (waited == 0 || cut == 0); seed++)
            {
                var loop = Camp(seed);
                loop.Expeditions.Send("camp_dog", "green_hills", 8);
                loop.Expeditions.Advance(4 * Hour);
                if (!loop.State.Expeditions.Active[0].Choice)
                    continue;
                var wait = waited == 0;
                Assert.That(loop.Expeditions.Answer(0, wait), Is.EqualTo(ExpeditionResult.Done));
                Assert.That(loop.Expeditions.Answer(0, wait), Is.EqualTo(ExpeditionResult.NoChoice));
                loop.Expeditions.Advance(100 * Hour);
                var report = loop.State.Expeditions.Reports[0];
                if (wait)
                    waited = report.Elapsed;
                else
                    cut = report.Elapsed;
            }
            Assert.That(waited, Is.EqualTo(8 * Hour * (1 + Rules.WaitExtend)));
            Assert.That(cut, Is.EqualTo(4 * Hour));
        }

        [Test]
        public void RareFindsComeNowAndThenAndTheWandererFindsMore()
        {
            int Finds(string companion)
            {
                var found = 0;
                for (var seed = 1UL; seed <= 300; seed++)
                {
                    var loop = Camp(seed);
                    loop.Companions.Grant("egg_griffin");
                    loop.Expeditions.Send(companion, "green_hills", 8);
                    loop.Expeditions.Advance(8 * Hour);
                    found += loop.State.Expeditions.Reports[0].Rare.Length > 0 ? 1 : 0;
                }
                return found;
            }
            var dog = Finds("camp_dog");
            var fox = Finds("egg_griffin");
            Assert.That(dog, Is.InRange(30, 120));
            Assert.That(fox, Is.GreaterThan(dog));
        }

        [Test]
        public void TripsRunOfflineAndSurviveTheSave()
        {
            var loop = Camp();
            loop.Expeditions.Send("camp_dog", "green_hills", 2);
            loop.Expeditions.Send("camp_crow", "green_hills", 8);
            loop.Expeditions.Advance(3 * Hour);
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(Game.Core.Content.Json.JsonWriter.Write(GameStateJson.Write(copy)), Is.EqualTo(Game.Core.Content.Json.JsonWriter.Write(GameStateJson.Write(loop.State))));
            Assert.That(copy.Expeditions.Active.Single().Elapsed, Is.EqualTo(3 * Hour));
            Assert.That(copy.Expeditions.Reports.Single().Companion, Is.EqualTo("camp_dog"));
        }

        [Test]
        public void TicksWithTripsOnTheRoadDoNotAllocate()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Tick();
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            loop.Expeditions.Send("camp_crow", "green_hills", 8);
            for (var i = 0; i < 300; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [TestCase(@"""trait"": ""digger""", @"""trait"": ""miner""", "unknown trait 'miner'")]
        [TestCase(@"""biome"": ""ice_peak""", @"""biome"": ""moon""", "unknown biome 'moon'")]
        public void BrokenExpeditionsAreReported(string from, string to, string message)
        {
            var json = Content.TestData.ReadGameDataFile(ExpeditionRules.FileName).Replace(from, to);
            var error = Assert.Throws<ContentException>(() => ExpeditionRules.Load(json, TestWorld.Data.Stages.Biomes))!;
            Assert.That(error.Message, Does.Contain(message));
        }
    }
}
