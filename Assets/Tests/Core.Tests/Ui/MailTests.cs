using Game.Core.Content.Rift;
using Game.Core.Mail;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class MailTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static MailLetter Rift(string id, bool claimed = false) =>
            new MailLetter(id, MailLetter.RiftKind, 1000, 2, 14, 31, "night_shift", new RiftReward(200, 2, 4, 20), claimed);

        [Test]
        public void NewLettersArriveOnceAndCountAsUnread()
        {
            var box = new MailBox();

            var first = box.Replace(new[] { Rift("rift-1"), Rift("rift-0", claimed: true) });
            var again = box.Replace(new[] { Rift("rift-1"), Rift("rift-2"), Rift("rift-0", claimed: true) });

            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(again, Has.Count.EqualTo(1));
            Assert.That(again[0].Id, Is.EqualTo("rift-2"));
            Assert.That(box.Unread, Is.EqualTo(2));
        }

        [Test]
        public void ALetterIsTakenIntoTheWalletOnce()
        {
            var box = new MailBox();
            var state = new GameState();
            box.Replace(new[] { Rift("rift-1"), Rift("rift-0", claimed: true) });

            Assert.That(box.Take(Rift("rift-1"), state), Is.True);
            Assert.That(box.Take(Rift("rift-1"), state), Is.False);
            Assert.That(box.Take(Rift("rift-0"), state), Is.False, "a claimed letter pays nothing");

            Assert.That(state.Drops.Shards, Is.EqualTo(200));
            Assert.That(state.Drops.UncutGems, Is.EqualTo(2));
            Assert.That(state.Drops.Ingots, Is.EqualTo(4));
            Assert.That(box.Take(Rift("rift-9"), state), Is.True, "the server already gave out a letter the box has not loaded yet");
            Assert.That(state.Drops.Shards, Is.EqualTo(400));
            Assert.That(box.Unread, Is.Zero);
            Assert.That(box.Find("rift-1")!.Claimed, Is.True);
        }

        [Test]
        public void ARiftLetterReadsInRussian()
        {
            var rows = MailText.Rows(new[] { Rift("rift-1"), Rift("rift-0", claimed: true) });

            Assert.That(rows[0].Title, Is.EqualTo("Итоги Разлома"));
            Assert.That(rows[0].Body, Does.StartWith("2 место из 14, этап 31 · отряд «"));
            Assert.That(rows[0].Reward, Is.EqualTo("Осколки +200 · неогранённые гемы +2 · Слитки +4 · Знаки дружбы +20"));
            Assert.That(rows[0].Button, Is.EqualTo("Забрать"));
            Assert.That(rows[1].Button, Is.EqualTo("Получено"));
            Assert.That(MailText.Arrived(Rift("rift-1")), Is.EqualTo("Новое письмо: Итоги Разлома"));
            Assert.That(MailText.Taken(Rift("rift-1")), Does.StartWith("Получено: Осколки +200"));
        }
    }
}
