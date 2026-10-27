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
            new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), state ?? new GameState { Class = HeroClass.Ronin }, new StatSheet());

        private static SubclassDef Subclass(string id) => Catalog.Subclasses.Single(s => s.Id == id);

        private static TreeNodeDef NotableAtDepth(string subclass, int depth) =>
            Subclass(subclass).Nodes.Where(n => n.Type == TreeNodeType.Notable).OrderBy(n => n.Id)
                .First(n => TreePaths.SubclassPath(Subclass(subclass), n.Id).Length == depth);

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
            Assert.That(Catalog.Subclasses.Count, Is.EqualTo(42));
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
        public void ANodeComesBackOnlyWhenNoTakenNodeHangsOnIt()
        {
            var loop = UnlockedTypist(ActivityIds.FirstGuardian, ActivityIds.FirstPrestige);
            loop.ChooseSubclass("kensei");
            var path = TreePaths.SubclassPath(Subclass("kensei"), NotableAtDepth("kensei", 2).Id);
            foreach (var id in path)
                Assert.That(loop.AllocateSubclassNode(id), Is.EqualTo(AllocationResult.Allocated), id);

            Assert.That(loop.RefundSubclassNode(path[0]), Is.False);
            Assert.That(loop.RefundSubclassNode(path[1]), Is.True);
            Assert.That(loop.RefundSubclassNode(path[0]), Is.True);
            Assert.That(loop.State.Ascendancy.PointsLeft(Catalog), Is.EqualTo(2));
            Assert.That(loop.RefundSubclassNode(path[0]), Is.False);
        }

        [Test]
        public void SubclassIsLockedUntilTheFirstActivity()
        {
            var loop = TypistLoop();

            Assert.That(loop.State.Ascendancy.Unlocked(Catalog), Is.False);
            Assert.That(loop.ChooseSubclass("kensei"), Is.EqualTo(SubclassResult.Locked));
        }

        [Test]
        public void FirstGuardianAndFirstPrestigeCountAsActivities()
        {
            var state = new GameState { Class = HeroClass.Ronin, RecordStage = TestWorld.Data.Stages.BiomeLength + 1, Prestiges = 1 };

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

            Assert.That(loop.ChooseSubclass("conductor"), Is.EqualTo(SubclassResult.WrongClass));
            Assert.That(loop.ChooseSubclass("nobody"), Is.EqualTo(SubclassResult.UnknownSubclass));
            Assert.That(loop.ChooseSubclass("kensei"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChooseSubclass("ninja"), Is.EqualTo(SubclassResult.AlreadyChosen));
        }

        [Test]
        public void SubclassTreesHaveTheirOwnShapeLikeInTheOriginal()
        {
            foreach (var subclass in Catalog.Subclasses)
            {
                Assert.That(subclass.Nodes.Count(n => n.Type != TreeNodeType.Keystone && !n.Id.EndsWith("_bk_1")), Is.InRange(14, 17), subclass.Id);
                Assert.That(subclass.Nodes.Count(n => n.Type == TreeNodeType.Notable), Is.InRange(7, 8), subclass.Id);
                var depths = subclass.Nodes.Where(n => n.Type == TreeNodeType.Notable).Select(n => TreePaths.SubclassPath(subclass, n.Id).Length).Distinct().Count();
                Assert.That(depths, Is.GreaterThanOrEqualTo(3), subclass.Id);
            }
            Assert.That(Catalog.Subclasses.Select(s => string.Join(",", s.Nodes.Select(n => TreePaths.SubclassPath(s, n.Id).Length).OrderBy(d => d))).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void SubclassNodesGrowFromItsStartAndCostAPointEach()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("kensei");
            var path = TreePaths.SubclassPath(Subclass("kensei"), NotableAtDepth("kensei", 2).Id);
            var other = TreePaths.SubclassPath(Subclass("kensei"), NotableAtDepth("kensei", 3).Id);

            Assert.That(loop.AllocateSubclassNode(path[1]), Is.EqualTo(AllocationResult.NotConnected));
            Assert.That(loop.AllocateSubclassNode(path[0]), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateSubclassNode(path[1]), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.AllocateSubclassNode(other[0]), Is.EqualTo(AllocationResult.NoPoints));
            Assert.That(loop.AllocateSubclassNode(Subclass("ninja").Nodes[1].Id), Is.EqualTo(AllocationResult.UnknownNode));
        }

        [Test]
        public void NotablesWaitingForTheirMechanicCanBeLocked()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("\"id\": \"s_notable\", \"type\": \"notable\"", "\"id\": \"s_notable\", \"type\": \"notable\", \"locked\": true"));
            var state = new AscendancyState();
            state.Complete("a");
            state.Choose(catalog, "ronin", "s");

            Assert.That(state.Allocate(catalog, "s_small"), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(state.Allocate(catalog, "s_notable"), Is.EqualTo(AllocationResult.Locked));
        }

        [Test]
        public void PathKeystoneIsALeafBehindOneSmallNode()
        {
            foreach (var subclass in Catalog.Subclasses)
            {
                foreach (var keystone in subclass.Nodes.Where(n => n.Type == TreeNodeType.Keystone))
                {
                    Assert.That(keystone.Neighbours.Count, Is.EqualTo(1), keystone.Id);
                    Assert.That(TreePaths.SubclassPath(subclass, keystone.Id).Length, Is.EqualTo(2), keystone.Id);
                    Assert.That(keystone.DescriptionRu, Is.Not.Empty, keystone.Id);
                    Assert.That(keystone.DescriptionEn, Is.Not.Empty, keystone.Id);
                    Assert.That(keystone.Locked, Is.False, keystone.Id);
                }
            }
        }

        [Test]
        public void DamageDealersBuffersAndDebuffersHaveAPathKeystone()
        {
            foreach (var heroClass in new[] { "viking", "ronin", "amazon", "stormweaver", "nomad", "metalhead", "oracle", "bannerbearer", "voodoo", "clockmaker" })
            {
                foreach (var subclass in Catalog.SubclassesOf(heroClass))
                    Assert.That(subclass.Nodes.Count(n => n.Type == TreeNodeType.Keystone), Is.EqualTo(1), subclass.Id);
            }
        }

        [Test]
        public void KeystoneStatsAreReadByTheGame()
        {
            var known = typeof(CombatStats).GetFields().Select(f => ((StatId)f.GetValue(null)!).Name).ToHashSet();
            foreach (var subclass in Catalog.Subclasses)
            {
                foreach (var keystone in subclass.Nodes.Where(n => n.Type == TreeNodeType.Keystone))
                {
                    foreach (var modifier in keystone.Modifiers)
                        Assert.That(known, Does.Contain(modifier.Stat.Name), keystone.Id);
                }
            }
        }

        [Test]
        public void FateWeaverAndSongNodesAreOpen()
        {
            Assert.That(Subclass("fate_weaver").Nodes.Where(n => n.Locked), Is.Empty);
            foreach (var path in new[] { "pit_fighter", "conductor", "headliner" })
                Assert.That(Subclass(path).Nodes.Where(n => n.Locked), Is.Empty, path);
        }

        [Test]
        public void TwoKeystonesOnOnePathAreRefused()
        {
            var json = Valid.Replace("\"id\": \"s_small\", \"type\": \"small\"", "\"id\": \"s_small\", \"type\": \"keystone\"")
                .Replace("\"id\": \"s_notable\", \"type\": \"notable\"", "\"id\": \"s_notable\", \"type\": \"keystone\"");

            var error = Assert.Throws<ContentException>(() => AscendancyCatalog.Load(json))!;

            Assert.That(error.Message, Does.Contain("more than one keystone"));
        }

        [Test]
        public void DjinnKeystoneMakesTheMouseTheMainWeapon()
        {
            var keystone = Subclass("djinn").Nodes.Single(n => n.Type == TreeNodeType.Keystone);
            var path = TreePaths.SubclassPath(Subclass("djinn"), keystone.Id);
            var loop = new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), new GameState { Class = HeroClass.Nomad }, new StatSheet());
            foreach (var activity in Catalog.Activities.Take(path.Length))
                loop.CompleteActivity(activity.Id);
            loop.ChooseSubclass("djinn");
            foreach (var id in path.SkipLast(1))
                loop.AllocateSubclassNode(id);
            var mouse = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse);
            var keys = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            Assert.That(loop.AllocateSubclassNode(keystone.Id), Is.EqualTo(AllocationResult.Allocated));

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Mouse), Is.EqualTo(mouse * 3).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(keys * 0.4).Within(1e-9));
        }

        [Test]
        public void SubclassNotablesChangeTheHero()
        {
            var shadows = Subclass("ninja").Nodes.Single(n => n.NameRu == "Две тени у края");
            var path = TreePaths.SubclassPath(Subclass("ninja"), shadows.Id);
            var loop = UnlockedTypist(Catalog.Activities.Take(path.Length).Select(a => a.Id).ToArray());
            var before = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            loop.ChooseSubclass("ninja");
            foreach (var id in path.SkipLast(1))
                loop.AllocateSubclassNode(id);
            var beforeNotable = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            Assert.That(loop.AllocateSubclassNode(shadows.Id), Is.EqualTo(AllocationResult.Allocated));

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(beforeNotable * 1.2).Within(1e-9));
            Assert.That(beforeNotable, Is.GreaterThanOrEqualTo(before));
        }

        [Test]
        public void FreeChangeOnceADayResetsTheSubclassNodes()
        {
            var loop = UnlockedTypist("garden", "fishing");
            loop.ChooseSubclass("kensei");
            loop.AllocateSubclassNode(TreePaths.SubclassPath(Subclass("kensei"), NotableAtDepth("kensei", 2).Id)[0]);

            Assert.That(loop.ChangeSubclassFree("ninja", Day), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.State.Ascendancy.Subclass, Is.EqualTo("ninja"));
            Assert.That(loop.State.Ascendancy.Allocated, Is.Empty);
            Assert.That(loop.ChangeSubclassFree("kensei", Day + Day / 2), Is.EqualTo(SubclassResult.NotYet));
            Assert.That(loop.ChangeSubclassFree("kensei", 2 * Day), Is.EqualTo(SubclassResult.Chosen));
        }

        [Test]
        public void PaidChangeHasNoCooldown()
        {
            var loop = UnlockedTypist("garden");
            loop.ChooseSubclass("kensei");
            loop.ChangeSubclassFree("ninja", Day);

            Assert.That(loop.ChangeSubclassPaid("onryo"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(loop.ChangeSubclassPaid("kensei"), Is.EqualTo(SubclassResult.Chosen));
            Assert.That(Catalog.CrystalChangeCost, Is.GreaterThan(0));
        }

        [Test]
        public void ChangeNeedsAChosenSubclass()
        {
            var loop = UnlockedTypist("garden");

            Assert.That(loop.ChangeSubclassFree("kensei", Day), Is.EqualTo(SubclassResult.NotChosen));
            Assert.That(loop.ChangeSubclassPaid("kensei"), Is.EqualTo(SubclassResult.NotChosen));
        }

        [Test]
        public void SubclassSurvivesSaveAndLoad()
        {
            var loop = UnlockedTypist("garden", "fishing");
            var second = TreePaths.SubclassPath(Subclass("kensei"), NotableAtDepth("kensei", 3).Id)[0];
            loop.ChooseSubclass("kensei");
            loop.AllocateSubclassNode(TreePaths.SubclassPath(Subclass("kensei"), NotableAtDepth("kensei", 2).Id)[0]);
            loop.ChangeSubclassFree("kensei", Day);
            loop.AllocateSubclassNode(second);
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Ascendancy.Subclass, Is.EqualTo("kensei"));
            Assert.That(loaded.Ascendancy.Allocated, Is.EquivalentTo(new[] { second }));
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
    { ""id"": ""s"", ""class"": ""ronin"", ""name"": { ""ru"": ""С"", ""en"": ""S"" },
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

        [TestCase("\"type\": \"small\"", "\"type\": \"mastery\"", "unknown subclass node type 'mastery'")]
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
            var catalog = AscendancyCatalog.Load(Valid.Replace("\"class\": \"ronin\"", "\"class\": \"pirate\""));

            var error = Assert.Throws<ContentException>(() => catalog.RequireClassesAndIds(TestWorld.Data.Tree))!;

            Assert.That(error.Message, Does.Contain("class 'pirate' has no start on the passive tree"));
        }

        [Test]
        public void SubclassNodeIdsMustNotRepeatTreeIds()
        {
            var catalog = AscendancyCatalog.Load(Valid.Replace("s_notable", "ronin_ring_1"));

            var error = Assert.Throws<ContentException>(() => catalog.RequireClassesAndIds(TestWorld.Data.Tree))!;

            Assert.That(error.Message, Does.Contain("already used on the passive tree"));
        }
    }
}
