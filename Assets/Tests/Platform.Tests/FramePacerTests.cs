using System;
using Game.Platform.Windows.Timing;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class FramePacerTests
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void FirstFrameWaitsAFullInterval()
        {
            var pacer = new FramePacer(60);

            Assert.That(pacer.SecondsToWait(10.0), Is.EqualTo(1.0 / 60).Within(Tolerance));
        }

        [Test]
        public void WaitsOnlyTheRestOfTheInterval()
        {
            var pacer = new FramePacer(50);
            pacer.SecondsToWait(0.0);

            var wait = pacer.SecondsToWait(0.02 + 0.005);

            Assert.That(wait, Is.EqualTo(0.015).Within(Tolerance));
        }

        [Test]
        public void OversleepIsPaidBackOnTheNextFrame()
        {
            var pacer = new FramePacer(50);
            pacer.SecondsToWait(0.0);

            var wait = pacer.SecondsToWait(0.035);

            Assert.That(wait, Is.EqualTo(0.005).Within(Tolerance));
        }

        [Test]
        public void FallingFarBehindRestartsTheSchedule()
        {
            var pacer = new FramePacer(60);
            pacer.SecondsToWait(0.0);

            var wait = pacer.SecondsToWait(5.0);

            Assert.That(wait, Is.EqualTo(1.0 / 60).Within(Tolerance));
        }

        [Test]
        public void ChangingFrameRateRestartsTheSchedule()
        {
            var pacer = new FramePacer(144);
            pacer.SecondsToWait(0.0);

            pacer.SetFrameRate(30);

            Assert.That(pacer.SecondsToWait(1.0), Is.EqualTo(1.0 / 30).Within(Tolerance));
        }

        [Test]
        public void RejectsNonPositiveFrameRate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FramePacer(0));
        }
    }
}
