using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Gems;
using Game.Core.Content.Rift;
using Game.Core.Mail;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class GemGiftTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop WithSpareCopy(string id)
        {
            var loop = TestWorld.Loop();
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.OwnedGems.Add(Gem(id));
            loop.State.OwnedGems.Add(Gem(id));
            Assert.That(loop.Book.Equip(Gem(id)), Is.EqualTo(BookResult.Done));
            return loop;
        }

        private static MailLetter Gift(string id, string gem, bool claimed = false) =>
            new MailLetter(id, MailLetter.GiftKind, 1000, 0, 0, 0, "Аня", default(RiftReward), claimed, gem);

        [Test]
        public void OnlyASpareCopyCanBeGiftedAndThenItLeavesTheBag()
        {
            var loop = WithSpareCopy("march");
            var march = Gem("march");

            Assert.That(GemGifts.Check(loop.Book, loop.State, march), Is.EqualTo(GiftCheck.Ready));
            Assert.That(GemGifts.Give(loop.Book, loop.State, march), Is.True);

            Assert.That(GemGifts.Owned(loop.State, march), Is.EqualTo(1));
            Assert.That(GemGifts.Check(loop.Book, loop.State, march), Is.EqualTo(GiftCheck.InTheBook), "the copy in the book stays");
            Assert.That(GemGifts.Give(loop.Book, loop.State, march), Is.False);
            Assert.That(loop.State.Skills[0].Skill, Is.SameAs(march));
        }

        [Test]
        public void GemsOfAPathAreNeverGifted()
        {
            var loop = TestWorld.Loop();
            var pathGem = TestWorld.Data.Gems.All.First(gem => gem.Ascendancy != null);
            loop.State.OwnedGems.Add(pathGem);

            Assert.That(GemGifts.Check(loop.Book, loop.State, pathGem), Is.EqualTo(GiftCheck.PathGem));
            Assert.That(GemGifts.Give(loop.Book, loop.State, pathGem), Is.False);
        }

        [Test]
        public void AReceivedGemIsBoundAndCannotBeGiftedAgain()
        {
            var loop = TestWorld.Loop();
            var box = new MailBox();
            var gem = Gem("march");
            box.Replace(new[] { Gift("gift-1", "march") });

            Assert.That(box.Take(Gift("gift-1", "march"), loop.State, TestWorld.Data.Gems), Is.True);

            Assert.That(GemGifts.Owned(loop.State, gem), Is.EqualTo(1));
            Assert.That(GemGifts.Bound(loop.State, gem), Is.EqualTo(1));
            Assert.That(GemGifts.Check(loop.Book, loop.State, gem), Is.EqualTo(GiftCheck.Bound));
            loop.State.OwnedGems.Add(gem);
            Assert.That(GemGifts.Check(loop.Book, loop.State, gem), Is.EqualTo(GiftCheck.Ready), "an own copy next to the bound one can still be gifted");
            Assert.That(box.Take(Gift("gift-1", "march"), loop.State, TestWorld.Data.Gems), Is.False, "a letter pays once");
            Assert.That(box.Take(Gift("gift-2", "no_such_gem"), loop.State, TestWorld.Data.Gems), Is.True, "an unknown gem from newer content gives nothing");
            Assert.That(GemGifts.Owned(loop.State, gem), Is.EqualTo(2));
        }

        [Test]
        public void BoundGemsSurviveSaving()
        {
            var loop = TestWorld.Loop();
            GemGifts.Receive(loop.State, Gem("march"));
            GemGifts.Receive(loop.State, Gem("march"));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.BoundGems["march"], Is.EqualTo(2));
            Assert.That(loaded.OwnedGems.Count(gem => gem.Id == "march"), Is.EqualTo(loop.State.OwnedGems.Count(gem => gem.Id == "march")));
        }

        [Test]
        public void GiftReasonsReadInRussian()
        {
            Texts.UseLanguage("ru");

            Assert.That(GiftText.Check(GiftCheck.PathGem), Is.EqualTo("Гемы Пути не дарятся"));
            Assert.That(GiftText.Check(GiftCheck.Bound), Is.EqualTo("Подаренный гем привязан к аккаунту"));
            Assert.That(GiftText.Sent("Марш", "Боря", 2), Is.EqualTo("Гем «Марш» ушёл к Боря · подарков сегодня осталось 2"));
            Assert.That(GiftText.Status(2, 3, 5), Is.EqualTo("Подарков сегодня: 2 из 3 · подарок стоит 5 Знаков дружбы"));
            Assert.That(GiftText.Refused("not enough friendship"), Is.EqualTo("не хватает Знаков дружбы"));
            Assert.That(GiftText.Refused("this gem cannot be gifted"), Is.EqualTo("Гемы Пути не дарятся"));
            Assert.That(GiftText.Refused("timeout"), Is.EqualTo("нет связи с сервером"));
        }

        [Test]
        public void AGiftLetterReadsInRussian()
        {
            Texts.UseLanguage("ru");
            var rows = MailText.Rows(new[] { Gift("gift-1", "march") }, TestWorld.Data.Gems);

            Assert.That(rows[0].Title, Is.EqualTo("Подарок друга"));
            Assert.That(rows[0].Body, Does.StartWith("от Аня · гем привязан к аккаунту"));
            Assert.That(rows[0].Reward, Is.EqualTo("гем «" + BookTexts.Name(Gem("march")) + "»"));
        }
    }
}
