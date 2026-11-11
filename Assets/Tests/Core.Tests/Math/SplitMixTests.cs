using Game.Core.Math;
using NUnit.Framework;

namespace Game.Core.Tests.Math
{
    public class SplitMixTests
    {
        [TestCase(0UL, 1UL)]
        [TestCase(11UL, 5UL)]
        [TestCase(ulong.MaxValue, 123456UL)]
        [TestCase(0x9E3779B97F4A7C15UL, 4000000000UL)]
        public void TheValueAfterOneOfTheSequenceIsTheNextOne(ulong seed, ulong index)
        {
            Assert.That(SplitMix.After(SplitMix.At(seed, index)), Is.EqualTo(SplitMix.At(seed, index + 1)));
        }

        [Test]
        public void KnownValuesStayTheSame()
        {
            Assert.That(SplitMix.At(11, 5), Is.EqualTo(0x2A4A0F1A750459C4UL));
            Assert.That(SplitMix.After(0x2A4A0F1A750459C4UL), Is.EqualTo(0x8D4BC9E17AB0580EUL));
        }
    }
}
