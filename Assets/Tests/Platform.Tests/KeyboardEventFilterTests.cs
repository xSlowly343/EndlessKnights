using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class KeyboardEventFilterTests
    {
        [TestCase((ushort)0x0000, true)]
        [TestCase((ushort)0x0002, true)]
        [TestCase((ushort)0x0001, false)]
        [TestCase((ushort)0x0003, false)]
        public void OnlyMakeEventsAreKeyDowns(ushort flags, bool expected)
        {
            Assert.That(KeyboardEventFilter.IsKeyDown(flags), Is.EqualTo(expected));
        }

        [Test]
        public void CountersDrainAndReset()
        {
            var counters = new InputCounters();
            counters.AddKeyDown();
            counters.AddKeyDown();

            Assert.That(counters.Drain().KeyDowns, Is.EqualTo(2));
            Assert.That(counters.Drain().KeyDowns, Is.EqualTo(0));
        }
    }
}
