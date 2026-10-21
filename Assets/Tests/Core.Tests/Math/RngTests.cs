using System;
using System.Linq;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Math
{
    public class RngTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);

            for (var i = 0; i < 1000; i++)
                Assert.That(a.NextULong(), Is.EqualTo(b.NextULong()));
        }

        [Test]
        public void DifferentSeedsGiveDifferentSequences()
        {
            var a = new Rng(1);
            var b = new Rng(2);

            Assert.That(Enumerable.Range(0, 10).Select(_ => a.NextULong()),
                Is.Not.EqualTo(Enumerable.Range(0, 10).Select(_ => b.NextULong())));
        }

        [Test]
        public void ZeroSeedStillProducesNumbers()
        {
            var rng = new Rng(0);

            Assert.That(Enumerable.Range(0, 10).Select(_ => rng.NextULong()).Distinct().Count(), Is.EqualTo(10));
        }

        [Test]
        public void StateRoundTripContinuesTheSameSequence()
        {
            var original = new Rng(7);
            original.NextULong();
            var (s0, s1) = original.State;
            var restored = Rng.FromState(s0, s1);

            for (var i = 0; i < 100; i++)
                Assert.That(restored.NextULong(), Is.EqualTo(original.NextULong()));
        }

        [Test]
        public void AllZeroStateIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Rng.FromState(0, 0));
        }

        [Test]
        public void NextDoubleStaysInUnitRangeAndAveragesHalf()
        {
            var rng = new Rng(123);
            var sum = 0.0;
            for (var i = 0; i < 100000; i++)
            {
                var value = rng.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                sum += value;
            }

            Assert.That(sum / 100000, Is.EqualTo(0.5).Within(0.01));
        }

        [Test]
        public void NextIntCoversRangeEvenly()
        {
            var rng = new Rng(99);
            var counts = new int[6];
            for (var i = 0; i < 60000; i++)
                counts[rng.NextInt(0, 6)]++;

            foreach (var count in counts)
                Assert.That(count, Is.EqualTo(10000).Within(500));
        }

        [Test]
        public void NextIntHandlesNegativeRanges()
        {
            var rng = new Rng(5);
            for (var i = 0; i < 1000; i++)
                Assert.That(rng.NextInt(-3, 2), Is.InRange(-3, 1));
        }

        [Test]
        public void EmptyRangeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Rng(1).NextInt(5, 5));
        }

        [Test]
        public void ChanceMatchesProbability()
        {
            var rng = new Rng(2026);
            var hits = Enumerable.Range(0, 100000).Count(_ => rng.Chance(0.25));

            Assert.That(hits, Is.EqualTo(25000).Within(700));
        }
    }
}
