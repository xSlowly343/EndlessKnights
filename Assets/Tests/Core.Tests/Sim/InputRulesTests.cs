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
                    total += effective.Hits + effective.MousePx;
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
        public void TenHitsPerSecondCountInFull()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: 1));

            Assert.That(perSecond, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void TwentyHitsPerSecondCountAsAboutThirteen()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: 2));

            Assert.That(perSecond, Is.EqualTo(System.Math.Sqrt(10 * 16)).Within(1e-6));
        }

        [Test]
        public void TwelveHitsPerSecondAreDiminishedBySquareRoot()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: tick % 5 < 1 ? 2 : 1));

            Assert.That(perSecond, Is.EqualTo(System.Math.Sqrt(10 * 12)).Within(1e-6));
        }

        [Test]
        public void KeysAndClicksShareOneCap()
        {
            var keysOnly = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: 2));
            var mixed = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(keyDowns: 1, leftClicks: 1));
            var clicksOnly = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(leftClicks: 2));

            Assert.That(mixed, Is.EqualTo(keysOnly).Within(1e-9));
            Assert.That(clicksOnly, Is.EqualTo(keysOnly).Within(1e-9));
        }

        [Test]
        public void MouseIsCutAtMaxSpeed()
        {
            var caps = RealCaps();
            var perSecond = EffectivePerSecond(new InputRules(caps), 10, tick => new InputSnapshot(mouseDistancePx: (float)caps.MaxMousePxPerSecond));

            Assert.That(perSecond, Is.EqualTo(caps.MaxMousePxPerSecond).Within(1e-3));
        }

        [Test]
        public void AllButtonsShareTheHitCapAndTheWheelIsNoHit()
        {
            var perSecond = EffectivePerSecond(new InputRules(RealCaps()), 10, tick => new InputSnapshot(leftClicks: 1, rightClicks: 1, middleClicks: 1, wheelTicks: 5));

            Assert.That(perSecond, Is.EqualTo(System.Math.Sqrt(10 * 16)).Within(1e-6));
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
        public void GesturesStayOutOfCombat()
        {
            var rules = new InputRules(RealCaps());

            var effective = rules.Apply(new InputSnapshot(keyDowns: 1, enterDowns: 1, rightClicks: 2, middleClicks: 1, wheelTicks: 3, flicks: 1, circleTurns: 0.5f, rhythmCv: 0.4f, comboDowns: 1, reversals: 2));

            Assert.That(effective.Hits, Is.EqualTo(4));
            Assert.That(effective.RightClicks, Is.EqualTo(2));
            Assert.That(effective.MiddleClicks, Is.EqualTo(1));
            Assert.That(effective.MousePx, Is.EqualTo(0));
            Assert.That(effective.RhythmCv, Is.EqualTo(0.4).Within(1e-6));
        }
    }
}
