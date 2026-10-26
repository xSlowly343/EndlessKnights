using Game.Core.Clock;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Input
{
    public class InputStatsTests
    {
        [Test]
        public void HundredKeysCountInSessionAndTotal()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            for (var i = 0; i < 100; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }

            Assert.That(loop.SessionInput.Keys, Is.EqualTo(100));
            Assert.That(loop.State.TotalInput.Keys, Is.EqualTo(100));
        }

        [Test]
        public void StatsCountRawInputBeforeCombatCaps()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(4);
                loop.Tick();
            }

            Assert.That(loop.SessionInput.Keys, Is.EqualTo(200));
        }

        [Test]
        public void SessionStartsFromZeroAfterRestartButTotalIsSaved()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            input.PressKeys(30);
            input.ClickLeft(2);
            input.ClickRight(3);
            input.ClickMiddle(1);
            input.MoveMouse(96 * 100);
            loop.Tick();
            loop.MarkStarted(1_793_000_000_000);
            loop.MarkStarted(1_800_000_000_000);

            var text = SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1)));
            var restarted = TestWorld.Loop(state: SaveCodec.Decode(text, TestWorld.Data).State);

            Assert.That(restarted.SessionInput.Keys, Is.EqualTo(0));
            var total = restarted.State.TotalInput;
            Assert.That(total.Keys, Is.EqualTo(30));
            Assert.That(total.Clicks, Is.EqualTo(6));
            Assert.That(total.RightClicks, Is.EqualTo(3));
            Assert.That(total.MouseMeters, Is.EqualTo(2.54).Within(1e-9));
            Assert.That(total.PlaySeconds, Is.EqualTo(GameLoop.TickSeconds).Within(1e-9));
            Assert.That(restarted.SessionInput.PlaySeconds, Is.EqualTo(0));
            Assert.That(restarted.State.StartedUnixMs, Is.EqualTo(1_793_000_000_000));
        }

        [Test]
        public void SuppressedInputIsNotCounted()
        {
            var stats = new InputStats();

            stats.Add(new InputSnapshot(keyDowns: 5, leftClicks: 1, suppressed: true), InputStats.DefaultMetersPerPixel);

            Assert.That(stats.Keys, Is.EqualTo(0));
            Assert.That(stats.Clicks, Is.EqualTo(0));
        }

        [Test]
        public void MetersUseTheMonitorScale()
        {
            var stats = new InputStats();

            stats.Add(new InputSnapshot(mouseDistancePx: 1000), 0.0003);

            Assert.That(stats.MouseMeters, Is.EqualTo(0.3).Within(1e-12));
        }

        [TestCase(0, "0 м")]
        [TestCase(999.999, "999 м")]
        [TestCase(1000, "1.00 км")]
        [TestCase(2054, "2.05 км")]
        [TestCase(1204370, "1 204.37 км")]
        public void DistanceFormat(double meters, string expected)
        {
            Assert.That(StatFormat.Distance(meters), Is.EqualTo(expected));
        }

        [Test]
        public void DistanceInEnglish()
        {
            Assert.That(StatFormat.Distance(734, russian: false), Is.EqualTo("734 m"));
            Assert.That(StatFormat.Distance(2054, russian: false, thousandsSeparator: ","), Is.EqualTo("2.05 km"));
        }

        [TestCase(0UL, "0")]
        [TestCase(999UL, "999")]
        [TestCase(48210UL, "48 210")]
        [TestCase(1234567UL, "1 234 567")]
        public void CountFormat(ulong value, string expected)
        {
            Assert.That(StatFormat.Count(value), Is.EqualTo(expected));
        }
    }
}
