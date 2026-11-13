using System.Linq;
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
    public class HideoutTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void TheBaseTiersAndTheirBuildingsComeFromTheRules()
        {
            var rules = TestWorld.Real.Hideout;
            Assert.That(rules.Tiers.Count, Is.EqualTo(9));
            var json = TestData.ReadGameDataFile(HideoutRules.FileName);
            Assert.That(rules.Building("corner")!.Personal, Is.True);
            Assert.That(rules.Building("pier")!.Guild, Is.EqualTo("fishers"));
            Assert.That(rules.Building("bonfire")!.Levels.Count, Is.EqualTo(3));
            Assert.Throws<ContentException>(() => HideoutRules.Load(json.Replace("\"counter\": \"partyTrips\"", "\"counter\": \"dragons\"")));
            Assert.Throws<ContentException>(() => HideoutRules.Load(json.Replace("\"levelTiers\": [\n    0,\n    2,\n    5\n  ]", "\"levelTiers\": [\n    0,\n    4,\n    5\n  ]")));
            Assert.That(rules.Tiers[0].NameRu, Is.EqualTo("Привал"));
            Assert.That(rules.Tiers[1].SitHours, Is.EqualTo(2));
            Assert.Throws<ContentException>(() => HideoutRules.Load(json.Replace("\"opens\": [\n        \"garden\",", "\"opens\": [\n        \"moat\",")));
            Assert.Throws<ContentException>(() => HideoutRules.Load(json.Replace("\"bosses\": 50,", "\"bosses\": 5,")));
        }

        [Test]
        public void TheServerAnswerSetsTheTierBuildingsAndWhoIsByTheFire()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            Assert.That(HideoutView.TierName(hideout), Is.EqualTo("Пустой Привал — нужен отряд из двоих"));
            Assert.That(HideoutView.NextTier(hideout), Has.Member("в отряде 0 из 2"));

            hideout.Use(1, 2, 4, 0, 1800, 0, new[] { "Ann", "Bob" });
            Assert.That(HideoutView.TierName(hideout), Is.EqualTo("Привал"));
            Assert.That(HideoutView.Buildings(hideout), Is.EqualTo(new[] { "Костёр", "Уголок участника", "Уголок участника", "Стол Хроники" }));
            Assert.That(HideoutView.AtTheFire(hideout.State), Is.EqualTo("У костра: Ann, Bob"));
            var next = HideoutView.NextTier(hideout);
            Assert.That(next[0], Is.EqualTo("До ступени «Двор»:"));
            Assert.That(next, Has.Member("боссов вместе 4 из 10"));
            Assert.That(next, Has.Member("у костра вместе 0.5 из 2 ч"));

            hideout.Use(2, 2, 12, 0, 7300, 0, System.Array.Empty<string>());
            Assert.That(HideoutView.NextTier(hideout), Has.Member("заказов отряда 0 из 1"));
            Assert.That(HideoutView.NextTier(hideout), Has.Member("костёр 2-го уровня"));
            Assert.That(HideoutView.AtTheFire(hideout.State), Is.EqualTo("У костра никого"));

            hideout.Use(3, 2, 60, 1, 7300, 2, System.Array.Empty<string>());
            Assert.That(HideoutView.NextTier(hideout), Has.Member("сборов с общей грядки: 20"));
            hideout.Use(9, 2, 6000, 30, 7300, 3, System.Array.Empty<string>());
            Assert.That(hideout.NextTier, Is.Null);
            Assert.That(HideoutView.NextTier(hideout), Is.Empty);
        }

        [Test]
        public void InThePartyHideoutTheActivityAndTheSharedWorkBothCount()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetPlace(PeacefulPlace.Hideout);
            Assert.That(loop.Hideout.Inside, Is.False, "the own hideout before the party");
            loop.Hideout.Use(1, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            Assert.That(loop.Hideout.Inside, Is.True);
            input.PressKeys(6);
            loop.Tick();
            Assert.That(loop.State.Peaceful.HideoutWork.Keys, Is.EqualTo(6));
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(6));

            loop.Hideout.Leave();
            input.PressKeys(4);
            loop.Tick();
            Assert.That(loop.Hideout.Inside, Is.False, "without a party the hideout is the player's own");
            Assert.That(loop.State.Peaceful.Place, Is.EqualTo(PeacefulPlace.Hideout));
            Assert.That(loop.Guilds.UnsentActions, Is.EqualTo(10), "the chosen activity keeps counting in the own hideout");
            Assert.That(loop.State.Peaceful.HideoutWork.Keys, Is.EqualTo(6), "the shared work waits for the party");
        }

        [Test]
        public void TheSaveKeepsThePlaceAndTheLastAnswerAndLeavingClearsThem()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetPlace(PeacefulPlace.Hideout);
            loop.Hideout.Use(2, 3, 14, 0, 7300, 0, new[] { "Ann" });
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.Place, Is.EqualTo(PeacefulPlace.Hideout));
            Assert.That(copy.Hideout.Tier, Is.EqualTo(2));
            Assert.That(copy.Hideout.SitSeconds, Is.EqualTo(7300));
            Assert.That(copy.Hideout.Here, Is.Empty, "who is by the fire is only known online");

            loop.Hideout.Leave();
            Assert.That(loop.Hideout.State.Tier, Is.Zero);
            Assert.That(loop.State.Peaceful.Place, Is.EqualTo(PeacefulPlace.Hideout), "leaving the party keeps the player in their own hideout");
        }
    

        [Test]
        public void TheBuildPlanShowsLevelsTheProjectAndWants()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            hideout.Use(2, 2, 12, 0, 7300, 1, System.Array.Empty<string>());
            hideout.UseBuild(new System.Collections.Generic.Dictionary<string, int> { ["bonfire"] = 1 }, new[] { "bonfire", "board" }, string.Empty, 0, 0, 0, 0, 3, true,
                new System.Collections.Generic.Dictionary<string, int> { ["board"] = 2 }, new[] { "board" });
            var plan = HideoutView.Plan(hideout);
            Assert.That(plan.ConvertAll(line => line.Id), Is.EqualTo(new[] { "bonfire", "chronicle_table", "board" }), "corners are personal and not in the plan");
            var board = plan.Find(line => line.Id == "board")!;
            Assert.That(board.CanQueue, Is.True);
            Assert.That(board.Wants, Is.EqualTo(2));
            Assert.That(board.IWant, Is.True);
            Assert.That(board.Status, Is.EqualTo("следующий уровень: 120 очков материалов, 2 ч боя вместе"));
            Assert.That(plan.Find(line => line.Id == "chronicle_table")!.Status, Is.EqualTo("ждёт ступени или здания в Кромке"));
            Assert.That(HideoutView.Project(hideout), Is.EqualTo("Выбери, что строить дальше"));

            hideout.UseBuild(new System.Collections.Generic.Dictionary<string, int> { ["bonfire"] = 1 }, System.Array.Empty<string>(), "board", 1, 40, 120, 2, 1, false,
                new System.Collections.Generic.Dictionary<string, int>(), System.Array.Empty<string>());
            Assert.That(HideoutView.Project(hideout), Is.EqualTo("Строим «Доска заказов» (1 ур.): материалы 40 из 120, бой вместе 1 из 2 ч"));
            Assert.That(HideoutView.Plan(hideout).Find(line => line.Id == "board")!.Status, Is.EqualTo("строится"));
            Assert.That(HideoutView.Plan(hideout).TrueForAll(line => !line.CanQueue), Is.True, "only the leader queues");
        }

        [Test]
        public void MaterialsLeaveTheBagForTheBuildAndComeBackOnRefusal()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.State.Camp.Bag["herb_windwort"] = 4;
            Assert.That(loop.Hideout.PackMaterial("herb_windwort", 5), Is.EqualTo(ClanCampResult.NotEnough));
            Assert.That(loop.Hideout.PackMaterial("fish_hills_roach", 1), Is.EqualTo(ClanCampResult.NotAMaterial));
            Assert.That(loop.Hideout.PackMaterial("herb_windwort", 4), Is.EqualTo(ClanCampResult.Done));
            Assert.That(loop.State.Camp.Bag.ContainsKey("herb_windwort"), Is.False);
            loop.Hideout.ReturnMaterial("herb_windwort", 4);
            Assert.That(loop.State.Camp.Bag["herb_windwort"], Is.EqualTo(4));

            loop.Hideout.UseBuild(new System.Collections.Generic.Dictionary<string, int> { ["bonfire"] = 2 }, System.Array.Empty<string>(), string.Empty, 0, 0, 0, 0, 0, false,
                new System.Collections.Generic.Dictionary<string, int>(), System.Array.Empty<string>());
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Hideout.Buildings["bonfire"], Is.EqualTo(2));
        }
    
        [Test]
        public void DecorMovesToTheCornerAndComesHomeWhenThePlayerLeaves()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Tick();
            var hideout = loop.Hideout;
            Assert.That(loop.Decor.Place(0, "decor_lantern"), Is.EqualTo(DecorResult.Done));
            Assert.That(hideout.PutInCorner("decor_lantern"), Is.EqualTo(CornerResult.NoCorner), "no party, no corner");
            hideout.Use(1, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            hideout.UseCorners(true, new[] { new HideoutCorner("Ann", "Huscarl", "", new[] { "decor_lantern" }) });
            Assert.That(hideout.CornerDecor, Is.EqualTo(hideout.Rules.CornerDecor));
            Assert.That(hideout.PutInCorner("decor_lantern"), Is.EqualTo(CornerResult.Done));
            Assert.That(loop.State.Decor.Placed, Has.No.Member("decor_lantern"), "the lantern left the refuge street");
            Assert.That(hideout.PutInCorner("decor_brazier"), Is.EqualTo(CornerResult.NotOwned));
            Assert.That(hideout.PutInCorner("decor_nothing"), Is.EqualTo(CornerResult.Unknown));
            Assert.That(hideout.PutInCorner("decor_bench"), Is.EqualTo(CornerResult.Done));
            Assert.That(HideoutView.MyCorner(hideout), Does.Contain("2"));
            Assert.That(HideoutView.Corners(hideout.State), Has.Count.EqualTo(1));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Decor.Corner, Is.EqualTo(new[] { "decor_lantern", "decor_bench" }));

            Assert.That(hideout.TakeFromCorner("decor_bench"), Is.EqualTo(CornerResult.Done));
            Assert.That(loop.State.Decor.Placed, Has.Member("decor_bench"));
            Assert.That(hideout.TakeFromCorner("decor_bench"), Is.EqualTo(CornerResult.NotThere));

            hideout.UseCorners(false, System.Array.Empty<HideoutCorner>());
            Assert.That(hideout.Corner, Is.Empty);
            Assert.That(loop.State.Decor.Placed, Has.Member("decor_lantern"), "a player who left gets the decor back");
        }

        [Test]
        public void TheCornerIsFullAtThreeAndTheStrongholdGivesFive()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            var rules = hideout.Rules;
            var stronghold = 0;
            while (stronghold < rules.Tiers.Count && rules.Tiers[stronghold].CornerDecor == 0)
                stronghold++;
            Assert.That(rules.CornerDecorAt(stronghold), Is.EqualTo(3));
            Assert.That(rules.CornerDecorAt(stronghold + 1), Is.EqualTo(5));
            Assert.That(rules.FrozenDays, Is.EqualTo(30));
            hideout.Use(1, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            hideout.UseCorners(true, System.Array.Empty<HideoutCorner>());
            foreach (var id in new[] { "decor_lantern", "decor_bench", "decor_brazier" })
                loop.State.Decor.Owned.Add(id);
            loop.State.Decor.Owned.Add("decor_ice_statue");
            Assert.That(hideout.PutInCorner("decor_lantern"), Is.EqualTo(CornerResult.Done));
            Assert.That(hideout.PutInCorner("decor_bench"), Is.EqualTo(CornerResult.Done));
            Assert.That(hideout.PutInCorner("decor_brazier"), Is.EqualTo(CornerResult.Done));
            Assert.That(hideout.PutInCorner("decor_ice_statue"), Is.EqualTo(CornerResult.Full));
            Assert.That(loop.Decor.Place(0, "decor_brazier"), Is.EqualTo(DecorResult.Done));
            Assert.That(hideout.Corner, Has.No.Member("decor_brazier"), "placing it at home takes it out of the corner");
            hideout.Leave();
            Assert.That(hideout.Corner, Is.Empty);
            Assert.That(hideout.CornerOpen, Is.False);
        }
    
        [Test]
        public void InputInsideTheHideoutIsKeptForTheSharedGarden()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetPlace(PeacefulPlace.Hideout);
            loop.Hideout.Use(1, 2, 0, 0, 0, 0, System.Array.Empty<string>());
            input.PressKeys(5);
            input.ClickLeft(2);
            loop.Tick();
            loop.Tick();
            var work = loop.Hideout.TakeTending();
            Assert.That(work.Keys, Is.EqualTo(5));
            Assert.That(work.Clicks, Is.EqualTo(2));
            Assert.That(work.IdleSeconds, Is.GreaterThan(0));
            Assert.That(loop.Hideout.TakeTending().Keys, Is.Zero, "tending is taken once");
        }

        [Test]
        public void SeedsGoToTheSharedBedAndTheHarvestAndStorageComeBackToTheBag()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            var garden = TestWorld.Data.Garden;
            var starter = garden.Plants.First(p => p.Starter);
            var rare = garden.Plants.First(p => !p.Starter);
            Assert.That(hideout.PackSeed(starter.Id), Is.EqualTo(GardenResult.Done), "starter plants need no seed");
            Assert.That(hideout.PackSeed(rare.Id), Is.EqualTo(GardenResult.NoSeed));
            Assert.That(hideout.PackSeed("nothing"), Is.EqualTo(GardenResult.UnknownPlant));
            loop.Camp.Add(rare.Seed, 1);
            Assert.That(hideout.PackSeed(rare.Id), Is.EqualTo(GardenResult.Done));
            Assert.That(loop.Camp.Count(rare.Seed), Is.Zero);
            hideout.ReturnSeed(rare.Id);
            Assert.That(loop.Camp.Count(rare.Seed), Is.EqualTo(1), "a refused planting gives the seed back");

            hideout.Receive(new Dictionary<string, long> { [starter.Herb] = 3 });
            Assert.That(loop.Camp.Count(starter.Herb), Is.EqualTo(3));
            Assert.That(hideout.PackGoods(starter.Herb, 4), Is.False);
            Assert.That(hideout.PackGoods(starter.Herb, 2), Is.True);
            Assert.That(loop.Camp.Count(starter.Herb), Is.EqualTo(1));

            hideout.UseCommons(true, new[] { new SharedBed(starter.Id, 0.5, false, true), new SharedBed(string.Empty, 0, false, false) },
                new Dictionary<string, long> { [starter.Herb] = 3 }, true, new Dictionary<string, long> { ["ember_coal"] = 2 },
                new[] { new StorageEntry("Ann", "ember_coal", 2) }, 5, 20);
            var lines = HideoutView.SharedGarden(hideout, garden);
            Assert.That(lines[0], Is.EqualTo(Texts.Get("hideout.garden.together")));
            Assert.That(lines[1], Does.Contain("50%"));
            Assert.That(lines, Has.Member(Texts.Get("hideout.garden.owed")));
            Assert.That(HideoutView.StorageLines(hideout.State)[0], Does.Contain("5").And.Contain("20"));
            Assert.That(HideoutView.JournalLines(hideout.State)[0], Does.Contain("Ann"));
            hideout.Leave();
            Assert.That(hideout.State.Beds, Is.Empty);
            Assert.That(HideoutView.StorageLines(hideout.State), Is.EqualTo(new[] { Texts.Get("hideout.storage.none") }));
        }
    
        [Test]
        public void BuildingMaterialsGoToTheStorageByThemselvesAboveWhatThePlayerKeeps()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            var material = "ore_hills";
            Assert.That(hideout.PointsOf(material), Is.GreaterThan(0));
            loop.Camp.Add(material, 25);
            loop.Camp.Add("fish_perch", 30);
            loop.Camp.Add("seed_colossus", 30);

            Assert.That(hideout.TakeShare(), Is.Empty, "off by default");
            hideout.SetShare(true);
            Assert.That(hideout.TakeShare(), Is.Empty, "nothing goes while the storage is not built");
            hideout.UseCommons(false, System.Array.Empty<SharedBed>(), new Dictionary<string, long>(), true, new Dictionary<string, long>(), System.Array.Empty<StorageEntry>(), 0, 20);
            var sent = hideout.TakeShare();
            Assert.That(sent, Is.EqualTo(new Dictionary<string, long> { [material] = 25 - hideout.ShareKeep }), "only building materials, the player keeps a few");
            Assert.That(loop.Camp.Count(material), Is.EqualTo(hideout.ShareKeep));
            Assert.That(loop.Camp.Count("fish_perch"), Is.EqualTo(30));
            hideout.SettleShare(sent, new Dictionary<string, long> { [material] = 5 });
            Assert.That(loop.Camp.Count(material), Is.EqualTo(25 - 5), "what the server did not take comes back");
            Assert.That(HideoutView.ShareSetting(hideout), Does.Contain(hideout.ShareKeep.ToString()));

            hideout.SetShare(true, 0);
            Assert.That(hideout.TakeShare()[material], Is.EqualTo(20));
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Hideout.ShareMaterials, Is.True);
            Assert.That(copy.Hideout.ShareKeep, Is.Zero);
        }

        [Test]
        public void TheStorageShowsHowMuchOfTheBuildItCanCover()
        {
            Texts.UseLanguage("ru");
            var loop = TestWorld.Loop(new FakeInputSource());
            var hideout = loop.Hideout;
            hideout.UseBuild(new Dictionary<string, int>(), System.Array.Empty<string>(), "storage", 2, 400, 1000, 30, 10, false, new Dictionary<string, int>(), System.Array.Empty<string>());
            hideout.UseCommons(false, System.Array.Empty<SharedBed>(), new Dictionary<string, long>(), true, new Dictionary<string, long> { ["ore_hills"] = 30, ["fish_perch"] = 4 },
                new[] { new StorageEntry("Bob", "ore_hills", -6, "storage") }, 0, 20);
            hideout.UseForBuild(30 * hideout.PointsOf("ore_hills"), 12);

            var lines = HideoutView.StorageForBuild(hideout);
            Assert.That(lines.Select(line => line.Item), Is.EqualTo(new[] { "ore_hills" }));
            Assert.That(lines[0].Points, Is.EqualTo(30 * hideout.PointsOf("ore_hills")));
            Assert.That(lines[0].CanGive, Is.True);
            Assert.That(hideout.CanBuildFromStorage("ore_hills", 31), Is.False);
            var share = (int)System.Math.Floor(100.0 * 30 * hideout.PointsOf("ore_hills") / 600);
            Assert.That(HideoutView.StorageForBuildSummary(hideout), Does.Contain(share + "%"));
            Assert.That(HideoutView.Project(hideout), Does.Contain(Texts.Format("hideout.project.fromStorage", 12)));
            Assert.That(HideoutView.JournalLines(hideout.State)[0], Is.EqualTo(Texts.Format("hideout.journal.built", "Bob", "ore_hills", 6)));
            hideout.Leave();
            Assert.That(hideout.State.ForBuildPoints, Is.Zero);
        }

        [Test]
        public void AnOldSaveFromTheRefugeReadsAsTheOwnHideout()
        {
            var state = new GameState(1);
            state.Peaceful.Mode = PeacefulMode.Camp;
            var json = Game.Core.Content.Json.JsonWriter.Write(GameStateJson.Write(state));
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"place\":\\s*\"hideout\"", "\"place\": \"refuge\"");
            Assert.That(json, Does.Contain("refuge"));
            var copy = GameStateJson.Read(new ContentNode(Game.Core.Content.Json.JsonParser.Parse(json, "save"), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Peaceful.Place, Is.EqualTo(PeacefulPlace.Hideout));
            Assert.That(StripScene.PlaceOf(copy), Is.EqualTo(StripPlace.Hideout));
        }

        [Test]
        public void TheStorageRulesComeFromTheData()
        {
            var storage = TestWorld.Real.Hideout.Storage;
            Assert.That(storage.Takes("herb_windwort"), Is.True);
            Assert.That(storage.Takes("ember_coal"), Is.True);
            Assert.That(storage.Takes("seed_colossus"), Is.False);
            Assert.That(storage.Takes("gem_fireball"), Is.False);
            Assert.That(storage.ShareKeep, Is.EqualTo(10));
        }
}
}
