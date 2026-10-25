using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class SurfaceResizeGateTests
    {
        private static readonly ScreenRect Strip = new ScreenRect(0, 852, 480, 1032);
        private static readonly ScreenRect WorkArea = new ScreenRect(0, 0, 1920, 1032);

        private static PanelLayout Expanded => PanelLayout.Expanded(Strip, WorkArea, TaskbarEdge.Bottom, 480, 606);

        [Test]
        public void ExpandedLayoutWaitsUntilTheSurfaceGrows()
        {
            var gate = new SurfaceResizeGate();
            gate.Request(Expanded);

            Assert.That(gate.TryTake(480, 180, out _), Is.False);
            Assert.That(gate.TryTake(480, 786, out var layout), Is.True);
            Assert.That(layout.Window.Height, Is.EqualTo(786));
            Assert.That(gate.HasPending, Is.False);
        }

        [Test]
        public void SameSizeMoveFitsAtOnce()
        {
            var gate = new SurfaceResizeGate();
            gate.Request(PanelLayout.Collapsed(Strip));

            Assert.That(gate.TryTake(480, 180, out _), Is.True);
        }

        [Test]
        public void GivesUpWaitingAfterAFewFrames()
        {
            var gate = new SurfaceResizeGate();
            gate.Request(Expanded);

            for (var i = 1; i < SurfaceResizeGate.MaxWaitFrames; i++)
                Assert.That(gate.TryTake(480, 180, out _), Is.False);
            Assert.That(gate.TryTake(480, 180, out _), Is.True);
        }

        [Test]
        public void NewRequestReplacesThePendingOne()
        {
            var gate = new SurfaceResizeGate();
            gate.Request(Expanded);
            gate.TryTake(480, 180, out _);
            gate.Request(PanelLayout.Collapsed(Strip));

            Assert.That(gate.TryTake(480, 180, out var layout), Is.True);
            Assert.That(layout.Window.Height, Is.EqualTo(180));
        }

        [Test]
        public void NothingToTakeWithoutARequest()
        {
            Assert.That(new SurfaceResizeGate().TryTake(480, 180, out _), Is.False);
        }
    }
}
