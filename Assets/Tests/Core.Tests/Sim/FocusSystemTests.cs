using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class FocusSystemTests
    {
        private static Game.Core.Content.FocusRules Rules => TestWorld.Data.Combat.Focus;

        private static void Type(GameLoop loop, FakeInputSource input, int seconds, float rhythmCv = 0.4f)
        {
            input.RhythmCv = rhythmCv;
            for (var tick = 0; tick < seconds * GameLoop.TicksPerSecond; tick++)
            {
                if (tick % 2 == 1)
                    input.PressKeys(1);
                loop.Tick();
            }
        }

        private static void Idle(GameLoop loop, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
                loop.Tick();
        }

        [Test]
        public void FocusStartsAtMinimum()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.State.Focus, Is.EqualTo(Rules.Min));
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(Rules.Min));
        }

        [Test]
        public void EachInputEventAddsFocus()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            input.PressKeys(1);
            input.ClickLeft();
            input.ClickRight();
            input.MoveMouse((float)Rules.MousePxPerEvent);
            loop.Tick();

            Assert.That(loop.State.Focus, Is.EqualTo(Rules.Min + 4 * Rules.PerEvent).Within(1e-9));
        }

        [Test]
        public void ContinuousTypingGrowsFocusUpToMaximum()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            Type(loop, input, 10);
            Assert.That(loop.State.Focus, Is.EqualTo(Rules.Min + 50 * Rules.PerEvent).Within(1e-9));

            Type(loop, input, 600);
            Assert.That(loop.State.Focus, Is.EqualTo(Rules.Max));
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(Rules.Max));
        }

        [Test]
        public void ShortPauseKeepsFocus()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            Type(loop, input, 10);
            var focus = loop.State.Focus;

            Idle(loop, (int)(Rules.ResetAfterIdleSeconds * GameLoop.TicksPerSecond));

            Assert.That(loop.State.Focus, Is.EqualTo(focus));
        }

        [Test]
        public void PauseLongerThanThreeSecondsResetsFocus()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            Type(loop, input, 10);

            Idle(loop, (int)(Rules.ResetAfterIdleSeconds * GameLoop.TicksPerSecond) + 1);

            Assert.That(loop.State.Focus, Is.EqualTo(Rules.Min));
        }

        [Test]
        public void AResetRemembersTheDroppedFocusForTheRelay()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            Type(loop, input, 10);
            var before = loop.State.Focus;

            Idle(loop, (int)(Rules.ResetAfterIdleSeconds * GameLoop.TicksPerSecond) + 5);

            Assert.That(before, Is.GreaterThan(Rules.Min));
            Assert.That(loop.State.FocusDropped, Is.EqualTo(before - Rules.Min).Within(1e-9), "only the first reset tick counts, later ticks drop nothing");
        }

        [Test]
        public void ARelayedBonusAddsToTheMultiplierAndRunsOut()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            Type(loop, input, 10);
            var focus = loop.State.Focus;

            loop.Focus.Receive(0.6, 1, 2);
            loop.Focus.Receive(0.6, 1, 2);
            Type(loop, input, 1);

            Assert.That(loop.State.RelayFocus, Is.EqualTo(1), "two relays stop at the cap");
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(loop.State.Focus + 1).Within(1e-9));
            Assert.That(loop.Focus.RelaySecondsLeft, Is.EqualTo(1).Within(1e-9));
            Type(loop, input, 1);
            Assert.That(loop.State.RelayFocus, Is.Zero);
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(loop.State.Focus).Within(1e-9));
            Assert.That(focus, Is.GreaterThan(Rules.Min));
        }

        [Test]
        public void AMacroDropsNoFocusForTheRelay()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            Type(loop, input, (int)Rules.MacroAfterSeconds + 1, rhythmCv: 0.01f);

            Idle(loop, (int)(Rules.ResetAfterIdleSeconds * GameLoop.TicksPerSecond) + 1);

            Assert.That(loop.State.FocusDropped, Is.Zero);
        }

        [Test]
        public void MacroRhythmForAMinuteSwitchesFocusBonusOff()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            Type(loop, input, (int)Rules.MacroAfterSeconds - 1, rhythmCv: 0.01f);
            Assert.That(loop.Focus.MacroDetected, Is.False);
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(loop.State.Focus));

            Type(loop, input, 1, rhythmCv: 0.01f);
            Assert.That(loop.Focus.MacroDetected, Is.True);
            Assert.That(loop.State.Focus, Is.GreaterThan(Rules.Min));
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(Rules.Min));

            Type(loop, input, 1, rhythmCv: 0.4f);
            Assert.That(loop.Focus.MacroDetected, Is.False);
            Assert.That(loop.State.FocusMultiplier, Is.EqualTo(loop.State.Focus));
        }

        [Test]
        public void UnknownRhythmIsNotTreatedAsMacro()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            Type(loop, input, 120, rhythmCv: 0f);

            Assert.That(loop.Focus.MacroDetected, Is.False);
        }
    }
}
