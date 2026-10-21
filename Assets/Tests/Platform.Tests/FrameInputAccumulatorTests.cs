using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class FrameInputAccumulatorTests
    {
        [Test]
        public void CollectsAndResetsLikeTheRawSource()
        {
            var accumulator = new FrameInputAccumulator();
            accumulator.AddKey(KeyClass.Regular, 1.0);
            accumulator.AddKey(KeyClass.Enter, 1.1);
            accumulator.AddKey(KeyClass.None, 1.2);
            accumulator.AddClicks(1, 0, 1, 1.3);
            accumulator.AddWheelTicks(2, 1.4);
            accumulator.AddMouseDistance(15.5f, 1.5);

            var snapshot = accumulator.Drain(4.0);

            Assert.That(snapshot.KeyDowns, Is.EqualTo(2));
            Assert.That(snapshot.EnterDowns, Is.EqualTo(1));
            Assert.That(snapshot.LeftClicks, Is.EqualTo(1));
            Assert.That(snapshot.MiddleClicks, Is.EqualTo(1));
            Assert.That(snapshot.WheelTicks, Is.EqualTo(2));
            Assert.That(snapshot.MouseDistancePx, Is.EqualTo(15.5f));
            Assert.That(snapshot.SecondsSinceLastInput, Is.EqualTo(2.5).Within(1e-9));

            var second = accumulator.Drain(5.0);
            Assert.That(second.HasAnyInput, Is.False);
            Assert.That(second.SecondsSinceLastInput, Is.EqualTo(3.5).Within(1e-9));
        }

        [Test]
        public void ModifierKeysAndZeroValuesDoNotCountAsInput()
        {
            var accumulator = new FrameInputAccumulator();
            accumulator.AddKey(KeyClass.None, 1.0);
            accumulator.AddClicks(0, 0, 0, 1.0);
            accumulator.AddWheelTicks(0, 1.0);
            accumulator.AddMouseDistance(0f, 1.0);

            var snapshot = accumulator.Drain(2.0);

            Assert.That(snapshot.HasAnyInput, Is.False);
            Assert.That(snapshot.SecondsSinceLastInput, Is.EqualTo(0));
        }
    }
}
