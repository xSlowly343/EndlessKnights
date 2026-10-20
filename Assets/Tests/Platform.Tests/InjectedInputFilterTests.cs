using System;
using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class InjectedInputFilterTests
    {
        [Test]
        public void EventWithoutDeviceIsInjected()
        {
            Assert.That(InjectedInputFilter.IsInjected(IntPtr.Zero), Is.True);
        }

        [Test]
        public void EventFromRealDeviceIsNotInjected()
        {
            Assert.That(InjectedInputFilter.IsInjected(new IntPtr(0x0001_0045)), Is.False);
        }
    }
}
