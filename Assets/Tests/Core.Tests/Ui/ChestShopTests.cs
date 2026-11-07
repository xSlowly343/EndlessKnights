using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Mail;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class ChestShopTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static ChestCatalog Chests => TestWorld.Data.Chests;

        [Test]
        public void EveryChestHasFixedContentsAPriceAndAWeeklyLimit()
        {
            Assert.That(Chests.Chests, Is.Not.Empty);
            foreach (var chest in Chests.Chests)
            {
                Assert.That(chest.Price, Is.GreaterThan(0), chest.Id);
                Assert.That(chest.Ingots + chest.Essence, Is.GreaterThan(0), chest.Id);
                Assert.That(chest.PerWeek, Is.GreaterThanOrEqualTo(1), chest.Id);
                Assert.That(chest.NameRu, Is.Not.Empty, chest.Id);
                Assert.That(chest.NameEn, Is.Not.Empty, chest.Id);
            }
        }

        [Test]
        public void BiggerChestsGiveMoreForEachCrystal()
        {
            Assert.That(Chests.TryGet("smith_small", out var small), Is.True);
            Assert.That(Chests.TryGet("smith", out var middle), Is.True);
            Assert.That(Chests.TryGet("smith_big", out var big), Is.True);
            Assert.That(middle.Ingots / (double)middle.Price, Is.GreaterThan(small.Ingots / (double)small.Price));
            Assert.That(big.Ingots / (double)big.Price, Is.GreaterThan(middle.Ingots / (double)middle.Price));
        }

        [Test]
        public void TheLoaderRefusesEmptyRepeatedAndNonCrystalChests()
        {
            const string head = "{\"version\":1,\"currency\":\"crystals\",\"chests\":[";
            const string chest = "{\"id\":\"a\",\"name\":{\"ru\":\"А\",\"en\":\"A\"},\"price\":100,\"ingots\":10,\"perWeek\":1}";
            Assert.That(ChestCatalog.Load(head + chest + "]}").Chests.Count, Is.EqualTo(1));
            Assert.Throws<ContentException>(() => ChestCatalog.Load(head + chest + "," + chest + "]}"));
            Assert.Throws<ContentException>(() => ChestCatalog.Load(head + "{\"id\":\"b\",\"name\":{\"ru\":\"Б\",\"en\":\"B\"},\"price\":100,\"perWeek\":1}]}"));
            Assert.Throws<ContentException>(() => ChestCatalog.Load("{\"version\":1,\"currency\":\"friendship\",\"chests\":[" + chest + "]}"));
            Assert.Throws<ContentException>(() => ChestCatalog.Load(head + "]}"));
        }

        [Test]
        public void TheShopShowsWhatIsLeftThisWeekAndWhatCanBeBought()
        {
            var bought = new Dictionary<string, int> { ["smith_small"] = 3, ["smith"] = 1 };
            var lines = ChestShopView.Lines(Chests, bought, 600);

            var small = lines.Find(line => line.Id == "smith_small")!;
            var smith = lines.Find(line => line.Id == "smith")!;
            var big = lines.Find(line => line.Id == "smith_big")!;
            var master = lines.Find(line => line.Id == "master")!;

            Assert.That(small.CanBuy, Is.False);
            Assert.That(small.Button, Is.EqualTo("До понедельника"));
            Assert.That(smith.CanBuy, Is.True);
            Assert.That(smith.LeftThisWeek, Is.EqualTo(1));
            Assert.That(smith.Left, Is.EqualTo("осталось на неделе: 1 из 2"));
            Assert.That(smith.Contents, Is.EqualTo("Слитки Разлома ×110"));
            Assert.That(big.CanBuy, Is.False);
            Assert.That(big.Button, Is.EqualTo("Не хватает Кристаллов"));
            Assert.That(master.Contents, Is.EqualTo("Слитки Разлома ×200 · Эссенция Разлома ×200"));
            Assert.That(master.Price, Is.EqualTo("1500 Кристаллов"));
        }

        [Test]
        public void AChestLetterGivesIngotsAndEssenceOnce()
        {
            var letter = new MailLetter("chest-3000-master-1", MailLetter.ChestKind, 1000, 0, 0, 0, "master", new RiftReward(0, 200, 0, 200), false);
            var state = new GameState();
            var box = new MailBox();
            box.Replace(new[] { letter });

            Assert.That(box.Take(letter, state), Is.True);
            Assert.That(box.Take(letter, state), Is.False);
            Assert.That(state.Drops.Ingots, Is.EqualTo(200));
            Assert.That(state.Drops.Essence, Is.EqualTo(200));
            Assert.That(MailText.Title(letter), Is.EqualTo("Покупка в лавке"));
            Assert.That(MailText.Body(letter, Chests), Is.EqualTo("«Сундук мастера» — содержимое сундука"));
            Assert.That(MailText.Reward(letter), Is.EqualTo("Слитки +200 · Эссенция +200"));
        }
    }
}
