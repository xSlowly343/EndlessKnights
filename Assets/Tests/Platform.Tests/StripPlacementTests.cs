using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class StripPlacementTests
    {
        private const int Width = 480;
        private const int Height = 180;

        private static readonly ScreenRect BottomTaskbar = new ScreenRect(0, 1032, 1920, 1080);

        private static void AssertRect(ScreenRect actual, int left, int top)
        {
            Assert.That(actual.Left, Is.EqualTo(left));
            Assert.That(actual.Top, Is.EqualTo(top));
            Assert.That(actual.Width, Is.EqualTo(Width));
            Assert.That(actual.Height, Is.EqualTo(Height));
        }

        [Test]
        public void BottomTaskbar_StripSitsOnTopOfItAtTheLeftByDefault()
        {
            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, BottomTaskbar, Width, Height, 0, 0);

            AssertRect(strip, 0, 852);
            Assert.That(strip.Bottom, Is.EqualTo(BottomTaskbar.Top));
        }

        [Test]
        public void TopTaskbar_StripHangsBelowIt()
        {
            var taskbar = new ScreenRect(0, 0, 1920, 48);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Top, taskbar, Width, Height, 0, 0);

            AssertRect(strip, 0, 48);
        }

        [Test]
        public void LeftTaskbar_StripSitsToTheRightAtTheTop()
        {
            var taskbar = new ScreenRect(0, 0, 62, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Left, taskbar, Width, Height, 0, 0);

            AssertRect(strip, 62, 0);
        }

        [Test]
        public void RightTaskbar_StripSitsToTheLeftAtTheTop()
        {
            var taskbar = new ScreenRect(1858, 0, 1920, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Right, taskbar, Width, Height, 0, 0);

            AssertRect(strip, 1378, 0);
        }

        [Test]
        public void GapMovesStripAwayFromTaskbar()
        {
            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, BottomTaskbar, Width, Height, 10, 0);

            Assert.That(strip.Bottom, Is.EqualTo(BottomTaskbar.Top - 10));
        }

        [Test]
        public void OffsetSlidesStripAlongHorizontalTaskbar()
        {
            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, BottomTaskbar, Width, Height, 0, 700);

            AssertRect(strip, 700, 852);
        }

        [Test]
        public void OffsetSlidesStripAlongVerticalTaskbar()
        {
            var taskbar = new ScreenRect(0, 0, 62, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Left, taskbar, Width, Height, 0, 300);

            AssertRect(strip, 62, 300);
        }

        [TestCase(-50, 0)]
        [TestCase(5000, 1440)]
        public void OffsetIsClampedToTaskbarLength(int offset, int expectedLeft)
        {
            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, BottomTaskbar, Width, Height, 0, offset);

            Assert.That(strip.Left, Is.EqualTo(expectedLeft));
        }

        [Test]
        public void SecondMonitorTaskbarUsesItsOwnCoordinates()
        {
            var taskbar = new ScreenRect(1920, 1392, 4480, 1440);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, taskbar, Width, Height, 0, 0);

            AssertRect(strip, 1920, 1212);
        }
    }
}
