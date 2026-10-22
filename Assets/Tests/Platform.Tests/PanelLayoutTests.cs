using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class PanelLayoutTests
    {
        private static readonly ScreenRect WorkArea = new ScreenRect(0, 0, 1920, 1032);

        [Test]
        public void CollapsedIsJustTheStrip()
        {
            var strip = new ScreenRect(0, 892, 480, 1032);

            var layout = PanelLayout.Collapsed(strip);

            Assert.That(layout.Window.Equals(strip), Is.True);
            Assert.That(layout.StripOffsetX, Is.EqualTo(0));
        }

        [Test]
        public void ExpandsUpwardKeepingTheStripInPlace()
        {
            var strip = new ScreenRect(0, 892, 480, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, 1354, 592);

            Assert.That((layout.Window.Left, layout.Window.Top, layout.Window.Right, layout.Window.Bottom), Is.EqualTo((0, 300, 1354, 1032)));
            Assert.That(layout.StripOffsetX, Is.EqualTo(0));
            Assert.That(layout.StripOffsetFromBottom, Is.EqualTo(0));
            Assert.That(layout.PanelsAboveStrip, Is.True);
        }

        [Test]
        public void ShiftsLeftWhenTheStripIsNearTheRightEdge()
        {
            var strip = new ScreenRect(1440, 892, 1920, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, 1354, 592);

            Assert.That(layout.Window.Right, Is.EqualTo(1920));
            Assert.That(layout.Window.Left, Is.EqualTo(566));
            Assert.That(layout.StripOffsetX, Is.EqualTo(874));
        }

        [Test]
        public void NeverTallerOrWiderThanTheWorkArea()
        {
            var strip = new ScreenRect(0, 600, 480, 740);
            var small = new ScreenRect(0, 0, 1280, 740);

            var layout = PanelLayout.Expanded(strip, small, TaskbarEdge.Bottom, 1354, 2000);

            Assert.That(layout.Window.Top, Is.EqualTo(0));
            Assert.That(layout.Window.Width, Is.EqualTo(1280));
        }

        [Test]
        public void TopTaskbarExpandsDownward()
        {
            var strip = new ScreenRect(0, 48, 480, 188);

            var layout = PanelLayout.Expanded(strip, new ScreenRect(0, 48, 1920, 1080), TaskbarEdge.Top, 1354, 592);

            Assert.That(layout.Window.Top, Is.EqualTo(48));
            Assert.That(layout.Window.Height, Is.EqualTo(140 + 592));
            Assert.That(layout.StripOffsetFromBottom, Is.EqualTo(592));
            Assert.That(layout.PanelsAboveStrip, Is.False);
        }
    }
}
