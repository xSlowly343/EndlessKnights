using System.Collections.Generic;
using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class FocusModeTests
    {
        private FocusMode _focusMode = null!;
        private List<bool> _changes = null!;

        [SetUp]
        public void SetUp()
        {
            _focusMode = new FocusMode();
            _changes = new List<bool>();
            _focusMode.Changed += _changes.Add;
        }

        [Test]
        public void StartsInNormalMode()
        {
            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.Normal));
            Assert.That(_focusMode.IsNoFocus, Is.False);
        }

        [Test]
        public void ToggleTurnsNoFocusOnAndNotifiesOnce()
        {
            _focusMode.Toggle();

            Assert.That(_focusMode.IsNoFocus, Is.True);
            Assert.That(_changes, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void StaysPendingWhileGameIsStillForeground()
        {
            _focusMode.Toggle();
            _focusMode.Observe(gameIsForeground: true);

            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.NoFocusPending));
        }

        [Test]
        public void ArmsAfterGameLosesForeground()
        {
            _focusMode.Toggle();
            _focusMode.Observe(gameIsForeground: false);

            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.NoFocus));
            Assert.That(_changes, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void SelectingGameAfterArmingReturnsToNormal()
        {
            _focusMode.Toggle();
            _focusMode.Observe(gameIsForeground: false);
            _focusMode.Observe(gameIsForeground: true);

            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.Normal));
            Assert.That(_changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ToggleAgainReturnsToNormal()
        {
            _focusMode.Toggle();
            _focusMode.Toggle();

            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.Normal));
            Assert.That(_changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ObservingInNormalModeChangesNothing()
        {
            _focusMode.Observe(gameIsForeground: false);
            _focusMode.Observe(gameIsForeground: true);

            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.Normal));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void SetFollowsTheWantedModeAndNotifiesOnlyOnChange()
        {
            _focusMode.Set(true);
            _focusMode.Set(true);
            _focusMode.Observe(gameIsForeground: false);
            _focusMode.Set(false);

            Assert.That(_focusMode.State, Is.EqualTo(FocusModeState.Normal));
            Assert.That(_changes, Is.EqualTo(new[] { true, false }));
        }
    }
}
