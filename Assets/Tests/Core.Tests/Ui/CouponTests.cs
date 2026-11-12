using Game.Core.Content.Rift;
using Game.Core.Mail;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class CouponTests
    {
        [TestCase("rift-2026", "RIFT2026")]
        [TestCase("  Rift 2026\n", "RIFT2026")]
        [TestCase("ABCD", "ABCD")]
        [TestCase("abc", "")]
        [TestCase("", "")]
        [TestCase(null, "")]
        [TestCase("пароль123", "")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAA", "")]
        [TestCase("code;drop", "")]
        public void ACodeIsCleanedTheSameWayAsOnTheServer(string? raw, string code) => Assert.That(Coupon.Normalize(raw), Is.EqualTo(code));

        [Test]
        public void AVeryLongClipboardIsNotACode() => Assert.That(Coupon.Normalize(new string('A', 20) + new string(' ', 60)), Is.Empty);

        [Test]
        public void EveryServerAnswerHasItsText()
        {
            Texts.UseLanguage("ru");
            foreach (var status in new[] { "done", "unknown", "early", "expired", "used", "spent", "tooMany", "busy" })
                Assert.That(CouponText.Result(status), Is.Not.EqualTo(CouponText.Failed), status);
            Assert.That(CouponText.Result("something new"), Is.EqualTo(CouponText.Failed));
            Assert.That(CouponText.Result(string.Empty), Is.EqualTo(CouponText.Failed));
            Assert.That(CouponText.Empty, Does.Contain("Вставить"));
        }

        [Test]
        public void ACouponLetterNamesItsCode()
        {
            Texts.UseLanguage("ru");
            var letter = new MailLetter("coupon:RIFT2026", MailLetter.CouponKind, 0, 0, 0, 0, "RIFT2026", new RiftReward(500, 3, 0, 0), false);

            Assert.That(MailText.Title(letter), Is.EqualTo("Подарок по купону"));
            Assert.That(MailText.Body(letter), Is.EqualTo("Купон RIFT2026"));
        }
    }
}
