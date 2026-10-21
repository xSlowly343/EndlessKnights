using Game.Core.Math;
using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class RhythmMeterTests
    {
        [Test]
        public void PerfectlyEvenRhythmIsNearZero()
        {
            var meter = new RhythmMeter();
            for (var i = 0; i < 300; i++)
                meter.Record(i * 125.0);

            Assert.That(meter.CoefficientOfVariation(), Is.LessThan(0.01f));
        }

        [Test]
        public void HumanLikeTypingIsAboveThreshold()
        {
            var meter = new RhythmMeter();
            var rng = new Rng(17);
            var time = 0.0;
            for (var i = 0; i < 300; i++)
            {
                time += 60 + rng.NextDouble() * 300;
                meter.Record(time);
            }

            Assert.That(meter.CoefficientOfVariation(), Is.GreaterThan(0.3f));
        }

        [Test]
        public void MacroWithTinyJitterStaysLow()
        {
            var meter = new RhythmMeter();
            var rng = new Rng(3);
            var time = 0.0;
            for (var i = 0; i < 300; i++)
            {
                time += 100 + rng.NextDouble() * 4;
                meter.Record(time);
            }

            Assert.That(meter.CoefficientOfVariation(), Is.LessThan(0.05f));
        }

        [Test]
        public void TooFewEventsGiveNoEstimate()
        {
            var meter = new RhythmMeter();
            for (var i = 0; i < 5; i++)
                meter.Record(i * 100.0 + (i % 2) * 50);

            Assert.That(meter.HasEstimate, Is.False);
            Assert.That(meter.CoefficientOfVariation(), Is.EqualTo(0f));
        }

        [Test]
        public void LongPausesAreNotIntervals()
        {
            var meter = new RhythmMeter();
            var time = 0.0;
            for (var burst = 0; burst < 10; burst++)
            {
                for (var i = 0; i < 20; i++)
                {
                    time += 100;
                    meter.Record(time);
                }
                time += 60000;
            }

            Assert.That(meter.CoefficientOfVariation(), Is.LessThan(0.01f));
        }

        [Test]
        public void OnlyTheLastWindowCounts()
        {
            var meter = new RhythmMeter(capacity: 50);
            var rng = new Rng(9);
            var time = 0.0;
            for (var i = 0; i < 200; i++)
            {
                time += 60 + rng.NextDouble() * 300;
                meter.Record(time);
            }
            for (var i = 0; i < 100; i++)
            {
                time += 100;
                meter.Record(time);
            }

            Assert.That(meter.CoefficientOfVariation(), Is.LessThan(0.01f));
        }
    }
}
