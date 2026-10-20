using System;
using Game.Platform.Windows.Power;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class SystemEventTests
    {
        [TestCase(0x0218u, 0x0004L, SystemEvent.Suspend)]
        [TestCase(0x0218u, 0x0012L, SystemEvent.Resume)]
        [TestCase(0x0218u, 0x0007L, SystemEvent.Resume)]
        [TestCase(0x02B1u, 0x0007L, SystemEvent.SessionLocked)]
        [TestCase(0x02B1u, 0x0008L, SystemEvent.SessionUnlocked)]
        public void DecodesKnownMessages(uint message, long wParam, SystemEvent expected)
        {
            Assert.That(SystemEventDecoder.TryDecode(message, wParam, out var systemEvent), Is.True);
            Assert.That(systemEvent, Is.EqualTo(expected));
        }

        [TestCase(0x0218u, 0x000AL)]
        [TestCase(0x02B1u, 0x0001L)]
        [TestCase(0x0111u, 0x0004L)]
        public void IgnoresOtherMessages(uint message, long wParam)
        {
            Assert.That(SystemEventDecoder.TryDecode(message, wParam, out _), Is.False);
        }

        [Test]
        public void SleepTrackerMeasuresTimeAsleep()
        {
            var tracker = new SleepTracker();
            var start = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

            tracker.Suspended(start);
            var asleep = tracker.Resumed(start.AddMinutes(5));

            Assert.That(asleep, Is.EqualTo(TimeSpan.FromMinutes(5)));
        }

        [Test]
        public void ResumeWithoutSuspendIsZero()
        {
            var tracker = new SleepTracker();

            Assert.That(tracker.Resumed(DateTime.UtcNow), Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void ClockGoingBackwardsIsZero()
        {
            var tracker = new SleepTracker();
            var start = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

            tracker.Suspended(start);

            Assert.That(tracker.Resumed(start.AddMinutes(-3)), Is.EqualTo(TimeSpan.Zero));
        }
    }
}
