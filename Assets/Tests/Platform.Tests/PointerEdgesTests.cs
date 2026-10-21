using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class PointerEdgesTests
    {
        [Test]
        public void PressHoldRelease()
        {
            var edges = new PointerEdges();

            edges.Update(true);
            Assert.That((edges.Pressed, edges.Held, edges.Released), Is.EqualTo((true, true, false)));

            edges.Update(true);
            Assert.That((edges.Pressed, edges.Held, edges.Released), Is.EqualTo((false, true, false)));

            edges.Update(false);
            Assert.That((edges.Pressed, edges.Held, edges.Released), Is.EqualTo((false, false, true)));

            edges.Update(false);
            Assert.That((edges.Pressed, edges.Held, edges.Released), Is.EqualTo((false, false, false)));
        }

        [Test]
        public void ClickFasterThanAFrameIsStillAPress()
        {
            var edges = new PointerEdges();

            edges.Update(false, pressedSinceLastPoll: true);

            Assert.That(edges.Pressed, Is.True);
            Assert.That(edges.Held, Is.False);
        }

        [Test]
        public void PressedBitWhileHeldIsNotASecondPress()
        {
            var edges = new PointerEdges();
            edges.Update(true);

            edges.Update(true, pressedSinceLastPoll: true);

            Assert.That(edges.Pressed, Is.False);
        }
    }
}
