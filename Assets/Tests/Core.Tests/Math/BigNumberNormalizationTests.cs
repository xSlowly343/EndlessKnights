using System;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Math
{
    public class BigNumberNormalizationTests
    {
        private const double Tolerance = 1e-12;

        [TestCase(1d, 1d, 0L)]
        [TestCase(10d, 1d, 1L)]
        [TestCase(9.99d, 9.99d, 0L)]
        [TestCase(1234.5d, 1.2345d, 3L)]
        [TestCase(0.05d, 5d, -2L)]
        [TestCase(1e300d, 1d, 300L)]
        [TestCase(1e-300d, 1d, -300L)]
        [TestCase(-2500d, -2.5d, 3L)]
        public void FromDouble_NormalizesMantissaIntoOneToTen(double value, double mantissa, long exponent)
        {
            var number = BigNumber.FromDouble(value);

            Assert.That(number.Mantissa, Is.EqualTo(mantissa).Within(Tolerance));
            Assert.That(number.Exponent, Is.EqualTo(exponent));
        }

        [TestCase(123d, 5L, 1.23d, 7L)]
        [TestCase(0.5d, 100L, 5d, 99L)]
        [TestCase(10d, -1L, 1d, 0L)]
        [TestCase(-0.001d, 10L, -1d, 7L)]
        public void Create_ShiftsExponentWhenMantissaIsOutOfRange(double mantissa, long exponent, double expectedMantissa, long expectedExponent)
        {
            var number = BigNumber.Create(mantissa, exponent);

            Assert.That(number.Mantissa, Is.EqualTo(expectedMantissa).Within(Tolerance));
            Assert.That(number.Exponent, Is.EqualTo(expectedExponent));
        }

        [Test]
        public void Create_KeepsAbsoluteMantissaInRangeForManyValues()
        {
            for (var power = -50; power <= 50; power++)
            {
                foreach (var digit in new[] { 1d, 2.5d, 9.999999d })
                {
                    var number = BigNumber.FromDouble(digit * System.Math.Pow(10d, power));
                    Assert.That(System.Math.Abs(number.Mantissa), Is.GreaterThanOrEqualTo(1d).And.LessThan(10d));
                }
            }
        }

        [Test]
        public void Create_SupportsExponentsBeyondDoubleRange()
        {
            var number = BigNumber.Create(4.2d, 5000L);

            Assert.That(number.Mantissa, Is.EqualTo(4.2d));
            Assert.That(number.Exponent, Is.EqualTo(5000L));
        }

        [Test]
        public void Create_ZeroMantissaIsZeroWithZeroExponent()
        {
            var number = BigNumber.Create(0d, 42L);

            Assert.That(number.IsZero, Is.True);
            Assert.That(number.Exponent, Is.EqualTo(0L));
            Assert.That(number, Is.EqualTo(BigNumber.Zero));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Create_RejectsNonFiniteMantissa(double mantissa)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BigNumber.Create(mantissa, 0L));
        }

        [Test]
        public void Constants_HaveExpectedValues()
        {
            Assert.That(BigNumber.Zero.IsZero, Is.True);
            Assert.That(BigNumber.One.Mantissa, Is.EqualTo(1d));
            Assert.That(BigNumber.One.Exponent, Is.EqualTo(0L));
        }

        [Test]
        public void Default_IsZero()
        {
            Assert.That(default(BigNumber).IsZero, Is.True);
            Assert.That(default(BigNumber), Is.EqualTo(BigNumber.Zero));
        }

        [TestCase(0d, 0)]
        [TestCase(5d, 1)]
        [TestCase(-5d, -1)]
        public void Sign_MatchesValue(double value, int sign)
        {
            Assert.That(BigNumber.FromDouble(value).Sign, Is.EqualTo(sign));
        }

        [TestCase(0d)]
        [TestCase(1d)]
        [TestCase(123456.789d)]
        [TestCase(-0.000321d)]
        [TestCase(6.02e23d)]
        public void ToDouble_RoundTrips(double value)
        {
            var roundTripped = BigNumber.FromDouble(value).ToDouble();

            Assert.That(roundTripped, Is.EqualTo(value).Within(System.Math.Abs(value) * Tolerance));
        }

        [Test]
        public void ToDouble_OverflowsToInfinityBeyondDoubleRange()
        {
            Assert.That(BigNumber.Create(1d, 400L).ToDouble(), Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void ImplicitConversion_FromDouble()
        {
            BigNumber number = 2000d;

            Assert.That(number, Is.EqualTo(BigNumber.Create(2d, 3L)));
        }

        [Test]
        public void ToString_IsInvariant()
        {
            Assert.That(BigNumber.Create(1.5d, 45L).ToString(), Is.EqualTo("1.5e45"));
        }
    }
}
