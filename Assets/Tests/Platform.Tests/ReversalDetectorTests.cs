using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class ReversalDetectorTests
    {
        [Test]
        public void SwingLeftAndRightCountsEachTurn()
        {
            var detector = new ReversalDetector(60);
            var reversals = 0;
            for (var swing = 0; swing < 4; swing++)
            {
                var direction = swing % 2 == 0 ? 1 : -1;
                for (var step = 0; step < 10; step++)
                    reversals += detector.Add(10 * direction, 0);
            }

            Assert.That(reversals, Is.EqualTo(3));
        }

        [Test]
        public void SmallJitterIsNotAReversal()
        {
            var detector = new ReversalDetector(60);
            var reversals = 0;
            for (var step = 0; step < 100; step++)
                reversals += detector.Add(step % 2 == 0 ? 5 : -5, step % 2 == 0 ? -3 : 3);

            Assert.That(reversals, Is.EqualTo(0));
        }

        [Test]
        public void UpAndDownCountsToo()
        {
            var detector = new ReversalDetector(60);

            detector.Add(0, 100);
            var reversals = detector.Add(0, -100);

            Assert.That(reversals, Is.EqualTo(1));
        }
    }
}
