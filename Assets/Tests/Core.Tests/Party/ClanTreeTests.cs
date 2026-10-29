using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class ClanTreeTests
    {
        private const long Now = 1_800_000_000_000;
        private const long Hour = 3_600_000;

        private static ClanTreeCatalog Catalog => TestWorld.Data.ClanTree;

        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [Test]
        public void TheGameFileLoadsWithAStartAndNamesInBothLanguages()
        {
            var catalog = ClanTreeCatalog.Load(TestData.ReadGameDataFile(ClanTreeCatalog.FileName));

            Assert.That(catalog.All, Is.Not.Empty);
            Assert.That(catalog.TryGet("common_hearth", out var start), Is.True);
            Assert.That(start.Requires, Is.Empty);
            foreach (var node in catalog.All)
            {
                Assert.That(node.NameRu, Is.Not.Empty, node.Id);
                Assert.That(node.NameEn, Is.Not.Empty, node.Id);
                Assert.That(node.Modifiers, Is.Not.Empty, node.Id);
            }
        }

        [TestCase(@"""requires"": [""common_hearth""]", @"""requires"": [""nowhere""]", "unknown node")]
        [TestCase(@"""id"": ""war_drums""", @"""id"": ""common_hearth""", "listed twice")]
        [TestCase(@"""cost"": 300", @"""cost"": 0", "cost")]
        public void BadTreesAreRejected(string from, string to, string message)
        {
            var error = Assert.Throws<ContentException>(() => ClanTreeCatalog.Load(TestData.ReadGameDataFile(ClanTreeCatalog.FileName).Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }

        [Test]
        public void OpenNodesGiveEveryMemberTheirBonusUntilTheyLeaveTheClan()
        {
            var loop = TestWorld.Loop(new Game.Core.Input.FakeInputSource());
            var before = loop.Hero.Value(CombatStats.Gold, 1);

            loop.ClanTree.Use(new[] { "common_hearth", "war_drums", "unknown_from_a_newer_server" }, Now);

            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(before * 1.03).Within(1e-9));
            Assert.That(loop.ClanTree.Applied.Count, Is.EqualTo(2), "nodes the game does not know yet are skipped");
            loop.ClanTree.Clear(Now + 1000);
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(before).Within(1e-9));
            Assert.That(loop.ClanTree.Taken, Is.Empty);
            Assert.That(loop.ClanTree.Active, Is.False);
        }

        [Test]
        public void SavedBonusesWorkWithoutTheServerForTheHoursOfTheFileAndThenFade()
        {
            var state = new GameState();
            TestWorld.Loop(new Game.Core.Input.FakeInputSource(), state: state).ClanTree.Use(new[] { "common_hearth" }, Now);
            var loaded = SaveCodec.Decode(SaveCodec.Encode(state, new ClockStamp(new ClockReading(Now, 0, 1), Now)), TestWorld.Data).State;
            var loop = TestWorld.Loop(new Game.Core.Input.FakeInputSource(), state: loaded);
            var plain = loop.Hero.Value(CombatStats.Gold, 1);
            var hours = Catalog.OfflineHours;

            Assert.That(hours, Is.EqualTo(24));
            Assert.That(loop.ClanTree.Taken, Is.EqualTo(new[] { "common_hearth" }));
            Assert.That(loop.ClanTree.Applied, Is.Empty, "nothing is applied before the game checks the clock");
            Assert.That(loop.ClanTree.Refresh(Now + (hours - 1) * Hour), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(plain * 1.03).Within(1e-9));
            Assert.That(loop.ClanTree.Refresh(Now + hours * Hour + 1), Is.False);
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(plain).Within(1e-9));
            Assert.That(loop.ClanTree.Taken, Is.EqualTo(new[] { "common_hearth" }), "the nodes stay saved until the server answers");
            Assert.That(loop.ClanTree.Refresh(Now), Is.False, "turning the clock back does not bring the bonus back");

            loop.ClanTree.Use(new[] { "common_hearth" }, Now + 30 * Hour);

            Assert.That(loop.ClanTree.Active, Is.True, "an answer of the server lights the bonus again");
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(plain * 1.03).Within(1e-9));
        }

        [Test]
        public void RefreshingAFreshBonusAddsItOnce()
        {
            var loop = TestWorld.Loop(new Game.Core.Input.FakeInputSource());
            var before = loop.Hero.Value(CombatStats.Gold, 1);

            loop.ClanTree.Use(new[] { "common_hearth" }, Now);
            loop.ClanTree.Refresh(Now + Hour);
            loop.ClanTree.Refresh(Now + 2 * Hour);
            loop.ClanTree.UseCatalog(Catalog);

            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(before * 1.03).Within(1e-9));
        }

        [Test]
        public void NodesReadPlainly()
        {
            Catalog.TryGet("war_drums", out var drums);

            Assert.That(ClanTreeText.Name(drums), Is.EqualTo("Боевые барабаны"));
            Assert.That(ClanTreeText.State(drums, false, true), Is.EqualTo("600 Печатей"));
            Assert.That(ClanTreeText.State(drums, true, true), Is.EqualTo("открыт"));
            Assert.That(ClanTreeText.Effect(drums), Is.Not.Empty);
            Assert.That(ClanTreeText.Treasury(1240), Is.EqualTo("Казна клана: 1240 Печатей"));
            Assert.That(ClanTreeText.Log("bought", "Аня", 600, "Боевые барабаны"), Is.EqualTo("Аня: открыт узел «Боевые барабаны» за 600"));
            Assert.That(ClanTreeText.Failed("not enough seals"), Is.EqualTo("Не вышло: в казне не хватает Печатей"));
            Texts.UseLanguage("en");
            Assert.That(ClanTreeText.Name(drums), Is.EqualTo("War Drums"));
        }
    }
}
