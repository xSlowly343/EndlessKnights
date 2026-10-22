using Game.Core.Math;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class OfflineReportTests
    {
        private static OfflineResult Result(double seconds, long kills, long before, long after, bool guardian = false, bool stuck = false) =>
            new OfflineResult(seconds, kills, BigNumber.FromDouble(1500), before, after, 0, farming: before == after, guardian, stuck);

        [TestCase(30, "меньше минуты")]
        [TestCase(125, "2 мин")]
        [TestCase(3600, "1 ч")]
        [TestCase(8100, "2 ч 15 мин")]
        public void FormatsDuration(double seconds, string expected)
        {
            Assert.That(OfflineReport.Duration(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void ShowsOnlyAfterAMinuteWithKills()
        {
            Assert.That(OfflineReport.ShouldShow(30, Result(30, 5, 1, 1)), Is.False);
            Assert.That(OfflineReport.ShouldShow(600, Result(600, 0, 1, 1)), Is.False);
            Assert.That(OfflineReport.ShouldShow(600, Result(600, 5, 1, 1)), Is.True);
        }

        [Test]
        public void ListsProgressAndStageChange()
        {
            var lines = OfflineReport.Lines(8100, Result(8100, 340, 12, 15), NumberFormatSettings.Default);

            Assert.That(lines, Is.EqualTo(new[]
            {
                "Тебя не было: 2 ч 15 мин",
                "Золото: +" + NumberFormatter.Format(BigNumber.FromDouble(1500), NumberFormatSettings.Default),
                "Побеждено врагов: 340",
                "Этап: 12 → 15"
            }));
        }

        [Test]
        public void MentionsTheCapAndTheGuardian()
        {
            var lines = OfflineReport.Lines(3 * 86400, Result(8 * 3600, 900, 20, 24, guardian: true), NumberFormatSettings.Default);

            Assert.That(lines, Does.Contain("Засчитано: 8 ч (это предел)"));
            Assert.That(lines, Does.Contain("Дальше страж биома, его бьют вживую"));
        }

        [Test]
        public void ExplainsAStuckBossWhenFarming()
        {
            var lines = OfflineReport.Lines(1800, Result(1800, 60, 9, 9, stuck: true), NumberFormatSettings.Default);

            Assert.That(lines, Does.Contain("Этап: 9"));
            Assert.That(lines, Does.Contain("Босс этапа пока сильнее, нужна прокачка"));
        }
    }
}
