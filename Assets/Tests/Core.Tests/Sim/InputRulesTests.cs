using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class InputRulesTests
    {
        private static InputCaps RealCaps() => InputCaps.Load(TestData.ReadGameDataFile(InputCaps.FileName));

        private static double EffectivePerSecond(InputRules rules, int seconds, System.Func<int, InputSnapshot> snapshotForTick)
        {
            var total = 0.0;
            for (var tick = 0; tick < seconds * GameLoop.TicksPerSecond; tick++)
            {
                var effective = rules.Apply(snapshotForTick(tick));
                if (tick >= GameLoop.TicksPerSecond)
                    total += effective.Keys + effective.Clicks + effective.MousePx;
            }
            return total / (seconds - 1);
        }

        [TestCase(4, 4)]
        [TestCase(8, 8)]
        [TestCase(12, 9.80)]
        [TestCase(15, 10.95)]
        [TestCase(30, 10.95)]
        public void RateCapFormula(double rate, double expected)
        {
            var cap = new RateCap(8, 15);

            Assert.That(cap.Effective(rate), Is.EqualTo(expected).Within(0.01));
        }

        [Test]
        public void RateCapNeverGivesMoreThanRaw()
        {
            var cap = new RateCap(8, 15);

            for (var rate = 0.0; rate <= 40; rate += 0.25)
                Assert.That(cap.Effective(rate), Is.LessThanOrEqualTo(rate + 1e-9), $"rate {rate}");
        }

        [Test]
        public void EightKeysPerSecondCountInFull()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: tick % 5 < 4 ? 1 : 0));

            Assert.That(perSecond, Is.EqualTo(8).Within(1e-6));
        }

        [Test]
        public void TwentyKeysPerSecondCountAsAboutEleven()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: 2));

            Assert.That(perSecond, Is.EqualTo(System.Math.Sqrt(8 * 15)).Within(1e-6));
            Assert.That(perSecond, Is.EqualTo(11).Within(0.1));
        }

        [Test]
        public void TwelveKeysPerSecondAreDiminishedBySquareRoot()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: tick % 5 < 1 ? 2 : 1));

            Assert.That(perSecond, Is.EqualTo(System.Math.Sqrt(8 * 12)).Within(1e-6));
        }

        [Test]
        public void ClicksHaveTheirOwnCap()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(leftClicks: 3));

            Assert.That(perSecond, Is.EqualTo(System.Math.Sqrt(10 * 16)).Within(1e-6));
        }

        [Test]
        public void MouseIsCutAtMaxSpeed()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(mouseDistancePx: 1000));

            Assert.That(perSecond, Is.EqualTo(3000).Within(1e-6));
        }

        [Test]
        public void SlowMouseIsNotCut()
        {
            var rules = new InputRules(RealCaps());

            var effective = rules.Apply(new InputSnapshot(mouseDistancePx: 150));

            Assert.That(effective.MousePx, Is.EqualTo(150));
        }

        [Test]
        public void SuppressedInputGivesNothing()
        {
            var rules = new InputRules(RealCaps());

            var effective = rules.Apply(new InputSnapshot(keyDowns: 5, leftClicks: 2, mouseDistancePx: 100, suppressed: true));

            Assert.That(effective.HasInput, Is.False);
        }

        [Test]
        public void EnterAndOtherCountersPassThrough()
        {
            var rules = new InputRules(RealCaps());

            var effective = rules.Apply(new InputSnapshot(keyDowns: 1, enterDowns: 1, rightClicks: 2, middleClicks: 1, wheelTicks: 3, flicks: 1, circleTurns: 0.5f, rhythmCv: 0.4f));

            Assert.That(effective.Keys, Is.EqualTo(1));
            Assert.That(effective.EnterDowns, Is.EqualTo(1));
            Assert.That(effective.RightClicks, Is.EqualTo(2));
            Assert.That(effective.MiddleClicks, Is.EqualTo(1));
            Assert.That(effective.WheelTicks, Is.EqualTo(3));
            Assert.That(effective.Flicks, Is.EqualTo(1));
            Assert.That(effective.CircleTurns, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(effective.RhythmCv, Is.EqualTo(0.4).Within(1e-6));
        }
    }
}
