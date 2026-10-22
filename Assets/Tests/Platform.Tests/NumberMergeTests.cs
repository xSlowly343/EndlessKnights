using Game.Presentation.Battle;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class NumberMergeTests
    {
        [Test]
        public void FirstHitStartsANewNumber()
        {
            Assert.That(new NumberMerge().ShouldMerge(0f, crit: false), Is.False);
        }

        [Test]
        public void FastHitsMergeIntoOne()
        {
            var merge = new NumberMerge();
            merge.ShouldMerge(0f, false);

            Assert.That(merge.ShouldMerge(0.125f, false), Is.True);
            Assert.That(merge.ShouldMerge(0.25f, false), Is.True);
        }

        [Test]
        public void EightPressesPerSecondMakeOneGrowingNumber()
        {
            var merge = new NumberMerge();
            var separate = 0;
            for (var i = 0; i < 8; i++)
                if (!merge.ShouldMerge(i * 0.125f, false))
                    separate++;

            Assert.That(separate, Is.EqualTo(1));
        }

        [Test]
        public void PauseLongerThanTheWindowStartsANewNumber()
        {
            var merge = new NumberMerge();
            merge.ShouldMerge(0f, false);

            Assert.That(merge.ShouldMerge(0.5f, false), Is.False);
        }

        [Test]
        public void CritsAlwaysStandAlone()
        {
            var merge = new NumberMerge();
            merge.ShouldMerge(0f, false);

            Assert.That(merge.ShouldMerge(0.1f, crit: true), Is.False);
            Assert.That(merge.ShouldMerge(0.2f, false), Is.True);
        }

        [Test]
        public void BreakEndsTheCurrentNumber()
        {
            var merge = new NumberMerge();
            merge.ShouldMerge(0f, false);
            merge.Break();

            Assert.That(merge.ShouldMerge(0.1f, false), Is.False);
        }
    }
}
