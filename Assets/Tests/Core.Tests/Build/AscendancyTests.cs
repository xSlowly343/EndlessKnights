using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class AscendancyTests
    {
        private const long Day = 24L * 3_600_000;

        private static AscendancyCatalog Catalog => TestWorld.Data.Ascendancy;

        private static GameLoop TypistLoop(GameState? state = null) =>
            new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), state ?? new GameState { Class = HeroClass.Duelist }, new StatSheet());

        private static GameLoop UnlockedTypist(params string[] activities)
        {
            var loop = TypistLoop();
            foreach (var activity in activities)
                Assert.That(loop.CompleteActivity(activity), Is.True, activity);
            return loop;
        }

        [Test]
        public void EveryCharacterHasThreeSubclassesAndEightActivitiesGivePoints()
        {
            Assert.That(Catalog.MaxPoints, Is.EqualTo(8));
            Assert.That(Catalog.Subclasses.Count, Is.EqualTo(54));
            foreach (var heroClass in HeroClassNames.ByName.Keys)
                Assert.That(Catalog.SubclassesOf(heroClass).Count, Is.EqualTo(3), heroClass);
        }

        [Test]
        public void SubclassIsLockedUntilTheFirstActivity()
        {
            var loop = TypistLoop();

            Assert.That(loop.State.Ascendancy.Unlocked(Catalog), Is.False);
            Assert.That(loop.ChooseSubclass("duelist_trait_1"), Is.EqualTo(SubclassResult.Locked));
        }

        [Test]
        public void FirstGuardianAndFirstPrestigeCountAsActivities()
        {
            var state = new GameState { Class = HeroClass.Duelist, RecordStage = TestWorld.Data.Stages.BiomeLength + 1, Prestiges = 1 };

            var loop = TypistLoop(state);

            Assert.That(loop.State.Ascendancy.CompletedActivities, Is.EquivalentTo(new[] { ActivityIds.FirstGuardian, ActivityIds.FirstPrestige }));
            Assert.That(loop.State.Ascendancy.PointsEarned(Catalog), Is.EqualTo(2));
        }

        [Test]
        public void EachActivityGivesOnePointOnce()
        {
            var loop = UnlockedTypist("garden", "fishing");

            Assert.That(loop.CompleteActivity("garden"), Is.False);
            Assert.That(loop.CompleteActivity("unknown_activity"), Is.False);
            Assert.That(loop.State.Ascendancy.PointsEarned(Catalog), Is.EqualTo(2));
        }

        [Test]
        public void OnlyOwnClassSubclassesCanBeChosenAndOnlyOnce()
        {
            var loop = UnlockedTypist("garden");

            Assert.That(loop.ChooseSubclass("minstrel_trait_1"), Is.EqualTo(SubclassResult.WrongClass));
            Assert.That(loop.ChooseSubclass("nobody"), Is.EqualTo(SubclassResult.UnknownSubclass));
            Assert.That(loop.ChooseSubclass("duelist_trait_1"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChooseSubclass("duelist_trait_2"), Is.EqualTo(SubclassResult.AlreadyChosen));
        }

        [Test]
        public void ChoicesWaitForThePathAndForEachOpenFork()
        {
            var loop = TypistLoop();
            Assert.That(loop.State.Ascendancy.ChoicesWaiting(Catalog, 1), Is.Zero, "nothing before the first Path point");

            loop.CompleteActivity("garden");
            Assert.That(loop.State.Ascendancy.ChoicesWaiting(Catalog, 1), Is.EqualTo(1), "the Path itself");

            loop.ChooseSubclass("duelist_trait_1");
            Assert.That(loop.State.Ascendancy.ChoicesWaiting(Catalog, 44), Is.Zero);
            Assert.That(loop.State.Ascendancy.ChoicesWaiting(Catalog, 75), Is.EqualTo(3));

            loop.State.HeroLevel = 75;
            var fork = Catalog.SubclassesOf("duelist")[0].Trait!.Forks[0];
            Assert.That(loop.PickTraitFork(fork.Choices[1].Id), Is.EqualTo(TraitForkResult.Picked));
            Assert.That(loop.State.Ascendancy.ChoicesWaiting(Catalog, 75), Is.EqualTo(2));
        }

        [Test]
        public void FreeChangeOnceADayResetsTheForks()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("duelist_trait_1");
            Assert.That(loop.ChangeSubclassFree("duelist_trait_2", Day), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.State.Ascendancy.Subclass, Is.EqualTo("duelist_trait_2"));
            Assert.That(loop.State.Ascendancy.ForkPicks, Is.Empty);
            Assert.That(loop.ChangeSubclassFree("duelist_trait_1", Day + Day / 2), Is.EqualTo(SubclassResult.NotYet));
            Assert.That(loop.ChangeSubclassFree("duelist_trait_1", 2 * Day), Is.EqualTo(SubclassResult.Chosen));
        }

        [Test]
        public void PaidChangeHasNoCooldown()
        {
            var loop = UnlockedTypist("garden");
            loop.ChooseSubclass("duelist_trait_1");
            loop.ChangeSubclassFree("duelist_trait_2", Day);

            Assert.That(loop.ChangeSubclassPaid("duelist_trait_3"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChangeSubclassPaid("duelist_trait_1"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(Catalog.CrystalChangeCost, Is.GreaterThan(0));
        }

        [Test]
        public void ChangeNeedsAChosenSubclass()
        {
            var loop = UnlockedTypist("garden");

            Assert.That(loop.ChangeSubclassFree("duelist_trait_1", Day), Is.EqualTo(SubclassResult.NotChosen));
            Assert.That(loop.ChangeSubclassPaid("duelist_trait_1"), Is.EqualTo(SubclassResult.NotChosen));
        }

        [Test]
        public void SubclassSurvivesSaveAndLoad()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("duelist_trait_1");
            loop.ChangeSubclassFree("duelist_trait_1", Day);
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Ascendancy.Subclass, Is.EqualTo("duelist_trait_1"));
            Assert.That(loaded.Ascendancy.CompletedActivities, Is.EquivalentTo(new[] { "garden", "fishing" }));
            Assert.That(loaded.Ascendancy.LastFreeChangeUnixMs, Is.EqualTo(Day));
        }

        [Test]
        public void SixthVersionSaveStartsWithoutSubclass()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v6.json"), TestWorld.Data);

            Assert.That(save.State.Ascendancy.Subclass, Is.Null);
        }

        private const string Valid = @"{
  ""version"": 1,
  ""activities"": [{ ""id"": ""a"", ""name"": { ""ru"": ""А"", ""en"": ""A"" } }],
  ""change"": { ""freeEveryHours"": 24, ""crystalCost"": 100, ""freeClassChanges"": 1 },
  ""subclasses"": [
    { ""id"": ""s"", ""class"": ""duelist"", ""name"": { ""ru"": ""С"", ""en"": ""S"" } }
  ]
}";

        [Test]
        public void ValidFileLoads()
        {
            var catalog = AscendancyCatalog.Load(Valid);

            Assert.That(catalog.Subclasses.Single().Id, Is.EqualTo("s"));
        }

        [TestCase("\"name\": { \"ru\": \"С\", \"en\": \"S\" } }", "\"name\": { \"ru\": \"С\", \"en\": \"S\" }, \"nodes\": [] }", "subclass 's' has 'nodes'")]
        [TestCase("\"name\": { \"ru\": \"С\", \"en\": \"S\" } }", "\"name\": { \"ru\": \"С\", \"en\": \"S\" }, \"links\": [] }", "subclass 's' has 'links'")]
        [TestCase("\"activities\": [{ \"id\": \"a\", \"name\": { \"ru\": \"А\", \"en\": \"A\" } }]", "\"activities\": []", "at least one activity is required")]
        public void BrokenFilesAreReported(string original, string broken, string message)
        {
            var json = Valid.Replace(original, broken);

            var error = Assert.Throws<ContentException>(() => AscendancyCatalog.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void TheShippedPathsHaveNoNodeTree()
        {
            var json = TestData.ReadGameDataFile(AscendancyCatalog.FileName);

            Assert.That(json, Does.Not.Contain("\"nodes\"").And.Not.Contain("\"links\""));
        }

        [Test]
        public void SubclassClassMustBeAKnownClass()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("\"class\": \"duelist\"", "\"class\": \"pirate\""));

            var error = Assert.Throws<ContentException>(() => catalog.RequireKnownClasses())!;

            Assert.That(error.Message, Does.Contain("unknown class 'pirate'"));
        }
    }
}
