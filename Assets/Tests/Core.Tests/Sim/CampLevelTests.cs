using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CampLevelTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.RecordStage = 300;
            loop.Tick();
            return loop;
        }

        private static void Stock(GameLoop loop, string building, int level)
        {
            var def = loop.CampLevels.Rules.Of(building)!;
            var amounts = loop.CampLevels.Rules.AmountsFor(level);
            for (var i = 0; i < def.Materials.Count; i++)
                loop.Camp.Add(def.Materials[i], amounts[i]);
        }

        [Test]
        public void BrokenLevelRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(CampLevelRules.FileName);
            var camp = TestWorld.Real.Camp;
            Assert.That(CampLevelRules.Load(json, camp).Buildings.Count, Is.EqualTo(6));
            Assert.Throws<ContentException>(() => CampLevelRules.Load(json.Replace("\"building\": \"pier\"", "\"building\": \"moat\""), camp));
            Assert.Throws<ContentException>(() => CampLevelRules.Load(json.Replace("\"perLevel\": 0.05", "\"perLevel\": 0.2"), camp));
            Assert.Throws<ContentException>(() => CampLevelRules.Load(json.Replace("[[10, 5], [20, 10],", "[[10], [20, 10],"), camp));
        }

        [Test]
        public void AnUpgradeTakesMaterialsAndAddsItsPerk()
        {
            var loop = Camp();
            Assert.That(loop.CampLevels.Level("garden_bed"), Is.EqualTo(1));
            Assert.That(loop.CampLevels.Upgrade("garden_bed"), Is.EqualTo(CampLevelResult.NotEnough));
            Stock(loop, "garden_bed", 2);
            Assert.That(loop.CampLevels.Upgrade("garden_bed"), Is.EqualTo(CampLevelResult.Done));
            Assert.That(loop.CampLevels.Level("garden_bed"), Is.EqualTo(2));
            Assert.That(loop.Camp.Count("herb_knockbloom"), Is.Zero);
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(loop.CampLevels.Upgrade("trophy_shelf"), Is.EqualTo(CampLevelResult.NotLevelled));
        }

        [Test]
        public void GuildBuildingsWaitForTheHallAndHighLevelsForTheGuildLevel()
        {
            var loop = Camp();
            Assert.That(loop.CampLevels.Level("pier"), Is.Zero, "no fishers' hall in the town yet");
            Assert.That(loop.CampLevels.Check("pier"), Is.EqualTo(CampLevelResult.NotOpen));
            loop.Camp.SetGuilds(new[] { "fishers" });
            for (var level = 2; level <= 3; level++)
            {
                Stock(loop, "pier", level);
                Assert.That(loop.CampLevels.Upgrade("pier"), Is.EqualTo(CampLevelResult.Done));
            }
            Stock(loop, "pier", 4);
            Assert.That(loop.CampLevels.Upgrade("pier"), Is.EqualTo(CampLevelResult.GuildTooLow));
            loop.State.Town.Reputation["fishers"] = TestWorld.Real.Guilds.Levels[4];
            Assert.That(loop.CampLevels.Upgrade("pier"), Is.EqualTo(CampLevelResult.Done));
            Stock(loop, "pier", 5);
            Assert.That(loop.CampLevels.Upgrade("pier"), Is.EqualTo(CampLevelResult.GuildTooLow));
            loop.State.Town.Reputation["fishers"] = TestWorld.Real.Guilds.Levels[7];
            Assert.That(loop.CampLevels.Upgrade("pier"), Is.EqualTo(CampLevelResult.Done));
            Assert.That(loop.CampLevels.Check("pier"), Is.EqualTo(CampLevelResult.TopLevel));
            Assert.That(loop.State.Perk(MuseumPerk.BiteFaster), Is.EqualTo(0.16).Within(1e-9));
        }

        [Test]
        public void LevelsAreSavedAndTheViewDescribesThem()
        {
            var loop = Camp();
            Stock(loop, "cauldron", 2);
            loop.CampLevels.Upgrade("cauldron");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Camp.Levels["cauldron"], Is.EqualTo(2));
            var reloaded = TestWorld.Loop(new FakeInputSource(), copy);
            Assert.That(reloaded.State.Perk(MuseumPerk.StirFaster), Is.EqualTo(0.05).Within(1e-9), "the perk comes back after loading");

            var lines = CampLevelView.Lines(loop.CampLevels, loop.Camp, TestWorld.Real.Guilds);
            var cauldron = lines.Find(line => line.Building == "cauldron")!;
            Assert.That(cauldron.Status, Is.EqualTo("уровень 2 из 5"));
            Assert.That(cauldron.Cost, Is.EqualTo("herb_clickweed: 0/20 · ruin_shard: 0/10"));
            Assert.That(lines.Find(line => line.Building == "pier")!.Status, Is.EqualTo("Постройки ещё нет в убежище"));
        }
    }
}
