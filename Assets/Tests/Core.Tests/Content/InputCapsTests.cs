using Game.Core.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class InputCapsTests
    {
        private static string RealFile() => TestData.ReadGameDataFile(InputCaps.FileName);

        [Test]
        public void RealDataFileMatchesConcept()
        {
            var caps = InputCaps.Load(RealFile());

            Assert.That(caps.Keys.FullRatePerSecond, Is.EqualTo(8));
            Assert.That(caps.Keys.DiminishingUntilPerSecond, Is.EqualTo(15));
            Assert.That(caps.Clicks.FullRatePerSecond, Is.EqualTo(10));
            Assert.That(caps.MaxMousePxPerSecond, Is.EqualTo(3000));
        }

        [Test]
        public void DetectionThresholdsMatchTheDetectorDefaults()
        {
            var detection = InputCaps.Load(RealFile()).Detection;

            Assert.That(detection.FlickWindowMs, Is.EqualTo(80));
            Assert.That(detection.FlickSpeedPxPerSecond, Is.EqualTo(4000));
            Assert.That(detection.CircleMinStepPx, Is.EqualTo(2));
            Assert.That(detection.CirclePauseMs, Is.EqualTo(300));
            Assert.That(detection.JigglerWindowMs, Is.EqualTo(250));
            Assert.That(detection.JigglerMinDisplacementPx, Is.EqualTo(8));
            Assert.That(detection.RhythmIntervals, Is.EqualTo(200));
            Assert.That(detection.RhythmMaxIntervalMs, Is.EqualTo(2000));
        }

        [TestCase("\"diminishingUntilPerSecond\": 15", "\"diminishingUntilPerSecond\": 5", "keys.diminishingUntilPerSecond: must not be lower than fullRatePerSecond (8)")]
        [TestCase("\"maxPxPerSecond\": 3000", "\"maxPxPerSecond\": 0", "mouse.maxPxPerSecond: 0 is outside the allowed range")]
        [TestCase("\"intervals\": 200", "\"intervals\": 2", "detection.rhythm.intervals: 2 is outside the allowed range")]
        [TestCase("\"windowMs\": 80", "\"windowMs\": \"fast\"", "detection.flick.windowMs: expected a number, got a string")]
        public void BrokenCapsAreReportedByPath(string original, string broken, string message)
        {
            var error = Assert.Throws<ContentException>(() => InputCaps.Load(RealFile().Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain("input_caps.json: " + message));
        }
    }
}
