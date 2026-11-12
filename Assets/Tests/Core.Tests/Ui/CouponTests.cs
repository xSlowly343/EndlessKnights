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
        public void ACodeCanBeTypedLetterByLetter()
        {
            Texts.UseLanguage("ru");
            var entry = new CouponEntry();

            foreach (var symbol in "rift-20 26")
                entry.Type(symbol);

            Assert.That(entry.Text, Is.EqualTo("RIFT2026"), "dashes and spaces are skipped, letters go upper-case");
            Assert.That(entry.Display, Is.EqualTo("RIFT-2026"));
            Assert.That(entry.Ready, Is.True);
            Assert.That(entry.Type('ж'), Is.False, "only Latin letters and digits");
            Assert.That(entry.Type(CouponEntry.Backspace), Is.True);
            Assert.That(entry.Text, Is.EqualTo("RIFT202"));
            Assert.That(Coupon.Normalize(entry.Text), Is.EqualTo("RIFT202"));
            Assert.That(CouponEntry.IsSubmit('\r'), Is.True);
            Assert.That(CouponEntry.IsSubmit('a'), Is.False);
        }

        [Test]
        public void TheTypedCodeStopsAtTheLongestCouponAndCanBePastedOver()
        {
            var entry = new CouponEntry();
            for (var i = 0; i < Coupon.MaxLength + 5; i++)
                entry.Type('a');

            Assert.That(entry.Length, Is.EqualTo(Coupon.MaxLength));
            Assert.That(entry.Paste("  gift-77aa "), Is.True);
            Assert.That(entry.Text, Is.EqualTo("GIFT77AA"));
            Assert.That(entry.Paste("no"), Is.False, "a broken clipboard keeps what was typed");
            Assert.That(entry.Text, Is.EqualTo("GIFT77AA"));
            Assert.That(entry.Clear(), Is.True);
            Assert.That(entry.Ready, Is.False);
            Assert.That(entry.Type(CouponEntry.Backspace), Is.False);
        }

        [Test]
        public void TheCouponRowShowsTheTypedCodeAndOffersToApplyIt()
        {
            Texts.UseLanguage("ru");
            var settings = new Game.Core.Settings.GameSettings();
            var entry = SettingsScreen.CouponCode;
            entry.Clear();

            Assert.That(SettingsScreen.ActionLabel(SettingId.Coupon), Is.EqualTo("Вставить"));
            Assert.That(SettingsScreen.HintLine(SettingId.Coupon, settings), Does.Contain("Набери"));
            foreach (var symbol in "abcd12")
                entry.Type(symbol);

            Assert.That(SettingsScreen.ActionLabel(SettingId.Coupon), Is.EqualTo("Применить"));
            Assert.That(SettingsScreen.HintLine(SettingId.Coupon, settings), Does.Contain("ABCD-12"));
            entry.Clear();
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
