using Game.Core.Input;
using NUnit.Framework;

namespace Game.Core.Tests.Input
{
    public class ObservedInputSourceTests
    {
        [Test]
        public void ForwardsSnapshotsToTheConsumerAndTheObserver()
        {
            var fake = new FakeInputSource();
            var observed = new ObservedInputSource(fake);
            var seen = 0;
            observed.Drained += snapshot => seen += snapshot.KeyDowns;
            fake.PressKeys(3);

            var drained = observed.Drain();

            Assert.That(drained.KeyDowns, Is.EqualTo(3));
            Assert.That(seen, Is.EqualTo(3));
        }
    }
}
