using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Math
{
    public class BigNumberComparisonTests
    {
        private static readonly double[] OrderedValues =
        {
            -1e300d, -5e10d, -123d, -1d, -0.5d, 0d, 1e-20d, 0.5d, 1d, 9.99d, 10d, 123d, 5e10d, 1e300d
        };

        [Test]
        public void CompareTo_MatchesDoubleOrderingForAllPairs()
        {
            foreach (var left in OrderedValues)
            {
                foreach (var right in OrderedValues)
                {
                    var expected = left.CompareTo(right);
                    var actual = BigNumber.FromDouble(left).CompareTo(BigNumber.FromDouble(right));
                    Assert.That(System.Math.Sign(actual), Is.EqualTo(expected), $"{left} vs {right}");
                }
            }
        }

        [Test]
        public void CompareTo_HandlesExponentsBeyondDoubleRange()
        {
            var huge = BigNumber.Create(1d, 1000L);
            var bigger = BigNumber.Create(1d, 1001L);

            Assert.That(huge < bigger, Is.True);
            Assert.That(-huge > -bigger, Is.True);
            Assert.That(BigNumber.Create(2d, 1000L) > huge, Is.True);
        }

        [Test]
        public void Operators_AgreeWithCompareTo()
        {
            BigNumber small = 3d;
            BigNumber large = 30d;
            var sameAsSmall = small;

            Assert.That(small < large, Is.True);
            Assert.That(small <= large, Is.True);
            Assert.That(large > small, Is.True);
            Assert.That(large >= small, Is.True);
            Assert.That(small <= sameAsSmall, Is.True);
            Assert.That(small >= sameAsSmall, Is.True);
            Assert.That(small != large, Is.True);
        }

        [Test]
        public void Equality_DependsOnValueNotConstruction()
        {
            var fromDouble = BigNumber.FromDouble(1500d);
            var fromParts = BigNumber.Create(15d, 2L);

            Assert.That(fromDouble == fromParts, Is.True);
            Assert.That(fromDouble.Equals((object)fromParts), Is.True);
            Assert.That(fromDouble.GetHashCode(), Is.EqualTo(fromParts.GetHashCode()));
        }

        [Test]
        public void Equals_RejectsOtherTypes()
        {
            Assert.That(BigNumber.One.Equals("1"), Is.False);
        }

        [Test]
        public void MaxAndMin_PickByValue()
        {
            BigNumber a = -7d;
            BigNumber b = 2d;

            Assert.That(BigNumber.Max(a, b), Is.EqualTo(b));
            Assert.That(BigNumber.Min(a, b), Is.EqualTo(a));
        }
    }
}
