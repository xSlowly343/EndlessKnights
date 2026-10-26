using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class TooltipPlacementTests
    {
        [Test]
        public void TooltipGoesToTheSideWithMoreRoom()
        {
            Assert.That(TooltipPlacement.Side(480, 0, 280, 12), Is.EqualTo(TooltipSide.Left));
            Assert.That(TooltipPlacement.Side(0, 480, 280, 12), Is.EqualTo(TooltipSide.Right));
            Assert.That(TooltipPlacement.Side(300, 400, 280, 12), Is.EqualTo(TooltipSide.Right));
        }

        [Test]
        public void WithoutRoomOutsideTheTooltipStaysOverThePanel()
        {
            Assert.That(TooltipPlacement.Side(100, 200, 280, 12), Is.EqualTo(TooltipSide.Inside));
        }

        [Test]
        public void TooltipIsCentredOnTheNodeAndKeptWithinThePanel()
        {
            Assert.That(TooltipPlacement.Top(300, 100, 0, 600), Is.EqualTo(350));
            Assert.That(TooltipPlacement.Top(590, 100, 0, 600), Is.EqualTo(600));
            Assert.That(TooltipPlacement.Top(10, 100, 0, 600), Is.EqualTo(100));
        }
    }
}
