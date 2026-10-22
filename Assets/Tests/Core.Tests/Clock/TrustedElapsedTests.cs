using Game.Core.Clock;
using NUnit.Framework;

namespace Game.Core.Tests.Clock
{
    public class TrustedElapsedTests
    {
        private const long Hour = 3_600_000;
        private const long Start = 1_760_000_000_000;
        private const long Boot = 7;

        [Test]
        public void SameBootUsesMonotonicTime()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 5 * Hour, Boot));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start + 2 * Hour, 7 * Hour, Boot));

            Assert.That(elapsed.Seconds, Is.EqualTo(7200));
            Assert.That(elapsed.Source, Is.EqualTo(ElapsedSource.Monotonic));
            Assert.That(elapsed.ClockTampered, Is.False);
        }

        [Test]
        public void ClockMovedForwardGivesNoOfflineTimeOnSameBoot()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 5 * Hour, Boot));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start + 10 * Hour + 60_000, 5 * Hour + 60_000, Boot));

            Assert.That(elapsed.Seconds, Is.EqualTo(60));
            Assert.That(elapsed.ClockTampered, Is.True);
        }

        [Test]
        public void ClockMovedBackDoesNotGiveTimeTwice()
        {
            var first = ClockStamp.First(new ClockReading(Start, 1 * Hour, Boot));
            var savedWithClockBack = first.Next(new ClockReading(Start - 10 * Hour, 2 * Hour, Boot));

            var elapsed = TrustedElapsed.Between(savedWithClockBack, new ClockReading(Start + Hour, 0, Boot + 1));

            Assert.That(savedWithClockBack.HighestWallUnixMs, Is.EqualTo(Start));
            Assert.That(elapsed.Source, Is.EqualTo(ElapsedSource.Wall));
            Assert.That(elapsed.Seconds, Is.EqualTo(3600));
            Assert.That(elapsed.ClockTampered, Is.True);
        }

        [Test]
        public void AfterRebootWallClockIsUsed()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 9 * Hour, Boot));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start + 8 * Hour, Hour, Boot + 1));

            Assert.That(elapsed.Source, Is.EqualTo(ElapsedSource.Wall));
            Assert.That(elapsed.Seconds, Is.EqualTo(8 * 3600));
            Assert.That(elapsed.ClockTampered, Is.False);
        }

        [Test]
        public void ClockBehindTheSaveGivesZero()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 9 * Hour, Boot));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start - 5 * Hour, Hour, Boot + 1));

            Assert.That(elapsed.Seconds, Is.EqualTo(0));
            Assert.That(elapsed.ClockTampered, Is.True);
        }

        [Test]
        public void ServerTimeWinsWhenBothSidesHaveIt()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 9 * Hour, Boot, serverUnixMs: Start));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start + 20 * Hour, Hour, Boot + 1, serverUnixMs: Start + 3 * Hour));

            Assert.That(elapsed.Source, Is.EqualTo(ElapsedSource.Server));
            Assert.That(elapsed.Seconds, Is.EqualTo(3 * 3600));
            Assert.That(elapsed.ClockTampered, Is.True);
        }

        [Test]
        public void WithoutServerNowLocalTimeIsUsed()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 1 * Hour, Boot, serverUnixMs: Start));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start + Hour, 2 * Hour, Boot));

            Assert.That(elapsed.Source, Is.EqualTo(ElapsedSource.Monotonic));
            Assert.That(elapsed.Seconds, Is.EqualTo(3600));
        }

        [Test]
        public void SmallDriftIsNotTampering()
        {
            var saved = ClockStamp.First(new ClockReading(Start, 1 * Hour, Boot));

            var elapsed = TrustedElapsed.Between(saved, new ClockReading(Start + Hour + 30_000, 2 * Hour, Boot));

            Assert.That(elapsed.ClockTampered, Is.False);
            Assert.That(elapsed.Seconds, Is.EqualTo(3600));
        }
    }
}
