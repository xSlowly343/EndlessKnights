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
            new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), state ?? new GameState { Class = HeroClass.Typist }, new StatSheet());

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
            foreach (var heroClass in new[] { "typist", "bard", "hermit" })
                Assert.That(Catalog.SubclassesOf(heroClass).Count, Is.EqualTo(3), heroClass);
        }

        [Test]
        public void OpenNotablesTakeAllPointsButNotEveryNotable()
        {
            foreach (var subclass in Catalog.Subclasses.Where(s => s.Id != "summoner"))
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
            Assert.That(loop.ChooseSubclass("berserker"), Is.EqualTo(SubclassResult.Locked));
        }

        [Test]
        public void FirstGuardianAndFirstPrestigeCountAsActivities()
        {
            var state = new GameState { Class = HeroClass.Typist, RecordStage = TestWorld.Data.Stages.BiomeLength + 1, Prestiges = 1 };

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
            Assert.That(loop.ChooseSubclass("berserker"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChooseSubclass("stenographer"), Is.EqualTo(SubclassResult.AlreadyChosen));
        }

        [Test]
        public void SubclassNodesGrowFromItsStartAndCostAPointEach()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("berserker");

            Assert.That(loop.AllocateSubclassNode("bers_carnage"), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.State.Ascendancy.Allocatable(Catalog).All(n => n.Type == TreeNodeType.Small), Is.True);
            Assert.That(loop.AllocateSubclassNode("bers_carnage_path"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateSubclassNode("bers_carnage"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateSubclassNode("bers_crave_path"), Is.EqualTo(AllocationResult.NoPoints));
            Assert.That(loop.AllocateSubclassNode("steno_shorthand_path"), Is.EqualTo(AllocationResult.UnknownNode));
        }

        [Test]
        public void NotablesWaitingForTheirMechanicAreLocked()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("berserker");
            loop.AllocateSubclassNode("bers_headsman_path");

            Assert.That(loop.AllocateSubclassNode("bers_headsman"), Is.EqualTo(AllocationResult.Locked));
        }

        [Test]
        public void SubclassNotablesChangeTheHero()
        {
            var loop = UnlockedTypist("garden", "fishing");
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse);

            loop.ChooseSubclass("berserker");
            loop.AllocateSubclassNode("bers_carnage_path");
            loop.AllocateSubclassNode("bers_carnage");

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(before * 1.4).Within(1e-9));
        }

        [Test]
        public void FreeChangeOnceADayResetsTheSubclassNodes()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("berserker");
            loop.AllocateSubclassNode("bers_carnage_path");

            Assert.That(loop.ChangeSubclassFree("stenographer", Day), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.State.Ascendancy.Subclass, Is.EqualTo("stenographer"));
            Assert.That(loop.State.Ascendancy.Allocated, Is.Empty);
            Assert.That(loop.ChangeSubclassFree("berserker", Day + Day / 2), Is.EqualTo(SubclassResult.NotYet));
            Assert.That(loop.ChangeSubclassFree("berserker", 2 * Day), Is.EqualTo(SubclassResult.Chosen));
        }

        [Test]
        public void PaidChangeHasNoCooldown()
        {
            var loop = UnlockedTypist("garden");
            loop.ChooseSubclass("berserker");
            loop.ChangeSubclassFree("stenographer", Day);

            Assert.That(loop.ChangeSubclassPaid("calligrapher"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChangeSubclassPaid("berserker"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(Catalog.CrystalChangeCost, Is.GreaterThan(0));
        }

        [Test]
        public void ChangeNeedsAChosenSubclass()
        {
            var loop = UnlockedTypist("garden");

            Assert.That(loop.ChangeSubclassFree("berserker", Day), Is.EqualTo(SubclassResult.NotChosen));
            Assert.That(loop.ChangeSubclassPaid("berserker"), Is.EqualTo(SubclassResult.NotChosen));
        }

        [Test]
        public void SubclassSurvivesSaveAndLoad()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("berserker");
            loop.AllocateSubclassNode("bers_carnage_path");
            loop.ChangeSubclassFree("berserker", Day);
            loop.AllocateSubclassNode("bers_crave_path");
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Ascendancy.Subclass, Is.EqualTo("berserker"));
            Assert.That(loaded.Ascendancy.Allocated, Is.EquivalentTo(new[] { "bers_crave_path" }));
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
    { ""id"": ""s"", ""class"": ""typist"", ""name"": { ""ru"": ""С"", ""en"": ""S"" },
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
            var catalog = AscendancyCatalog.Load(Valid.Replace("\"class\": \"typist\"", "\"class\": \"pirate\""));

            var error = Assert.Throws<ContentException>(() => catalog.RequireClassesAndIds(TestWorld.Data.Tree))!;

            Assert.That(error.Message, Does.Contain("class 'pirate' has no start on the passive tree"));
        }

        [Test]
        public void SubclassNodeIdsMustNotRepeatTreeIds()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("s_notable", "t_pen_1"));

            var error = Assert.Throws<ContentException>(() => catalog.RequireClassesAndIds(TestWorld.Data.Tree))!;

            Assert.That(error.Message, Does.Contain("already used on the passive tree"));
        }
    }
}
