using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class JigglerFilterTests
    {
        private const double Tolerance = 1e-9;

        private static double Feed(JigglerFilter filter, double dx, double dy, int events, double stepMs, double startMs = 0, bool alternate = false)
        {
            var accepted = 0.0;
            for (var i = 0; i < events; i++)
            {
                var sign = alternate && i % 2 == 1 ? -1 : 1;
                accepted += filter.Add(sign * dx, sign * dy, startMs + i * stepMs);
            }
            return accepted + filter.Flush();
        }

        [Test]
        public void JigglerWobbleIsIgnored()
        {
            var filter = new JigglerFilter();

            Assert.That(Feed(filter, 1, 0, events: 600, stepMs: 100, alternate: true), Is.EqualTo(0).Within(Tolerance));
        }

        [Test]
        public void SlowSinglePixelStepsAreIgnored()
        {
            var filter = new JigglerFilter();

            Assert.That(Feed(filter, 1, 0, events: 100, stepMs: 1000), Is.EqualTo(0).Within(Tolerance));
        }

        [Test]
        public void RealMovementCountsFully()
        {
            var filter = new JigglerFilter();

            Assert.That(Feed(filter, 3, 4, events: 100, stepMs: 8), Is.EqualTo(500).Within(Tolerance));
        }

        [Test]
        public void SmallButDeliberateMoveCounts()
        {
            var filter = new JigglerFilter();

            Assert.That(Feed(filter, 2, 0, events: 5, stepMs: 10), Is.EqualTo(10).Within(Tolerance));
        }

        [Test]
        public void ResultArrivesWhenTheWindowCloses()
        {
            var filter = new JigglerFilter();
            Assert.That(filter.Add(20, 0, 0), Is.EqualTo(0));
            Assert.That(filter.Add(20, 0, 100), Is.EqualTo(0));

            Assert.That(filter.Add(1, 0, 300), Is.EqualTo(40).Within(Tolerance));
        }
    }
}
