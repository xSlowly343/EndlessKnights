using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class MouseMotionTests
    {
        private const ushort Relative = 0;
        private const ushort Absolute = MouseMotion.MoveAbsolute;
        private const double Tolerance = 1e-9;

        [Test]
        public void RelativeDeltaIsVectorLengthAtDefaultSpeed()
        {
            var motion = new MouseMotion();
            motion.SetPointerSpeed(MouseMotion.DefaultPointerSpeed);

            Assert.That(motion.DistancePx(Relative, 3, 4, 1920, 1080), Is.EqualTo(5.0).Within(Tolerance));
        }

        [TestCase(1, 1.0 / 32)]
        [TestCase(6, 0.5)]
        [TestCase(10, 1.0)]
        [TestCase(14, 2.0)]
        [TestCase(20, 3.5)]
        [TestCase(0, 1.0 / 32)]
        [TestCase(99, 3.5)]
        public void PointerSpeedScalesRelativeDistance(int speed, double factor)
        {
            var motion = new MouseMotion();
            motion.SetPointerSpeed(speed);

            Assert.That(motion.DistancePx(Relative, 10, 0, 1920, 1080), Is.EqualTo(10.0 * factor).Within(Tolerance));
        }

        [Test]
        public void FirstAbsolutePointGivesNoDistance()
        {
            var motion = new MouseMotion();

            Assert.That(motion.DistancePx(Absolute, 32768, 32768, 1920, 1080), Is.EqualTo(0.0));
        }

        [Test]
        public void AbsoluteMovesAreMeasuredInScreenPixels()
        {
            var motion = new MouseMotion();
            motion.DistancePx(Absolute, 0, 0, 1920, 1080);

            var distance = motion.DistancePx(Absolute, 65535, 0, 1920, 1080);

            Assert.That(distance, Is.EqualTo(1920.0).Within(Tolerance));
        }

        [Test]
        public void AbsoluteDistanceIgnoresPointerSpeed()
        {
            var motion = new MouseMotion();
            motion.SetPointerSpeed(20);
            motion.DistancePx(Absolute, 0, 0, 1000, 1000);

            Assert.That(motion.DistancePx(Absolute, 0, 65535, 1000, 1000), Is.EqualTo(1000.0).Within(Tolerance));
        }

        [Test]
        public void RelativeEventResetsAbsoluteStartPoint()
        {
            var motion = new MouseMotion();
            motion.DistancePx(Absolute, 0, 0, 1000, 1000);
            motion.DistancePx(Relative, 1, 0, 1000, 1000);

            Assert.That(motion.DistancePx(Absolute, 65535, 65535, 1000, 1000), Is.EqualTo(0.0));
        }

        [Test]
        public void CountersAccumulateMouseDistance()
        {
            var counters = new InputCounters();
            counters.AddMouseDistance(12.5);
            counters.AddMouseDistance(7.25);
            counters.AddMouseDistance(-3);

            Assert.That(counters.Drain().MouseDistancePx, Is.EqualTo(19.75f).Within(0.001f));
            Assert.That(counters.Drain().MouseDistancePx, Is.EqualTo(0f));
        }
    }
}
