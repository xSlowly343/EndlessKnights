using Game.Core.Input;
using Game.Platform.Windows.Input;
using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class MouseButtonsTests
    {
        [Test]
        public void DecodesButtonDowns()
        {
            var buttons = MouseButtons.Decode(0x0001 | 0x0004 | 0x0010, 0);

            Assert.That((buttons.Left, buttons.Right, buttons.Middle), Is.EqualTo((1, 1, 1)));
            Assert.That(buttons.AnyClick, Is.True);
        }

        [Test]
        public void ButtonUpsAreNotClicks()
        {
            var buttons = MouseButtons.Decode(0x0002 | 0x0008 | 0x0020, 0);

            Assert.That(buttons.AnyClick, Is.False);
        }

        [Test]
        public void SideButtonsAreNotCounted()
        {
            Assert.That(MouseButtons.Decode(0x0040 | 0x0100, 0).AnyClick, Is.False);
        }

        [Test]
        public void WheelDeltaOnlyWithWheelFlag()
        {
            Assert.That(MouseButtons.Decode(0x0400, -240).WheelDelta, Is.EqualTo(-240));
            Assert.That(MouseButtons.Decode(0x0000, -240).WheelDelta, Is.EqualTo(0));
        }

        [Test]
        public void WheelNotchesCountInBothDirections()
        {
            var wheel = new WheelAccumulator();

            Assert.That(wheel.AddDelta(120), Is.EqualTo(1));
            Assert.That(wheel.AddDelta(-240), Is.EqualTo(2));
        }

        [Test]
        public void SmoothScrollingAddsUpToNotches()
        {
            var wheel = new WheelAccumulator();
            var ticks = 0;
            for (var i = 0; i < 12; i++)
                ticks += wheel.AddDelta(30);

            Assert.That(ticks, Is.EqualTo(3));
        }

        [Test]
        public void OwnWindowAreaContainsOnlyItsRectangle()
        {
            var area = new OwnWindowArea();
            Assert.That(area.Contains(10, 10), Is.False);

            area.Set(new ScreenRect(0, 892, 480, 1032));

            Assert.That(area.Contains(10, 900), Is.True);
            Assert.That(area.Contains(480, 900), Is.False);
            Assert.That(area.Contains(10, 800), Is.False);
        }

        [Test]
        public void CountersCarryClicksWheelGesturesAndRhythm()
        {
            var counters = new InputCounters();
            counters.AddClicks(MouseButtons.Decode(0x0001, 0));
            counters.AddClicks(MouseButtons.Decode(0x0004, 0));
            counters.AddWheelTicks(3);
            counters.AddFlick();
            counters.AddCircleTurns(2);
            counters.SetRhythm(0.42f);
            counters.MarkInput(1000);

            var snapshot = counters.Drain(4500);

            Assert.That(snapshot.LeftClicks, Is.EqualTo(1));
            Assert.That(snapshot.RightClicks, Is.EqualTo(1));
            Assert.That(snapshot.WheelTicks, Is.EqualTo(3));
            Assert.That(snapshot.Flicks, Is.EqualTo(1));
            Assert.That(snapshot.CircleTurns, Is.EqualTo(2f));
            Assert.That(snapshot.RhythmCv, Is.EqualTo(0.42f));
            Assert.That(snapshot.SecondsSinceLastInput, Is.EqualTo(3.5));
            Assert.That(counters.Drain(4500).LeftClicks, Is.EqualTo(0));
        }

        [Test]
        public void NoInputYetMeansZeroIdleTime()
        {
            Assert.That(new InputCounters().Drain(99999).SecondsSinceLastInput, Is.EqualTo(0));
        }
    }
}
