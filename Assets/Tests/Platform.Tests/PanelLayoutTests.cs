using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class PanelLayoutTests
    {
        private const int PanelWidth = 480;
        private const int PanelArea = 600 + 6;
        private const int WideWidth = 960;
        private const int WideArea = 640 + 6;

        private static readonly ScreenRect WorkArea = new ScreenRect(0, 0, 1920, 1032);

        [Test]
        public void CollapsedIsJustTheStrip()
        {
            var strip = new ScreenRect(0, 852, 480, 1032);

            var layout = PanelLayout.Collapsed(strip);

            Assert.That(layout.Window.Equals(strip), Is.True);
            Assert.That(layout.StripOffsetX, Is.EqualTo(0));
        }

        [Test]
        public void SinglePanelStandsRightAboveTheStripWithTheSameWidth()
        {
            var strip = new ScreenRect(0, 852, 480, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, PanelWidth, PanelArea);

            Assert.That((layout.Window.Left, layout.Window.Top, layout.Window.Right, layout.Window.Bottom), Is.EqualTo((0, 246, 480, 1032)));
            Assert.That(layout.StripOffsetX, Is.EqualTo(0));
            Assert.That(layout.StripOffsetFromBottom, Is.EqualTo(0));
            Assert.That(layout.PanelsAboveStrip, Is.True);
        }

        [Test]
        public void PanelGrowsAboveAStripLoweredOntoTheTaskbar()
        {
            var strip = new ScreenRect(0, 900, 480, 1080);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, PanelWidth, PanelArea);

            Assert.That((layout.Window.Top, layout.Window.Bottom), Is.EqualTo((900 - PanelArea, 1080)));
            Assert.That(layout.StripOffsetFromBottom, Is.EqualTo(0));
        }

        [Test]
        public void SinglePanelFollowsTheStripAlongTheTaskbar()
        {
            var strip = new ScreenRect(1440, 852, 1920, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, PanelWidth, PanelArea);

            Assert.That(layout.Window.Left, Is.EqualTo(1440));
            Assert.That(layout.Window.Width, Is.EqualTo(480));
            Assert.That(layout.StripOffsetX, Is.EqualTo(0));
        }

        [Test]
        public void WideTreeGrowsToTheRightWhenTheStripIsAtTheLeft()
        {
            var strip = new ScreenRect(0, 852, 480, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That((layout.Window.Left, layout.Window.Right), Is.EqualTo((0, 960)));
            Assert.That(layout.Window.Top, Is.EqualTo(1032 - 180 - WideArea));
            Assert.That(layout.StripOffsetX, Is.EqualTo(0));
        }

        [Test]
        public void WideTreeGrowsToTheLeftWhenThereIsMoreRoomThere()
        {
            var strip = new ScreenRect(1000, 852, 1480, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That((layout.Window.Left, layout.Window.Right), Is.EqualTo((520, 1480)));
            Assert.That(layout.StripOffsetX, Is.EqualTo(480));
        }

        [Test]
        public void WideTreeShiftsInsideTheScreenAtTheRightEdge()
        {
            var strip = new ScreenRect(1440, 852, 1920, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That(layout.Window.Right, Is.EqualTo(1920));
            Assert.That(layout.Window.Left, Is.EqualTo(960));
            Assert.That(layout.StripOffsetX, Is.EqualTo(480));
        }

        [Test]
        public void WideTreeNearTheLeftEdgeStaysOnScreen()
        {
            var strip = new ScreenRect(300, 852, 780, 1032);
            var small = new ScreenRect(0, 0, 1000, 1032);

            var layout = PanelLayout.Expanded(strip, small, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That((layout.Window.Left, layout.Window.Right), Is.EqualTo((0, 960)));
            Assert.That(layout.StripOffsetX, Is.EqualTo(300));
        }

        [Test]
        public void NeverTallerOrWiderThanTheWorkArea()
        {
            var strip = new ScreenRect(0, 560, 480, 740);
            var small = new ScreenRect(0, 0, 800, 740);

            var layout = PanelLayout.Expanded(strip, small, TaskbarEdge.Bottom, 1354, 2000);

            Assert.That(layout.Window.Top, Is.EqualTo(0));
            Assert.That(layout.Window.Width, Is.EqualTo(800));
        }

        [Test]
        public void TopTaskbarOpensThePanelDownward()
        {
            var strip = new ScreenRect(0, 48, 480, 228);

            var layout = PanelLayout.Expanded(strip, new ScreenRect(0, 48, 1920, 1080), TaskbarEdge.Top, PanelWidth, PanelArea);

            Assert.That(layout.Window.Top, Is.EqualTo(48));
            Assert.That(layout.Window.Height, Is.EqualTo(180 + PanelArea));
            Assert.That(layout.StripOffsetFromBottom, Is.EqualTo(PanelArea));
            Assert.That(layout.PanelsAboveStrip, Is.False);
        }

        [Test]
        public void NarrowPanelStandsAboveTheStripInsideTheWideWindow()
        {
            var strip = new ScreenRect(700, 852, 1180, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That(layout.PanelLeft(PanelWidth, 480), Is.EqualTo(0));
            Assert.That(layout.PanelLeft(WideWidth, 480), Is.EqualTo(-layout.StripOffsetX));
        }

        [Test]
        public void WidePanelGrowsTowardsTheRoomyScreenSide()
        {
            var strip = new ScreenRect(0, 852, 480, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That(layout.PanelLeft(PanelWidth, 480), Is.EqualTo(0));
            Assert.That(layout.PanelLeft(WideWidth, 480), Is.EqualTo(0));
        }

        [Test]
        public void PanelNeverLeavesTheWindow()
        {
            var strip = new ScreenRect(1440, 852, 1920, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);

            Assert.That(layout.PanelLeft(WideWidth, 480), Is.EqualTo(-480));
            Assert.That(layout.PanelLeft(PanelWidth, 480), Is.EqualTo(0));
        }

        [Test]
        public void RoomBesideTheNarrowPanelIsTheRestOfTheWideWindow()
        {
            var strip = new ScreenRect(1440, 852, 1920, 1032);

            var layout = PanelLayout.Expanded(strip, WorkArea, TaskbarEdge.Bottom, WideWidth, WideArea);
            var room = layout.RoomBeside(layout.PanelLeft(PanelWidth, 480), PanelWidth);

            Assert.That(room.Left + room.Right, Is.EqualTo(layout.Window.Width - PanelWidth));
            Assert.That(room.Left, Is.GreaterThan(room.Right));
        }
    }
}
