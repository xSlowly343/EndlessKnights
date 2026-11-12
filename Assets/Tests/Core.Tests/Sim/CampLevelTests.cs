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
            var loop = TestWorld.RealLoop();
            loop.State.RecordStage = 300;
            loop.State.Camp.Tier = 3;
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
            Assert.Throws<ContentException>(() => CampLevelRules.Load(json.Replace("\"amounts\": [\n    [\n      10,\n      5\n    ],", "\"amounts\": [\n    [\n      10\n    ],"), camp));
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
    

        private static void RaiseTo(GameLoop loop, string building, int level)
        {
            for (var next = loop.CampLevels.Level(building) + 1; next <= level; next++)
            {
                Stock(loop, building, next);
                Assert.That(loop.CampLevels.Upgrade(building), Is.EqualTo(CampLevelResult.Done));
            }
        }

        [Test]
        public void ABranchOpensAtTheThirdLevelAndChangingItCostsMaterials()
        {
            var loop = Camp();
            Assert.That(loop.CampLevels.Specialize("garden_bed", "wild"), Is.EqualTo(CampLevelResult.LevelTooLow));
            RaiseTo(loop, "garden_bed", 3);
            var grow = loop.State.Perk(MuseumPerk.GrowFaster);
            Assert.That(loop.CampLevels.Specialize("garden_bed", "moat"), Is.EqualTo(CampLevelResult.NoSuchBranch));
            Assert.That(loop.CampLevels.Specialize("garden_bed", "greenhouse"), Is.EqualTo(CampLevelResult.Done), "the first choice is free");
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.EqualTo(grow + 0.1).Within(1e-9));
            Assert.That(loop.CampLevels.Specialize("garden_bed", "greenhouse"), Is.EqualTo(CampLevelResult.SameBranch));
            Assert.That(loop.CampLevels.Specialize("garden_bed", "wild"), Is.EqualTo(CampLevelResult.NotEnough));
            loop.Camp.Add("herb_knockbloom", 20);
            loop.Camp.Add("ore_hills", 10);
            Assert.That(loop.CampLevels.Specialize("garden_bed", "wild"), Is.EqualTo(CampLevelResult.Done));
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.EqualTo(grow).Within(1e-9));
            Assert.That(loop.State.Perk(MuseumPerk.MutationChance), Is.GreaterThanOrEqualTo(0.05));
            Assert.That(loop.Camp.Count("herb_knockbloom"), Is.Zero);
        }

        [Test]
        public void NeighboursOnTheStreetHelpEachOtherAndMovingIsFree()
        {
            var loop = Camp();
            var street = loop.CampLevels.Street;
            Assert.That(street, Is.EqualTo(loop.CampLevels.Rules.Street));
            Assert.That(loop.CampLevels.Neighbors("cauldron", "garden_bed"), Is.True);
            Assert.That(loop.State.Perk(MuseumPerk.MutationChance), Is.EqualTo(0.02).Within(1e-9));

            Assert.That(loop.CampLevels.Move("garden_bed", street.Count - 1), Is.True);
            Assert.That(loop.CampLevels.Neighbors("cauldron", "garden_bed"), Is.False);
            Assert.That(loop.State.Perk(MuseumPerk.MutationChance), Is.Zero);
            Assert.That(loop.CampLevels.Move("moat", 0), Is.False);

            Assert.That(loop.CampLevels.Neighbors("pier", "bonfire"), Is.False, "the pier is not built without the fishers' hall");
            loop.Camp.SetGuilds(new[] { "fishers" });
            loop.CampLevels.Apply();
            Assert.That(loop.CampLevels.Neighbors("pier", "bonfire"), Is.True);

            RaiseTo(loop, "cauldron", 3);
            loop.CampLevels.Specialize("cauldron", "generous");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Camp.Branches["cauldron"], Is.EqualTo("generous"));
            Assert.That(copy.Camp.Street[copy.Camp.Street.Count - 1], Is.EqualTo("garden_bed"));
        }
    }
}
