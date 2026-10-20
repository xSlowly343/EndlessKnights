using Game.Core.Input;
using NUnit.Framework;

namespace Game.Core.Tests.Input
{
    public class FakeInputSourceTests
    {
        [Test]
        public void DrainReturnsEverythingAccumulated()
        {
            var source = new FakeInputSource();
            source.PressKeys(5);
            source.PressEnter();
            source.PressBackspace(2);
            source.PressSpace();
            source.ClickLeft(3);
            source.ClickRight();
            source.ClickMiddle();
            source.ScrollWheel(4);
            source.MoveMouse(120.5f);
            source.Flick();
            source.Circle(1.5f);

            var snapshot = source.Drain();

            Assert.That(snapshot.KeyDowns, Is.EqualTo(9));
            Assert.That(snapshot.EnterDowns, Is.EqualTo(1));
            Assert.That(snapshot.BackspaceDowns, Is.EqualTo(2));
            Assert.That(snapshot.SpaceDowns, Is.EqualTo(1));
            Assert.That(snapshot.LeftClicks, Is.EqualTo(3));
            Assert.That(snapshot.RightClicks, Is.EqualTo(1));
            Assert.That(snapshot.MiddleClicks, Is.EqualTo(1));
            Assert.That(snapshot.WheelTicks, Is.EqualTo(4));
            Assert.That(snapshot.MouseDistancePx, Is.EqualTo(120.5f));
            Assert.That(snapshot.Flicks, Is.EqualTo(1));
            Assert.That(snapshot.CircleTurns, Is.EqualTo(1.5f));
            Assert.That(snapshot.HasAnyInput, Is.True);
        }

        [Test]
        public void DrainResetsCountersButKeepsState()
        {
            var source = new FakeInputSource { SecondsSinceLastInput = 12.5, RhythmCv = 0.4f, Suppressed = true };
            source.PressKeys(3);
            source.MoveMouse(10f);
            source.Drain();

            var second = source.Drain();

            Assert.That(second.KeyDowns, Is.EqualTo(0));
            Assert.That(second.MouseDistancePx, Is.EqualTo(0f));
            Assert.That(second.HasAnyInput, Is.False);
            Assert.That(second.SecondsSinceLastInput, Is.EqualTo(12.5));
            Assert.That(second.RhythmCv, Is.EqualTo(0.4f));
            Assert.That(second.Suppressed, Is.True);
        }

        [Test]
        public void EmptySnapshotHasNoInput()
        {
            Assert.That(InputSnapshot.Empty.HasAnyInput, Is.False);
            Assert.That(InputSnapshot.Empty.KeyDowns, Is.EqualTo(0));
        }

        [Test]
        public void WorksThroughTheInterface()
        {
            var fake = new FakeInputSource();
            IInputSource source = fake;
            fake.ClickLeft(2);

            Assert.That(source.Drain().LeftClicks, Is.EqualTo(2));
        }
    }
}
