using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CosmeticTests
    {
        private static CosmeticCatalog Catalog => TestWorld.Real.Cosmetics;

        [Test]
        public void TheStartingShopHasSeventyTwoItemsAndEveryHeroHasASkin()
        {
            Assert.That(Catalog.All.Count(c => c.ForSale), Is.EqualTo(72));
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
    }
}
