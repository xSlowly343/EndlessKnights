using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class ClickThroughPolicyTests
    {
        [Test]
        public void EmptySpacePassesClicks()
        {
            Assert.That(ClickThroughPolicy.ShouldPassClicks(cursorOverSprite: false, mouseButtonHeld: false, passingClicksNow: false), Is.True);
        }

        [Test]
        public void SpriteCatchesClicks()
        {
            Assert.That(ClickThroughPolicy.ShouldPassClicks(cursorOverSprite: true, mouseButtonHeld: false, passingClicksNow: true), Is.False);
        }

        [Test]
        public void HeldButtonKeepsCatchingWhileDraggingOffTheSprite()
        {
            Assert.That(ClickThroughPolicy.ShouldPassClicks(cursorOverSprite: false, mouseButtonHeld: true, passingClicksNow: false), Is.False);
        }

        [Test]
        public void HeldButtonFromElsewhereDoesNotGrabTheWindow()
        {
            Assert.That(ClickThroughPolicy.ShouldPassClicks(cursorOverSprite: true, mouseButtonHeld: true, passingClicksNow: true), Is.True);
        }
    }
}
