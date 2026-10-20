using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class StripDragTests
    {
        [Test]
        public void SmallMovementIsStillAClick()
        {
            var drag = new StripDrag();
            drag.Press(100, 0);

            var moved = drag.TryMove(100 + StripDrag.StartThresholdPixels - 1, out var offset);

            Assert.That(moved, Is.False);
            Assert.That(drag.IsDragging, Is.False);
            Assert.That(offset, Is.EqualTo(0));
        }

        [Test]
        public void MovementPastThresholdDragsByFullDelta()
        {
            var drag = new StripDrag();
            drag.Press(100, 200);

            var moved = drag.TryMove(160, out var offset);

            Assert.That(moved, Is.True);
            Assert.That(drag.IsDragging, Is.True);
            Assert.That(offset, Is.EqualTo(260));
        }

        [Test]
        public void OnceDraggingSmallMovesStillFollow()
        {
            var drag = new StripDrag();
            drag.Press(100, 0);
            drag.TryMove(120, out _);

            var moved = drag.TryMove(118, out var offset);

            Assert.That(moved, Is.True);
            Assert.That(offset, Is.EqualTo(18));
        }

        [Test]
        public void DraggingLeftGivesNegativeDelta()
        {
            var drag = new StripDrag();
            drag.Press(500, 300);

            drag.TryMove(400, out var offset);

            Assert.That(offset, Is.EqualTo(200));
        }

        [Test]
        public void NoMovementWithoutPress()
        {
            var drag = new StripDrag();

            Assert.That(drag.TryMove(999, out _), Is.False);
        }

        [Test]
        public void ReleaseStopsDragging()
        {
            var drag = new StripDrag();
            drag.Press(100, 0);
            drag.TryMove(200, out _);

            drag.Release();

            Assert.That(drag.IsDragging, Is.False);
            Assert.That(drag.TryMove(300, out _), Is.False);
        }
    }
}
