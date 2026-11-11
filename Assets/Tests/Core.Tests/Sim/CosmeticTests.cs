using System.Linq;
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
    public class CosmeticTests
    {
        private static CosmeticCatalog Catalog => TestWorld.Real.Cosmetics;

        [Test]
        public void TheStartingShopHasNinetyFourItemsAndEveryHeroHasASkin()
        {
            Assert.That(Catalog.All.Count(c => c.ForSale), Is.EqualTo(94));
            foreach (var hero in HeroClassNames.ByName.Keys)
                Assert.That(Catalog.All.Any(c => c.Slot == "heroSkin" && c.Hero == hero && c.ForSale), Is.True, hero);
            var weaponTypes = TestWorld.Real.Weapons.Types.Select(t => t.Id).ToList();
            foreach (var item in Catalog.All.Where(c => c.WeaponType.Length > 0))
                Assert.That(weaponTypes, Does.Contain(item.WeaponType), item.Id);
            foreach (var item in Catalog.All.Where(c => c.Hero.Length > 0))
                Assert.That(HeroClassNames.ByName.ContainsKey(item.Hero), Is.True, item.Id);
            foreach (var item in Catalog.All.Where(c => c.Achievement.Length > 0))
                Assert.That(TestWorld.Real.Achievements.All.Any(a => a.Id == item.Achievement), Is.True, item.Id);
        }

        [Test]
        public void RewardsCannotBeBoughtAndShopItemsHavePrices()
        {
            Assert.That(Catalog.All.Where(c => c.Source == "reward").All(c => c.Price == 0 && !c.ForSale), Is.True);
            Assert.That(Catalog.TryGet("champion_top10", out _), Is.True);
            Assert.That(Catalog.TryGet("war_victors", out var banner) && banner.Clan, Is.True);
            Assert.Throws<ContentException>(() => CosmeticCatalog.Load(TestData.ReadGameDataFile(CosmeticCatalog.FileName).Replace("\"price\": 1000, \"source\": \"shop\"", "\"price\": 0, \"source\": \"shop\"")));
        }

        [Test]
        public void OnlyOwnedItemsAreWornAndEachHeroKeepsItsOwnSkin()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var cosmetics = loop.Cosmetics;

            Assert.That(cosmetics.Equip("skin_runedancer"), Is.EqualTo(CosmeticResult.NotOwned));
            Assert.That(cosmetics.Equip("nothing"), Is.EqualTo(CosmeticResult.Unknown));
            cosmetics.Grant("skin_runedancer");
            cosmetics.Grant("skin_duelist");
            cosmetics.Grant("theme_night");
            cosmetics.Grant("banner_wolf");
            Assert.That(cosmetics.Equip("skin_runedancer"), Is.EqualTo(CosmeticResult.Done));
            Assert.That(cosmetics.Equip("skin_duelist"), Is.EqualTo(CosmeticResult.Done));
            Assert.That(cosmetics.Equip("theme_night"), Is.EqualTo(CosmeticResult.Done));
            Assert.That(cosmetics.Equip("banner_wolf"), Is.EqualTo(CosmeticResult.ClanOnly));

            Assert.That(cosmetics.Worn("heroSkin", "runedancer")!.Id, Is.EqualTo("skin_runedancer"));
            Assert.That(cosmetics.Worn("heroSkin", "duelist")!.Id, Is.EqualTo("skin_duelist"));
            Assert.That(cosmetics.Worn("stripTheme")!.Id, Is.EqualTo("theme_night"));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Cosmetics.Owned, Does.Contain("skin_duelist"));
            Assert.That(copy.Cosmetics.Equipped["stripTheme"], Is.EqualTo("theme_night"));

            cosmetics.SetOwned(new[] { "theme_night" });
            Assert.That(cosmetics.Worn("heroSkin", "runedancer"), Is.Null, "the server's list wins and takes off what is not owned");
            Assert.That(cosmetics.Worn("stripTheme")!.Id, Is.EqualTo("theme_night"));
        }

        [Test]
        public void TheShopShowsWhatIsForSaleAndOwnedRewardsButHidesOtherRewards()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.Cosmetics.Grant("frame_limit");
            loop.Cosmetics.Grant("avatar_leaf");
            var frames = CosmeticsView.Lines(loop.Cosmetics, "avatarFrame");

            Assert.That(frames.Select(l => l.Id), Does.Contain("frame_limit"));
            Assert.That(frames.Select(l => l.Id), Does.Not.Contain("champion_top10"));
            Assert.That(frames.Single(l => l.Id == "avatar_leaf").ForSale, Is.False);
            Assert.That(frames.Single(l => l.Id == "avatar_silver").ForSale, Is.True);
            Assert.That(CosmeticsView.Lines(loop.Cosmetics, "heroSkin", "minstrel").Single().Id, Is.EqualTo("skin_minstrel"));
        }

        [Test]
        public void EveryLookSlotHasAFreeDefaultAndItsLooksAreNumberedOnce()
        {
            foreach (var slot in CosmeticCatalog.LookSlots)
            {
                var items = Catalog.All.Where(c => c.Slot == slot).ToList();
                Assert.That(items.Select(c => c.Look), Is.Unique, slot);
                Assert.That(Catalog.ByLook(slot, 0)!.Starter, Is.True, slot);
                Assert.That(items.Where(c => c.Starter).All(c => c.Price == 0 && !c.ForSale), Is.True, slot);
            }
            Assert.That(Catalog.All.Where(c => c.Slot == CosmeticCatalog.CloakSlot || c.Slot == CosmeticCatalog.EyesSlot).All(c => c.Color.Length == 7 && c.Color[0] == '#'), Is.True);
            Assert.That(Catalog.All.Count(c => c.Slot == CosmeticCatalog.StrikeSlot), Is.EqualTo(9), "the void crescent and eight more");
            Assert.That(Catalog.All.Where(c => !CosmeticCatalog.IsLookSlot(c.Slot)).All(c => c.Look == 0), Is.True);
            Assert.Throws<ContentException>(() => CosmeticCatalog.Load(TestData.ReadGameDataFile(CosmeticCatalog.FileName).Replace("\"source\": \"starter\", \"look\": 4,", "\"source\": \"starter\", \"look\": 0,")));
        }

        [Test]
        public void StarterLooksAreOwnedByEveryoneAndSurviveTheServersList()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var cosmetics = loop.Cosmetics;

            Assert.That(cosmetics.Look, Is.EqualTo(default(HeroLook)));
            Assert.That(cosmetics.Equip("cloak_moss"), Is.EqualTo(CosmeticResult.Done));
            Assert.That(cosmetics.Equip("eyes_ice"), Is.EqualTo(CosmeticResult.Done));
            Assert.That(cosmetics.Equip("strike_star"), Is.EqualTo(CosmeticResult.NotOwned));
            cosmetics.Grant("strike_star");
            cosmetics.Equip("strike_star");
            Assert.That(cosmetics.Look, Is.EqualTo(new HeroLook(4, 3, 1)));

            cosmetics.SetOwned(new string[0]);
            Assert.That(cosmetics.Look, Is.EqualTo(new HeroLook(4, 3, 0)), "starters stay on, the bought strike comes off");
        }

        [Test]
        public void TheChooserWearsWhatIsOwnedAndOnlyTriesOnTheRest()
        {
            Texts.UseLanguage("ru");
            var loop = TestWorld.Loop(new FakeInputSource());
            var chooser = new HeroLookChooser(loop.Cosmetics);

            Assert.That(chooser.Label(CosmeticCatalog.CloakSlot, true), Is.EqualTo("Плащ Изнанки"));
            Assert.That(chooser.Step(CosmeticCatalog.CloakSlot, 1), Is.True);
            Assert.That(chooser.ShownIn(CosmeticCatalog.CloakSlot)!.Id, Is.EqualTo("cloak_crimson"));
            Assert.That(chooser.TryingOn(CosmeticCatalog.CloakSlot), Is.True);
            Assert.That(chooser.Label(CosmeticCatalog.CloakSlot, true), Is.EqualTo("Багряный плащ · примерка, 300 крист."));
            Assert.That(chooser.Shown.Cloak, Is.EqualTo(1));
            Assert.That(chooser.Worn.Cloak, Is.Zero, "a try-on is not worn, so the party does not see it");

            loop.Cosmetics.Grant("cloak_crimson");
            Assert.That(chooser.TryingOn(CosmeticCatalog.CloakSlot), Is.False);
            Assert.That(chooser.Worn.Cloak, Is.Zero, "buying alone does not put it on");
            chooser.Step(CosmeticCatalog.CloakSlot, 1);
            Assert.That(chooser.Worn.Cloak, Is.EqualTo(1));

            chooser.Step(CosmeticCatalog.CloakSlot, -1);
            Assert.That(chooser.Worn.Cloak, Is.Zero);
            chooser.Step(CosmeticCatalog.CloakSlot, -1);
            Assert.That(chooser.ShownIn(CosmeticCatalog.CloakSlot)!.Id, Is.EqualTo("cloak_rose"), "the list wraps around");
        }

        [Test]
        public void TheLookTabOfTheSettingsCyclesTheChooser()
        {
            Texts.UseLanguage("ru");
            var loop = TestWorld.Loop(new FakeInputSource());
            var settings = new Game.Core.Settings.GameSettings();
            try
            {
                SettingsScreen.UseLooks(new HeroLookChooser(loop.Cosmetics));
                Assert.That(SettingsScreen.Rows(SettingsScreen.LookTab), Is.EqualTo(new[] { SettingId.HeroCloak, SettingId.HeroEyes, SettingId.HeroStrike }));
                Assert.That(SettingsScreen.Value(SettingId.HeroEyes, settings), Is.EqualTo("Глаза Изнанки"));
                Assert.That(SettingsScreen.Change(SettingId.HeroEyes, settings, -1), Is.True);
                Assert.That(SettingsScreen.Value(SettingId.HeroEyes, settings), Is.EqualTo("Розовые глаза · примерка, 200 крист."));
                Assert.That(SettingsScreen.Value(SettingId.HeroStrike, settings), Is.EqualTo("Полумесяц Изнанки"));
            }
            finally
            {
                SettingsScreen.UseLooks(null);
            }
            Assert.That(SettingsScreen.Change(SettingId.HeroCloak, settings, 1), Is.False, "without a game there is nothing to choose");
        }
    }
}
