using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GameLoopTests
    {
        [Test]
        public void TenTicksMakeOneSecond()
        {
            var loop = TestWorld.Loop();

            var ticks = loop.Advance(1.0);

            Assert.That(ticks, Is.EqualTo(10));
            Assert.That(loop.State.Tick, Is.EqualTo(10));
            Assert.That(loop.State.ElapsedSeconds, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void TickRateDoesNotDependOnFrameRate()
        {
            var at60 = TestWorld.Loop();
            var at144 = TestWorld.Loop();

            for (var frame = 0; frame < 60 * 30; frame++)
                at60.Advance(1.0 / 60);
            for (var frame = 0; frame < 144 * 30; frame++)
                at144.Advance(1.0 / 144);

            Assert.That(at60.State.Tick, Is.EqualTo(300));
            Assert.That(at144.State.Tick, Is.EqualTo(300));
        }

        [Test]
        public void SlowFramesRunSeveralTicksAtOnce()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Advance(0.05), Is.EqualTo(0));
            Assert.That(loop.Advance(0.3), Is.EqualTo(3));
            Assert.That(loop.Advance(0.05), Is.EqualTo(1));
        }

        [Test]
        public void LongHitchIsCappedInsteadOfFreezingTheGame()
        {
            var loop = TestWorld.Loop();

            var ticks = loop.Advance(3600);

            Assert.That(ticks, Is.EqualTo(GameLoop.MaxCatchUpTicks));
            Assert.That(loop.Advance(0.1), Is.EqualTo(1));
        }

        [Test]
        public void PausedLoopDoesNotTick()
        {
            var loop = TestWorld.Loop();
            loop.Paused = true;

            Assert.That(loop.Advance(5), Is.EqualTo(0));
            Assert.That(loop.State.Tick, Is.EqualTo(0));

            loop.Paused = false;
            Assert.That(loop.Advance(0.1), Is.EqualTo(1));
        }

        [Test]
        public void EachTickDrainsInputOnce()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            input.PressKeys(3);
            loop.Tick();
            Assert.That(loop.LastInput.KeyDowns, Is.EqualTo(3));

            loop.Tick();
            Assert.That(loop.LastInput.KeyDowns, Is.EqualTo(0));
        }

        [Test]
        public void EventsPileUpUntilCleared()
        {
            var queue = new EventQueue(capacity: 2);

            for (var i = 0; i < 5; i++)
                queue.Add(GameEvent.Damage(i, BigNumber.FromDouble(10), DamageSource.Keyboard, HitFlags.None));

            Assert.That(queue.Count, Is.EqualTo(5));
            Assert.That(queue[4].Tick, Is.EqualTo(4));
            Assert.That(queue.CountOf(GameEventKind.Damage), Is.EqualTo(5));

            queue.Clear();
            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = queue[0]);
        }
    }
}
