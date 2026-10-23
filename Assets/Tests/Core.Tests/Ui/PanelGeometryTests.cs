using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PanelGeometryTests
    {
        [Test]
        public void FullPanelIsAsWideAsTheStripAndSplitsIntoFourRows()
        {
            var frame = PanelGeometry.Frame(PanelHeight.Full, false, true);

            Assert.That((frame.WidthPx, frame.HeightPx), Is.EqualTo((480, 600)));
            Assert.That((frame.HeaderPx, frame.SubTabsPx, frame.TabBarPx), Is.EqualTo((46, 40, 60)));
            Assert.That(frame.ContentPx, Is.EqualTo(454));
            Assert.That(frame.AreaHeightPx, Is.EqualTo(606));
        }

        [Test]
        public void HalfPanelDropsTheSubTabs()
        {
            var frame = PanelGeometry.Frame(PanelHeight.Half, false, true);

            Assert.That(frame.HeightPx, Is.EqualTo(320));
            Assert.That(frame.HasSubTabs, Is.False);
            Assert.That(frame.ContentPx, Is.EqualTo(214));
        }

        [Test]
        public void SectionWithoutSubTabsGivesTheRowToContent()
        {
            var frame = PanelGeometry.Frame(PanelHeight.Full, false, false);

            Assert.That(frame.ContentPx, Is.EqualTo(494));
        }

        [Test]
        public void WideTreeIs960By640()
        {
            var frame = PanelGeometry.Frame(PanelHeight.Half, true, true);

            Assert.That((frame.WidthPx, frame.HeightPx), Is.EqualTo((960, 640)));
            Assert.That(frame.ContentPx, Is.EqualTo(494));
        }

        [Test]
        public void SinglePanelTakesMuchLessScreenThanThreeWindows()
        {
            var frame = PanelGeometry.Frame(PanelHeight.Full, false, true);
            var threeWindows = 1322 * 600;

            Assert.That(frame.WidthPx * frame.HeightPx, Is.LessThan(threeWindows * 0.4));
        }
    }
}
