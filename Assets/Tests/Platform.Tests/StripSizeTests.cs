using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class StripSizeTests
    {
        private static readonly StripSize Base = new StripSize(480, 180);

        [TestCase(96u, 480, 180)]
        [TestCase(120u, 600, 225)]
        [TestCase(144u, 720, 270)]
        [TestCase(192u, 960, 360)]
        public void ScalesWithMonitorDpi(uint dpi, int width, int height)
        {
            var scaled = Base.ScaledFor(dpi);

            Assert.That(scaled.Width, Is.EqualTo(width));
            Assert.That(scaled.Height, Is.EqualTo(height));
        }

        [Test]
        public void UnknownDpiFallsBackToBaseSize()
        {
            var scaled = Base.ScaledFor(0u);

            Assert.That(scaled.Width, Is.EqualTo(480));
            Assert.That(scaled.Height, Is.EqualTo(180));
        }

        [Test]
        public void ScaledStripStillDocksFlushAt150Percent()
        {
            var taskbar = new ScreenRect(0, 2088, 3840, 2160);
            var size = Base.ScaledFor(144u);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, taskbar, size.Width, size.Height, 0, 0);

            Assert.That(strip.Bottom, Is.EqualTo(taskbar.Top));
            Assert.That(strip.Width, Is.EqualTo(720));
            Assert.That(strip.Height, Is.EqualTo(270));
        }
    }
}
