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
            new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), state ?? new GameState { Class = HeroClass.Swordsman }, new StatSheet());

        private static GameLoop UnlockedTypist(params string[] activities)
        {
            var loop = TypistLoop();
            foreach (var activity in activities)
                Assert.That(loop.CompleteActivity(activity), Is.True, activity);
            return loop;
        }

        [Test]
        public void EveryPlayableClassHasThreeSubclassesAndEightActivitiesGivePoints()
        {
            Assert.That(Catalog.MaxPoints, Is.EqualTo(8));
            foreach (var heroClass in HeroClassNames.ByName.Keys)
                Assert.That(Catalog.SubclassesOf(heroClass).Count, Is.EqualTo(3), heroClass);
        }

        [Test]
        public void OpenNotablesTakeAllPointsButNotEveryNotable()
        {
            foreach (var subclass in Catalog.Subclasses)
            {
                var notables = subclass.Nodes.Count(n => n.Type == TreeNodeType.Notable);
                var open = subclass.Nodes.Count(n => n.Type == TreeNodeType.Notable && !n.Locked);
                Assert.That(open * 2, Is.GreaterThanOrEqualTo(Catalog.MaxPoints), subclass.Id);
                Assert.That(notables * 2, Is.GreaterThan(Catalog.MaxPoints), subclass.Id);
            }
        }

        [Test]
        public void SubclassIsLockedUntilTheFirstActivity()
        {
            var loop = TypistLoop();

            Assert.That(loop.State.Ascendancy.Unlocked(Catalog), Is.False);
            Assert.That(loop.ChooseSubclass("duellist"), Is.EqualTo(SubclassResult.Locked));
        }

        [Test]
        public void FirstGuardianAndFirstPrestigeCountAsActivities()
        {
            var state = new GameState { Class = HeroClass.Swordsman, RecordStage = TestWorld.Data.Stages.BiomeLength + 1, Prestiges = 1 };

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

            Assert.That(loop.ChooseSubclass("skald"), Is.EqualTo(SubclassResult.WrongClass));
            Assert.That(loop.ChooseSubclass("nobody"), Is.EqualTo(SubclassResult.UnknownSubclass));
            Assert.That(loop.ChooseSubclass("duellist"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChooseSubclass("stormcaller"), Is.EqualTo(SubclassResult.AlreadyChosen));
        }

        [Test]
        public void SubclassNodesGrowFromItsStartAndCostAPointEach()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("duellist");

            Assert.That(loop.AllocateSubclassNode("duellist_0"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.State.Ascendancy.Allocatable(Catalog).All(n => n.Type == TreeNodeType.Small), Is.True);
            Assert.That(loop.AllocateSubclassNode("duellist_0_path"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateSubclassNode("duellist_0"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateSubclassNode("duellist_1_path"), Is.EqualTo(AllocationResult.NoPoints));
            Assert.That(loop.AllocateSubclassNode("stormcaller_0_path"), Is.EqualTo(AllocationResult.UnknownNode));
        }

        [Test]
        public void NotablesWaitingForTheirMechanicCanBeLocked()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("\"id\": \"s_notable\", \"type\": \"notable\"", "\"id\": \"s_notable\", \"type\": \"notable\", \"locked\": true"));
            var state = new AscendancyState();
            state.Complete("a");
            state.Choose(catalog, "swordsman", "s");

            Assert.That(state.Allocate(catalog, "s_small"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(state.Allocate(catalog, "s_notable"), Is.EqualTo(AllocationResult.Locked));
        }

        [Test]
        public void SubclassNotablesChangeTheHero()
        {
            var loop = UnlockedTypist("garden", "fishing");
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse);

            loop.ChooseSubclass("stormcaller");
            loop.AllocateSubclassNode("stormcaller_3_path");
            loop.AllocateSubclassNode("stormcaller_3");

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(before * 1.15).Within(1e-9));
        }

        [Test]
        public void FreeChangeOnceADayResetsTheSubclassNodes()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("duellist");
            loop.AllocateSubclassNode("duellist_0_path");

            Assert.That(loop.ChangeSubclassFree("stormcaller", Day), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.State.Ascendancy.Subclass, Is.EqualTo("stormcaller"));
            Assert.That(loop.State.Ascendancy.Allocated, Is.Empty);
            Assert.That(loop.ChangeSubclassFree("duellist", Day + Day / 2), Is.EqualTo(SubclassResult.NotYet));
            Assert.That(loop.ChangeSubclassFree("duellist", 2 * Day), Is.EqualTo(SubclassResult.Chosen));
        }

        [Test]
        public void PaidChangeHasNoCooldown()
        {
            var loop = UnlockedTypist("garden");
            loop.ChooseSubclass("duellist");
            loop.ChangeSubclassFree("stormcaller", Day);

            Assert.That(loop.ChangeSubclassPaid("shade"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChangeSubclassPaid("duellist"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(Catalog.CrystalChangeCost, Is.GreaterThan(0));
        }

        [Test]
        public void ChangeNeedsAChosenSubclass()
        {
            var loop = UnlockedTypist("garden");

            Assert.That(loop.ChangeSubclassFree("duellist", Day), Is.EqualTo(SubclassResult.NotChosen));
            Assert.That(loop.ChangeSubclassPaid("duellist"), Is.EqualTo(SubclassResult.NotChosen));
        }

        [Test]
        public void SubclassSurvivesSaveAndLoad()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("duellist");
            loop.AllocateSubclassNode("duellist_0_path");
            loop.ChangeSubclassFree("duellist", Day);
            loop.AllocateSubclassNode("duellist_1_path");
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Ascendancy.Subclass, Is.EqualTo("duellist"));
            Assert.That(loaded.Ascendancy.Allocated, Is.EquivalentTo(new[] { "duellist_1_path" }));
            Assert.That(loaded.Ascendancy.CompletedActivities, Is.EquivalentTo(new[] { "garden", "fishing" }));
            Assert.That(loaded.Ascendancy.LastFreeChangeUnixMs, Is.EqualTo(Day));
        }

        [Test]
        public void SixthVersionSaveStartsWithoutSubclass()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v6.json"), TestWorld.Data);

            Assert.That(save.State.Ascendancy.Subclass, Is.Null);
            Assert.That(save.State.Ascendancy.Allocated, Is.Empty);
        }

        private const string Valid = @"{
  ""version"": 1,
  ""activities"": [{ ""id"": ""a"", ""name"": { ""ru"": ""А"", ""en"": ""A"" } }],
  ""change"": { ""freeEveryHours"": 24, ""crystalCost"": 100 },
  ""subclasses"": [
    { ""id"": ""s"", ""class"": ""swordsman"", ""name"": { ""ru"": ""С"", ""en"": ""S"" },
      ""nodes"": [
        { ""id"": ""s_start"", ""type"": ""start"", ""x"": 0, ""y"": 0, ""name"": { ""ru"": ""С"", ""en"": ""S"" } },
        { ""id"": ""s_small"", ""type"": ""small"", ""x"": 0, ""y"": 1, ""name"": { ""ru"": ""М"", ""en"": ""M"" } },
        { ""id"": ""s_notable"", ""type"": ""notable"", ""x"": 0, ""y"": 2, ""name"": { ""ru"": ""Н"", ""en"": ""N"" } }
      ],
      ""links"": [[""s_start"", ""s_small""], [""s_small"", ""s_notable""]] }
  ]
}";

        [Test]
        public void ValidFileLoads()
        {
            var catalog = AscendancyCatalog.Load(Valid);

            Assert.That(catalog.Subclasses.Single().Nodes.Count, Is.EqualTo(3));
        }

        [TestCase("\"type\": \"small\"", "\"type\": \"keystone\"", "unknown subclass node type 'keystone'")]
        [TestCase("\"type\": \"small\"", "\"type\": \"start\"", "subclass 's' has two start nodes")]
        [TestCase("[\"s_small\", \"s_notable\"]", "[\"s_small\", \"s_small\"]", "node 's_small' is linked to itself")]
        [TestCase(", [\"s_small\", \"s_notable\"]", "", "node 's_notable' cannot be reached from the start 's_start'")]
        [TestCase("\"id\": \"s_notable\"", "\"id\": \"s_small\"", "node id 's_small' is used twice")]
        [TestCase("\"activities\": [{ \"id\": \"a\", \"name\": { \"ru\": \"А\", \"en\": \"A\" } }]", "\"activities\": []", "at least one activity is required")]
        public void BrokenFilesAreReported(string original, string broken, string message)
        {
            var json = Valid.Replace(original, broken);

            var error = Assert.Throws<ContentException>(() => AscendancyCatalog.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void SubclassClassMustStartOnTheTree()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("\"class\": \"swordsman\"", "\"class\": \"pirate\""));

            var error = Assert.Throws<ContentException>(() => catalog.RequireClassesAndIds(TestWorld.Data.Tree))!;

            Assert.That(error.Message, Does.Contain("class 'pirate' has no start on the passive tree"));
        }

        [Test]
        public void SubclassNodeIdsMustNotRepeatTreeIds()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("s_notable", "swordsman_ring_1"));

            var error = Assert.Throws<ContentException>(() => catalog.RequireClassesAndIds(TestWorld.Data.Tree))!;

            Assert.That(error.Message, Does.Contain("already used on the passive tree"));
        }
    }
}
