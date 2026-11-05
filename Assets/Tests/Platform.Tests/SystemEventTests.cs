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
    }
}
