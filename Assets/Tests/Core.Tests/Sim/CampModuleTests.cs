using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CampModuleTests
    {
        private static CampModuleRules Rules => TestWorld.Real.CampModules;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static GameLoop Refuge()
        {
            var loop = TestWorld.RealLoop();
            loop.State.RecordStage = 300;
            loop.State.Camp.Tier = 3;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "smiths" });
            loop.CampLevels.Apply();
            return loop;
        }

        private static void Stock(GameLoop loop, string module)
        {
            foreach (var material in Rules.Of(module)!.Materials)
                loop.Camp.Add(material.Item, material.Count);
        }

        private static void LevelUp(GameLoop loop, string building, int level)
        {
            var def = loop.CampLevels.Rules.Of(building)!;
            for (var next = loop.CampLevels.Level(building) + 1; next <= level; next++)
            {
                var amounts = loop.CampLevels.Rules.AmountsFor(next);
                for (var i = 0; i < def.Materials.Count; i++)
                    loop.Camp.Add(def.Materials[i], amounts[i]);
                Assert.That(loop.CampLevels.Upgrade(building), Is.EqualTo(CampLevelResult.Done));
            }
        }

        [Test]
        public void BrokenModuleRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(CampModuleRules.FileName);
            var levels = TestWorld.Real.CampLevels;
            Assert.That(CampModuleRules.Load(json, levels).Modules.Count, Is.EqualTo(18));
            Assert.Throws<ContentException>(() => CampModuleRules.Load(json.Replace("\"building\": \"pier\"", "\"building\": \"trophy_shelf\""), levels));
            Assert.Throws<ContentException>(() => CampModuleRules.Load(json.Replace("\"sockets\": [1, 1, 2, 2, 3]", "\"sockets\": [1, 2, 1, 2, 3]"), levels));
            Assert.Throws<ContentException>(() => CampModuleRules.Load(json.Replace("\"sockets\": [1, 1, 2, 2, 3]", "\"sockets\": [1, 1, 2, 3]"), levels));
            Assert.Throws<ContentException>(() => CampModuleRules.Load(json.Replace("\"id\": \"whetstone\"", "\"id\": \"iron_tripod\""), levels));
            Assert.Throws<ContentException>(() => CampModuleRules.Load(json.Replace("\"perk\": \"reelEasier\"", "\"perk\": \"damage\""), levels));
        }

        [Test]
        public void AModuleIsMadeAtTheForgeAndItsPerkCountsOnlyWhenInstalled()
        {
            var loop = Refuge();
            Assert.That(loop.Modules.Craft("copper_kettle"), Is.EqualTo(CampModuleResult.NotEnough));
            Stock(loop, "copper_kettle");
            Assert.That(loop.Modules.Craft("copper_kettle"), Is.EqualTo(CampModuleResult.Done));
            Assert.That(loop.Camp.Count("ore_hills"), Is.Zero);
            Assert.That(loop.Modules.Craft("copper_kettle"), Is.EqualTo(CampModuleResult.AlreadyMade));
            Assert.That(loop.State.Perk(MuseumPerk.DoublePotion), Is.Zero);

            Assert.That(loop.Modules.Install("copper_kettle", "forge", 0), Is.EqualTo(CampModuleResult.WrongBuilding));
            Assert.That(loop.Modules.Install("copper_kettle", "cauldron", 1), Is.EqualTo(CampModuleResult.NoSuchSocket));
            Assert.That(loop.Modules.Install("copper_kettle", "cauldron", 0), Is.EqualTo(CampModuleResult.Done));
            Assert.That(loop.State.Perk(MuseumPerk.DoublePotion), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(loop.Modules.Remove("copper_kettle"), Is.True);
            Assert.That(loop.State.Perk(MuseumPerk.DoublePotion), Is.Zero);
        }

        [Test]
        public void WithoutAForgeNothingIsMade()
        {
            var loop = TestWorld.RealLoop();
            loop.State.RecordStage = 1;
            loop.Tick();
            Stock(loop, "copper_kettle");
            Assert.That(loop.Modules.Craft("copper_kettle"), Is.EqualTo(CampModuleResult.ForgeClosed));
        }

        [Test]
        public void HigherLevelsOpenMoreSocketsAndANewModuleReplacesTheOldOne()
        {
            var loop = Refuge();
            Assert.That(loop.Modules.Sockets("garden_bed"), Is.EqualTo(1));
            loop.State.Camp.Blueprints.Add("bee_hive");
            loop.State.Camp.Blueprints.Add("seed_sieve");
            foreach (var id in new[] { "copper_can", "bee_hive", "seed_sieve" })
            {
                Stock(loop, id);
                Assert.That(loop.Modules.Craft(id), Is.EqualTo(CampModuleResult.Done));
            }
            loop.Modules.Install("copper_can", "garden_bed", 0);
            Assert.That(loop.Modules.Install("bee_hive", "garden_bed", 0), Is.EqualTo(CampModuleResult.Done));
            Assert.That(loop.Modules.SocketOf("copper_can"), Is.EqualTo(-1), "the old module goes back to the shelf");
            Assert.That(loop.State.Perk(MuseumPerk.GrowFaster), Is.Zero);

            loop.State.Town.Reputation["gardeners"] = TestWorld.Real.Guilds.Levels[7];
            LevelUp(loop, "garden_bed", 5);
            Assert.That(loop.Modules.Sockets("garden_bed"), Is.EqualTo(3));
            Assert.That(loop.Modules.Install("copper_can", "garden_bed", 1), Is.EqualTo(CampModuleResult.Done));
            Assert.That(loop.Modules.Install("seed_sieve", "garden_bed", 2), Is.EqualTo(CampModuleResult.Done));
            Assert.That(loop.State.Perk(MuseumPerk.MutationChance), Is.GreaterThanOrEqualTo(0.03 - 1e-9));
            Assert.That(loop.State.Perk(MuseumPerk.SeedBack), Is.GreaterThanOrEqualTo(0.05 - 1e-9));
            Assert.That(CampModuleView.Sockets(loop.Modules, "garden_bed"), Is.EqualTo("гнёзда: 3 из 3"));
        }

        [Test]
        public void BlueprintsDropFromBossesAndTheSaveKeepsModules()
        {
            var loop = Refuge();
            Stock(loop, "wyvern_bellows");
            Assert.That(loop.Modules.Craft("wyvern_bellows"), Is.EqualTo(CampModuleResult.NoBlueprint));
            var line = CampModuleView.Lines(loop.Modules, "forge").Find(l => l.Id == "wyvern_bellows")!;
            Assert.That(line.Status, Is.EqualTo("нужен чертёж, он падает с боссов"));

            var json = TestData.ReadGameDataFile(CampModuleRules.FileName).Replace("\"blueprintChance\": 0.004", "\"blueprintChance\": 1");
            loop.Modules.UseRules(CampModuleRules.Load(json, TestWorld.Real.CampLevels));
            var blueprints = 0;
            foreach (var def in Rules.Modules)
            {
                if (def.Blueprint)
                    blueprints++;
            }
            for (var i = 0; i < blueprints + 3; i++)
            {
                var first = loop.Events.Count;
                loop.Events.Add(GameEvent.ForStage(GameEventKind.BossDefeated, 0, 1));
                loop.Modules.NoteBosses(first);
            }
            Assert.That(loop.State.Camp.Blueprints.Count, Is.EqualTo(blueprints), "every blueprint drops once and then the drops stop");
            Assert.That(loop.Modules.Craft("wyvern_bellows"), Is.EqualTo(CampModuleResult.Done));
            Assert.That(loop.Modules.Install("wyvern_bellows", "forge", 0), Is.EqualTo(CampModuleResult.Done));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Camp.Modules, Does.Contain("wyvern_bellows"));
            Assert.That(copy.Camp.Blueprints.Count, Is.EqualTo(blueprints));
            Assert.That(copy.Camp.Installed["wyvern_bellows"], Is.EqualTo(0));
        }
    
        [Test]
        public void InAPartyTheModulesOfThePartyCountAndTheOwnOnesWait()
        {
            var loop = Refuge();
            var modules = loop.Modules;
            var free = modules.Rules.Modules.First(def => !def.Blueprint && def.Building == "bonfire");
            foreach (var material in free.Materials)
                loop.Camp.Add(material.Item, material.Count);
            Assert.That(modules.Craft(free.Id), Is.EqualTo(CampModuleResult.Done));
            Assert.That(modules.Install(free.Id, "bonfire", 0), Is.EqualTo(CampModuleResult.Done));
            var own = (double[])loop.State.CampPerks.Clone();

            loop.Hideout.Use(1, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            loop.Hideout.UseStations(new Dictionary<string, int>(), new Dictionary<string, string>(), new Dictionary<string, int>());
            Assert.That(modules.Made(free.Id), Is.False, "the party has not made it");
            Assert.That(loop.State.CampPerks, Is.Not.EqualTo(own));
            Assert.That(modules.Install(free.Id, "bonfire", 0), Is.EqualTo(CampModuleResult.NotMade));
            loop.Hideout.UseStations(new Dictionary<string, int>(), new Dictionary<string, string>(), new Dictionary<string, int> { [free.Id] = 0 });
            Assert.That(modules.InSocket("bonfire", 0), Is.EqualTo(free.Id));
            Assert.That(loop.State.CampPerks, Is.EqualTo(own), "the same module in the party's socket gives the same perk");
            Assert.That(modules.Remove(free.Id), Is.False, "only the leader changes the party's sockets, through the server");
            loop.Hideout.UseStations(new Dictionary<string, int>(), new Dictionary<string, string>(), new Dictionary<string, int> { [free.Id] = CampModuleSystem.Stored });
            Assert.That(modules.InSocket("bonfire", 0), Is.Empty);
            Assert.That(modules.Made(free.Id), Is.True);

            loop.Hideout.Leave();
            Assert.That(modules.SocketOf(free.Id), Is.Zero, "the own module is back in the own socket");
            Assert.That(loop.State.CampPerks, Is.EqualTo(own));
        }
}
}
