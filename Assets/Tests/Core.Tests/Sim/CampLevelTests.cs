using System.Collections.Generic;
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
        public void InAPartyTheStationsOfTheSharedHideoutCountAndTheOwnOnesWait()
        {
            var loop = Camp();
            Stock(loop, "garden_bed", 2);
            Stock(loop, "garden_bed", 3);
            loop.CampLevels.Upgrade("garden_bed");
            loop.CampLevels.Upgrade("garden_bed");
            Assert.That(loop.CampLevels.Level("garden_bed"), Is.EqualTo(3));
            var own = (double[])loop.State.CampPerks.Clone();

            loop.Hideout.Use(1, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            loop.Hideout.UseStations(new Dictionary<string, int> { ["garden_bed"] = 2 }, new Dictionary<string, string>());
            Assert.That(loop.CampLevels.Level("garden_bed"), Is.EqualTo(2), "the party's beds are level 2");
            Assert.That(loop.CampLevels.Level("cauldron"), Is.EqualTo(1), "an untouched party station starts at level 1");
            Assert.That(loop.State.CampPerks, Is.Not.EqualTo(own));
            Stock(loop, "garden_bed", 3);
            Assert.That(loop.CampLevels.Upgrade("garden_bed"), Is.EqualTo(CampLevelResult.PartyHouse), "the party levels its stations from the storage");
            Assert.That(CampLevelView.Lines(loop.CampLevels, loop.Camp, TestWorld.Real.Guilds).Find(line => line.Building == "garden_bed")!.Status,
                Is.EqualTo(Texts.Format("campLevel.party", 2, loop.CampLevels.Rules.MaxLevel)));

            loop.Hideout.Leave();
            Assert.That(loop.CampLevels.Level("garden_bed"), Is.EqualTo(3), "leaving the party brings the own stations back as they were");
            Assert.That(loop.State.CampPerks, Is.EqualTo(own));
        }

        [Test]
        public void ThePartyStationLinesShowTheStorageAndOnlyTheLeaderCanLevel()
        {
            var loop = Camp();
            var hideout = loop.Hideout;
            Assert.That(HideoutView.PartyStations(hideout, loop.CampLevels), Is.Empty, "no party, no party stations");
            hideout.Use(3, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            hideout.UseStations(new Dictionary<string, int> { ["garden_bed"] = 2 }, new Dictionary<string, string>());
            var def = loop.CampLevels.Rules.Of("garden_bed")!;
            var amounts = loop.CampLevels.Rules.AmountsFor(3);
            var storage = new Dictionary<string, long>();
            for (var i = 0; i < def.Materials.Count; i++)
                storage[def.Materials[i]] = amounts[i];
            hideout.UseCommons(false, System.Array.Empty<SharedBed>(), new Dictionary<string, long>(), true, storage, System.Array.Empty<StorageEntry>(), 0, 20);
            var line = HideoutView.PartyStations(hideout, loop.CampLevels).Find(entry => entry.Building == "garden_bed");
            Assert.That(line.Level, Is.EqualTo(2));
            Assert.That(line.CanUpgrade, Is.False, "only the leader spends the storage");
            hideout.UseBuild(new Dictionary<string, int>(), System.Array.Empty<string>(), string.Empty, 0, 0, 0, 0, 0, true, new Dictionary<string, int>(), System.Array.Empty<string>());
            line = HideoutView.PartyStations(hideout, loop.CampLevels).Find(entry => entry.Building == "garden_bed");
            Assert.That(line.CanUpgrade, Is.True);
            Assert.That(line.Cost, Does.Contain(def.Materials[0]));
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
            Assert.That(lines.Find(line => line.Building == "pier")!.Status, Is.EqualTo("Постройки ещё нет в Пристанище"));
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
