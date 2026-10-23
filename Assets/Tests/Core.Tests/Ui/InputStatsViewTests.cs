using Game.Core.Input;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class InputStatsViewTests
    {
        [TearDown]
        public void BackToRussian() => Texts.UseLanguage(StringTable.DefaultLanguage);

        [Test]
        public void TwoColumnsWithClickBreakdown()
        {
            var session = new InputStats { Keys = 1234567, LeftClicks = 10, RightClicks = 2, MiddleClicks = 1, MouseMeters = 734.9 };
            var allTime = new InputStats { Keys = 99, LeftClicks = 48000, RightClicks = 200, MiddleClicks = 10, MouseMeters = 1204370 };

            var rows = InputStatsView.Rows(session, allTime);

            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[0].Label, Is.EqualTo("Нажатия клавиш"));
            Assert.That(rows[0].Session, Is.EqualTo("1 234 567"));
            Assert.That(rows[0].AllTime, Is.EqualTo("99"));
            Assert.That(rows[1].Session, Is.EqualTo("13"));
            Assert.That(rows[1].AllTime, Is.EqualTo("48 210"));
            Assert.That(rows[1].AllTimeHint, Is.EqualTo("ЛКМ 48 000 · ПКМ 200 · СКМ 10"));
            Assert.That(rows[0].HasHint, Is.False);
            Assert.That(rows[2].Session, Is.EqualTo("734 м"));
            Assert.That(rows[2].AllTime, Is.EqualTo("1 204.37 км"));
        }

        [Test]
        public void EnglishUsesKilometresAndButtonNames()
        {
            Texts.UseLanguage("en");
            var stats = new InputStats { LeftClicks = 1, MouseMeters = 2054 };

            var rows = InputStatsView.Rows(stats, stats, ",");

            Assert.That(InputStatsView.SessionTitle, Is.EqualTo("This session"));
            Assert.That(rows[1].SessionHint, Is.EqualTo("LMB 1 · RMB 0 · MMB 0"));
            Assert.That(rows[2].Session, Is.EqualTo("2.05 km"));
        }
    }
}
