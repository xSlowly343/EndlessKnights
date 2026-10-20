using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class StripPlacementTests
    {
        private const int Width = 480;
        private const int Height = 140;

        private static void AssertRect(ScreenRect actual, int left, int top)
        {
            Assert.That(actual.Left, Is.EqualTo(left));
            Assert.That(actual.Top, Is.EqualTo(top));
            Assert.That(actual.Width, Is.EqualTo(Width));
            Assert.That(actual.Height, Is.EqualTo(Height));
        }

        [Test]
        public void BottomTaskbar_StripSitsOnTopOfItAtTheRight()
        {
            var taskbar = new ScreenRect(0, 1032, 1920, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, taskbar, Width, Height, 0);

            AssertRect(strip, 1440, 892);
            Assert.That(strip.Bottom, Is.EqualTo(taskbar.Top));
            Assert.That(strip.Right, Is.EqualTo(taskbar.Right));
        }

        [Test]
        public void TopTaskbar_StripHangsBelowIt()
        {
            var taskbar = new ScreenRect(0, 0, 1920, 48);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Top, taskbar, Width, Height, 0);

            AssertRect(strip, 1440, 48);
        }

        [Test]
        public void LeftTaskbar_StripSitsToTheRightAtTheBottom()
        {
            var taskbar = new ScreenRect(0, 0, 62, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Left, taskbar, Width, Height, 0);

            AssertRect(strip, 62, 940);
        }

        [Test]
        public void RightTaskbar_StripSitsToTheLeftAtTheBottom()
        {
            var taskbar = new ScreenRect(1858, 0, 1920, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Right, taskbar, Width, Height, 0);

            AssertRect(strip, 1378, 940);
        }

        [Test]
        public void GapMovesStripAwayFromTaskbar()
        {
            var taskbar = new ScreenRect(0, 1032, 1920, 1080);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, taskbar, Width, Height, 10);

            Assert.That(strip.Bottom, Is.EqualTo(taskbar.Top - 10));
        }

        [Test]
        public void SecondMonitorTaskbarUsesItsOwnCoordinates()
        {
            var taskbar = new ScreenRect(1920, 1392, 4480, 1440);

            var strip = StripPlacement.NextToTaskbar(TaskbarEdge.Bottom, taskbar, Width, Height, 0);

            AssertRect(strip, 4000, 1252);
        }
    }
}
