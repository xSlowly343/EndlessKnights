using Game.Core.Clock;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Clock
{
    public class TrustedDayTests
    {
        private const long Day = LocalDay.MillisecondsPerDay;
        private const long Hour = 3_600_000;
        private const long Noon = 20_000 * Day + 12 * Hour;

        [Test]
        public void AFreshGameCountsDaysByTheMonotonicClock()
        {
            var start = new ClockReading(Noon, 1_000, 7);
            var day = TrustedDay.Start(null, start, 0);

            Assert.That(day.Today(start), Is.EqualTo(20_000));
            Assert.That(day.Today(new ClockReading(Noon + 13 * Hour, 1_000 + 13 * Hour, 7)), Is.EqualTo(20_001));
        }

        [Test]
        public void MovingTheWindowsClockForwardDoesNotStartANewDay()
        {
            var start = new ClockReading(Noon, 1_000, 7);
            var day = TrustedDay.Start(null, start, 0);

            var jumped = new ClockReading(Noon + 3 * Day, 1_000 + 60_000, 7);

            Assert.That(day.Today(jumped), Is.EqualTo(20_000));
        }

        [Test]
        public void AfterARestartTheDayFollowsTheTrustedTimeSinceTheSave()
        {
            var saved = ClockStamp.First(new ClockReading(Noon, 5_000, 7));
            var forward = new ClockReading(Noon + 5 * Day, 5_000 + 2 * Hour, 7);

            var day = TrustedDay.Start(saved, forward, 0);

            Assert.That(day.Today(forward), Is.EqualTo(20_000), "two trusted hours after noon is the same day, whatever the wall clock says");
        }

        [Test]
        public void TheLocalOffsetMovesMidnight()
        {
            var lateEvening = new ClockReading(20_000 * Day + 22 * Hour, 1_000, 7);

            Assert.That(TrustedDay.Start(null, lateEvening, 3 * Hour).Today(lateEvening), Is.EqualTo(20_001));
        }

        [Test]
        public void TheDailyLimitIsNotResetByGoingBackADay()
        {
            var loop = TestWorld.Loop();
            loop.SetLocalDay(10);
            loop.State.Drops.DailyItems = 5;

            loop.SetLocalDay(9);
            Assert.That(loop.State.Drops.DailyItems, Is.EqualTo(5));
            loop.SetLocalDay(10);
            Assert.That(loop.State.Drops.DailyItems, Is.EqualTo(5), "back and forth again is not a new day");
            loop.SetLocalDay(11);
            Assert.That(loop.State.Drops.DailyItems, Is.Zero);
        }
    }
}
