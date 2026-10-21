using System;
using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Math
{
    public class NumberFormatterTests
    {
        private static string Letters(BigNumber value) => NumberFormatter.Format(value, NumberFormatSettings.Default);

        private static string With(NumberFormat format, BigNumber value, int decimals = 2, string thousands = " ", string point = ".") =>
            NumberFormatter.Format(value, new NumberFormatSettings(format, decimals, thousands, point));

        [TestCase(0L, "")]
        [TestCase(1L, "K")]
        [TestCase(2L, "M")]
        [TestCase(3L, "B")]
        [TestCase(4L, "T")]
        [TestCase(5L, "aa")]
        [TestCase(6L, "ab")]
        [TestCase(30L, "az")]
        [TestCase(31L, "ba")]
        [TestCase(680L, "zz")]
        [TestCase(681L, "aaa")]
        [TestCase(682L, "aab")]
        [TestCase(18256L, "zzz")]
        [TestCase(18257L, "aaaa")]
        public void LetterSuffixTable(long group, string suffix)
        {
            Assert.That(NumberFormatter.LetterSuffix(group), Is.EqualTo(suffix));
        }

        [Test]
        public void NegativeGroupIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NumberFormatter.LetterSuffix(-1));
        }

        [TestCase(0d, "0")]
        [TestCase(7d, "7")]
        [TestCase(999d, "999")]
        [TestCase(12.345d, "12.34")]
        [TestCase(1000d, "1K")]
        [TestCase(1234d, "1.23K")]
        [TestCase(45600000d, "45.6M")]
        [TestCase(7890000000d, "7.89B")]
        [TestCase(1.2e12d, "1.2T")]
        [TestCase(3.4e15d, "3.4aa")]
        [TestCase(5.6e18d, "5.6ab")]
        [TestCase(999999d, "999.99K")]
        [TestCase(-1234d, "-1.23K")]
        public void LettersFormat(double value, string expected)
        {
            Assert.That(Letters(value), Is.EqualTo(expected));
        }

        [Test]
        public void LettersBeyondDoubleRange()
        {
            Assert.That(Letters(BigNumber.Create(1.23d, 45L)), Is.EqualTo("1.23ak"));
            Assert.That(Letters(BigNumber.Create(4.5d, 2043L)), Is.EqualTo("4.5aaa"));
            Assert.That(Letters(BigNumber.Create(1d, 5000L)), Is.EqualTo("100" + NumberFormatter.LetterSuffix(1666)));
        }

        [TestCase(1234d, 0, "1K")]
        [TestCase(1234d, 1, "1.2K")]
        [TestCase(1234d, 3, "1.234K")]
        public void DecimalsAreRespected(double value, int decimals, string expected)
        {
            Assert.That(With(NumberFormat.Letters, value, decimals), Is.EqualTo(expected));
        }

        [Test]
        public void DecimalsOutsideRangeAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumberFormatSettings(NumberFormat.Letters, 4, " ", "."));
        }

        [TestCase(123d, "123")]
        [TestCase(1234d, "1.23e3")]
        [TestCase(6.02e23d, "6.02e23")]
        public void ScientificFormat(double value, string expected)
        {
            Assert.That(With(NumberFormat.Scientific, value), Is.EqualTo(expected));
        }

        [Test]
        public void ScientificBeyondDoubleRange()
        {
            Assert.That(With(NumberFormat.Scientific, BigNumber.Create(1.23d, 45L)), Is.EqualTo("1.23e45"));
        }

        [TestCase(123d, "123")]
        [TestCase(1234d, "1.23e3")]
        [TestCase(123400d, "123.4e3")]
        public void EngineeringFormat(double value, string expected)
        {
            Assert.That(With(NumberFormat.Engineering, value), Is.EqualTo(expected));
        }

        [Test]
        public void EngineeringUsesMultiplesOfThree()
        {
            Assert.That(With(NumberFormat.Engineering, BigNumber.Create(1.234d, 44L), 1), Is.EqualTo("123.4e42"));
        }

        [TestCase(0d, "0")]
        [TestCase(0.5d, "0")]
        [TestCase(1234.9d, "1 234")]
        [TestCase(999d, "999")]
        [TestCase(1234567d, "1 234 567")]
        [TestCase(1000d, "1 000")]
        [TestCase(999999999999999d, "999 999 999 999 999")]
        public void FullFormat(double value, string expected)
        {
            Assert.That(With(NumberFormat.Full, value), Is.EqualTo(expected));
        }

        [Test]
        public void FullSwitchesToLettersFromQuadrillion()
        {
            Assert.That(With(NumberFormat.Full, 1.5e15d), Is.EqualTo("1.5aa"));
        }

        [Test]
        public void SeparatorsFollowSettings()
        {
            Assert.That(With(NumberFormat.Full, 1234567d, thousands: ","), Is.EqualTo("1,234,567"));
            Assert.That(With(NumberFormat.Letters, 1234d, point: ","), Is.EqualTo("1,23K"));
        }

        [Test]
        public void RoundingNeverShowsAThousandInAGroup()
        {
            Assert.That(Letters(999999.999d), Is.EqualTo("999.99K"));
        }
    }
}
