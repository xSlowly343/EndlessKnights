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
        public void SessionAndAllTimeColumnsWithOneClickCount()
        {
            var session = new InputStats { Keys = 1234567, LeftClicks = 10, RightClicks = 2, MiddleClicks = 1, MouseMeters = 734.9, PlaySeconds = 125 };
            var allTime = new InputStats { Keys = 99, LeftClicks = 48000, RightClicks = 200, MiddleClicks = 10, MouseMeters = 1204370, PlaySeconds = 3 * 3600 + 5 * 60 + 59 };

            var rows = InputStatsView.Rows(session, allTime, 0);

            Assert.That(rows.Count, Is.EqualTo(InputStatsView.RowCount));
            Assert.That(rows[0].Label, Is.EqualTo("Время в игре"));
            Assert.That(rows[0].Session, Is.EqualTo("2 мин"));
            Assert.That(rows[0].AllTime, Is.EqualTo("3 ч 05 мин"));
            Assert.That(rows[1].Label, Is.EqualTo("Начало игры"));
            Assert.That(rows[1].AllTime, Is.EqualTo("—"));
            Assert.That(rows[2].Label, Is.EqualTo("Нажатия клавиш"));
            Assert.That(rows[2].Session, Is.EqualTo("1 234 567"));
            Assert.That(rows[2].AllTime, Is.EqualTo("99"));
            Assert.That(rows[3].Session, Is.EqualTo("13"));
            Assert.That(rows[3].AllTime, Is.EqualTo("48 210"));
            Assert.That(rows[3].HasHint, Is.False);
            Assert.That(rows[4].Session, Is.EqualTo("734 м"));
            Assert.That(rows[4].AllTime, Is.EqualTo("1 204.37 км"));
        }

        [Test]
        public void StartDateIsShownAsADay()
        {
            var noon = new System.DateTimeOffset(2026, 10, 26, 12, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();

            var rows = InputStatsView.Rows(new InputStats(), new InputStats(), noon);

            Assert.That(rows[1].AllTime, Is.EqualTo("26.10.2026"));
        }

        [Test]
        public void EnglishUsesKilometresAndButtonNames()
        {
            Texts.UseLanguage("en");
            var stats = new InputStats { LeftClicks = 1, MouseMeters = 2054 };

            var rows = InputStatsView.Rows(stats, stats, 0, ",");

            Assert.That(InputStatsView.SessionTitle, Is.EqualTo("This session"));
            Assert.That(rows[3].Session, Is.EqualTo("1"));
            Assert.That(rows[4].Session, Is.EqualTo("2.05 km"));
            Assert.That(rows[0].Session, Is.EqualTo("0 min"));
        }
    }
}
