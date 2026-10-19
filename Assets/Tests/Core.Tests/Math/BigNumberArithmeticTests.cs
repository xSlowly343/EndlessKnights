using System;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Math
{
    public class BigNumberArithmeticTests
    {
        private const double RelativeTolerance = 1e-12;

        private static readonly double[] SmallValues =
        {
            -98765.4321d, -12d, -1d, -0.25d, 0d, 0.001d, 0.5d, 1d, 3d, 7.77d, 10d, 999d, 1234.5678d, 1e9d
        };

        private static void AssertClose(BigNumber actual, double expected)
        {
            Assert.That(actual.ToDouble(), Is.EqualTo(expected).Within(System.Math.Abs(expected) * RelativeTolerance + 1e-12));
        }

        [Test]
        public void Addition_MatchesDouble()
        {
            foreach (var a in SmallValues)
                foreach (var b in SmallValues)
                    AssertClose(BigNumber.FromDouble(a) + BigNumber.FromDouble(b), a + b);
        }

        [Test]
        public void Subtraction_MatchesDouble()
        {
            foreach (var a in SmallValues)
                foreach (var b in SmallValues)
                    AssertClose(BigNumber.FromDouble(a) - BigNumber.FromDouble(b), a - b);
        }

        [Test]
        public void Multiplication_MatchesDouble()
        {
            foreach (var a in SmallValues)
                foreach (var b in SmallValues)
                    AssertClose(BigNumber.FromDouble(a) * BigNumber.FromDouble(b), a * b);
        }

        [Test]
        public void Division_MatchesDouble()
        {
            foreach (var a in SmallValues)
                foreach (var b in SmallValues)
                    if (b != 0d)
                        AssertClose(BigNumber.FromDouble(a) / BigNumber.FromDouble(b), a / b);
        }

        [Test]
        public void Subtraction_OfEqualValuesIsExactZero()
        {
            BigNumber value = 123.456d;

            Assert.That((value - value).IsZero, Is.True);
        }

        [Test]
        public void Addition_IgnoresNegligiblySmallTerm()
        {
            var huge = BigNumber.Create(1d, 100L);

            Assert.That(huge + BigNumber.One, Is.EqualTo(huge));
            Assert.That(BigNumber.One + huge, Is.EqualTo(huge));
        }

        [Test]
        public void ArithmeticBeyondDoubleRange()
        {
            var a = BigNumber.Create(2d, 400L);
            var b = BigNumber.Create(3d, 400L);

            Assert.That(a + b, Is.EqualTo(BigNumber.Create(5d, 400L)));
            Assert.That(a * b, Is.EqualTo(BigNumber.Create(6d, 800L)));
            Assert.That((b / a).ToDouble(), Is.EqualTo(1.5d).Within(RelativeTolerance));
        }

        [Test]
        public void Division_ByZeroThrows()
        {
            Assert.Throws<DivideByZeroException>(() => { var _ = BigNumber.One / BigNumber.Zero; });
        }

        [Test]
        public void Negation_FlipsSign()
        {
            BigNumber value = 42d;

            AssertClose(-value, -42d);
            Assert.That(-(-value), Is.EqualTo(value));
        }

        [TestCase(2d, 10d)]
        [TestCase(10d, 3d)]
        [TestCase(1.07d, 100d)]
        [TestCase(1000d, 0.5d)]
        [TestCase(7d, -2d)]
        [TestCase(0.5d, 3d)]
        [TestCase(123.4d, 1.5d)]
        public void Pow_MatchesDouble(double value, double power)
        {
            AssertClose(BigNumber.FromDouble(value).Pow(power), System.Math.Pow(value, power));
        }

        [Test]
        public void Pow_GrowsBeyondDoubleRange()
        {
            var result = BigNumber.FromDouble(1.07d).Pow(20000d);

            Assert.That(result.Log10(), Is.EqualTo(20000d * System.Math.Log10(1.07d)).Within(1e-6));
        }

        [Test]
        public void Pow_OfHugeNumberKeepsPrecision()
        {
            var result = BigNumber.Create(1d, 1000L).Pow(0.5d);

            Assert.That(result, Is.EqualTo(BigNumber.Create(1d, 500L)));
        }

        [Test]
        public void Pow_EdgeCases()
        {
            Assert.That(BigNumber.FromDouble(5d).Pow(0d), Is.EqualTo(BigNumber.One));
            Assert.That(BigNumber.Zero.Pow(0d), Is.EqualTo(BigNumber.One));
            Assert.That(BigNumber.Zero.Pow(2d), Is.EqualTo(BigNumber.Zero));
            Assert.Throws<DivideByZeroException>(() => BigNumber.Zero.Pow(-1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => BigNumber.FromDouble(-2d).Pow(2d));
            Assert.Throws<ArgumentOutOfRangeException>(() => BigNumber.One.Pow(double.NaN));
        }

        [TestCase(1d)]
        [TestCase(0.001d)]
        [TestCase(42d)]
        [TestCase(6.02e23d)]
        public void Log10_MatchesDouble(double value)
        {
            Assert.That(BigNumber.FromDouble(value).Log10(), Is.EqualTo(System.Math.Log10(value)).Within(1e-12));
        }

        [Test]
        public void Log10_BeyondDoubleRange()
        {
            Assert.That(BigNumber.Create(3d, 5000L).Log10(), Is.EqualTo(5000d + System.Math.Log10(3d)).Within(1e-9));
        }

        [Test]
        public void Log10_OfZeroAndNegative()
        {
            Assert.That(BigNumber.Zero.Log10(), Is.EqualTo(double.NegativeInfinity));
            Assert.That(double.IsNaN(BigNumber.FromDouble(-1d).Log10()), Is.True);
        }
    }
}
