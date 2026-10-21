using System;
using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class MouseGestureTests
    {
        private static int FeedFlicks(FlickDetector detector, double pxPerMs, double durationMs, double startMs = 0, double stepMs = 1)
        {
            var flicks = 0;
            for (var t = 0.0; t < durationMs; t += stepMs)
                if (detector.Add(pxPerMs * stepMs, startMs + t))
                    flicks++;
            return flicks;
        }

        [Test]
        public void FastSwipeIsOneFlick()
        {
            var detector = new FlickDetector();

            Assert.That(FeedFlicks(detector, pxPerMs: 8, durationMs: 120), Is.EqualTo(1));
        }

        [Test]
        public void SlowMovementIsNoFlick()
        {
            var detector = new FlickDetector();

            Assert.That(FeedFlicks(detector, pxPerMs: 1, durationMs: 2000), Is.EqualTo(0));
        }

        [Test]
        public void TwoSwipesWithAPauseAreTwoFlicks()
        {
            var detector = new FlickDetector();
            var first = FeedFlicks(detector, 8, 120);
            var pause = FeedFlicks(detector, 0.1, 300, startMs: 120);
            var second = FeedFlicks(detector, 8, 120, startMs: 420);

            Assert.That(first + pause + second, Is.EqualTo(2));
        }

        [Test]
        public void LongFastMovementStaysOneFlick()
        {
            var detector = new FlickDetector();

            Assert.That(FeedFlicks(detector, pxPerMs: 8, durationMs: 1000), Is.EqualTo(1));
        }

        private static int DrawCircles(CircleDetector detector, double turns, int stepsPerTurn = 36, double radius = 50, bool clockwise = false, double startMs = 0)
        {
            var total = 0;
            var steps = (int)(turns * stepsPerTurn);
            var direction = clockwise ? -1 : 1;
            for (var i = 0; i < steps; i++)
            {
                var a0 = direction * 2 * Math.PI * i / stepsPerTurn;
                var a1 = direction * 2 * Math.PI * (i + 1) / stepsPerTurn;
                var dx = radius * (Math.Cos(a1) - Math.Cos(a0));
                var dy = radius * (Math.Sin(a1) - Math.Sin(a0));
                total += detector.Add(dx, dy, startMs + i * 10);
            }
            return total;
        }

        [Test]
        public void ThreeCirclesAreCounted()
        {
            Assert.That(DrawCircles(new CircleDetector(), 3.2), Is.EqualTo(3));
        }

        [Test]
        public void ClockwiseCirclesCountToo()
        {
            Assert.That(DrawCircles(new CircleDetector(), 2.2, clockwise: true), Is.EqualTo(2));
        }

        [Test]
        public void StraightLineIsNoCircle()
        {
            var detector = new CircleDetector();
            var total = 0;
            for (var i = 0; i < 500; i++)
                total += detector.Add(5, 1, i * 10);

            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void ZigZagIsNoCircle()
        {
            var detector = new CircleDetector();
            var total = 0;
            for (var i = 0; i < 500; i++)
                total += detector.Add(i % 2 == 0 ? 10 : -10, i % 2 == 0 ? 5 : -5, i * 10);

            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void PauseResetsAHalfDrawnCircle()
        {
            var detector = new CircleDetector();
            var first = DrawCircles(detector, 0.6);
            var second = DrawCircles(detector, 0.6, startMs: 5000);

            Assert.That(first + second, Is.EqualTo(0));
        }

        [Test]
        public void JitterBelowMinimumStepIsIgnored()
        {
            Assert.That(DrawCircles(new CircleDetector(), 5, radius: 0.2), Is.EqualTo(0));
        }
    }
}
