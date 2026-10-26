using Game.Core.Input;
using Game.Core.Settings;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Settings
{
    public class InputPermissionsTests
    {
        private static readonly InputSnapshot Busy = new InputSnapshot(
            keyDowns: 5, enterDowns: 1, backspaceDowns: 1, spaceDowns: 1,
            leftClicks: 2, rightClicks: 1, middleClicks: 1, wheelTicks: 3,
            mouseDistancePx: 400f, flicks: 1, circleTurns: 0.5f,
            secondsSinceLastInput: 0.1, rhythmCv: 0.3f);

        [Test]
        public void NewSettingsHaveNoConsent()
        {
            var permissions = InputPermissions.From(new GameSettings());

            Assert.That(permissions.Consent, Is.False);
            Assert.That(permissions.CountsAnything, Is.False);
            Assert.That(permissions.Apply(Busy).HasAnyInput, Is.False);
        }

        [Test]
        public void WithoutConsentTheLoopCountsNothing()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Permissions = InputPermissions.From(new GameSettings());
            var hp = loop.State.EnemyHp;

            for (var i = 0; i < 20; i++)
            {
                input.PressKeys(3);
                input.ClickLeft();
                input.MoveMouse(500);
                loop.Tick();
            }

            Assert.That(loop.SessionInput.Keys, Is.EqualTo(0UL));
            Assert.That(loop.State.TotalInput.Keys, Is.EqualTo(0UL));
            Assert.That(loop.State.EnemyHp, Is.EqualTo(hp));
        }

        [Test]
        public void ConsentLetsEverythingThrough()
        {
            var filtered = InputPermissions.From(new GameSettings { InputConsentGiven = true }).Apply(Busy);

            Assert.That(filtered.KeyDowns, Is.EqualTo(5));
            Assert.That(filtered.LeftClicks, Is.EqualTo(2));
            Assert.That(filtered.WheelTicks, Is.EqualTo(3));
            Assert.That(filtered.MouseDistancePx, Is.EqualTo(400f));
            Assert.That(filtered.RhythmCv, Is.EqualTo(0.3f));
        }

        [Test]
        public void EachInputKindCanBeTurnedOff()
        {
            var settings = new GameSettings { InputConsentGiven = true, CountKeys = false, CountMouseMovement = false };

            var filtered = InputPermissions.From(settings).Apply(Busy);

            Assert.That(filtered.KeyDowns + filtered.EnterDowns + filtered.BackspaceDowns + filtered.SpaceDowns, Is.EqualTo(0));
            Assert.That(filtered.MouseDistancePx, Is.EqualTo(0f));
            Assert.That(filtered.Flicks, Is.EqualTo(0));
            Assert.That(filtered.LeftClicks, Is.EqualTo(2));
            Assert.That(filtered.WheelTicks, Is.EqualTo(3));
        }

        [Test]
        public void ClicksAndWheelCanBeTurnedOff()
        {
            var settings = new GameSettings { InputConsentGiven = true, CountClicks = false, CountWheel = false };

            var filtered = InputPermissions.From(settings).Apply(Busy);

            Assert.That(filtered.LeftClicks + filtered.RightClicks + filtered.MiddleClicks, Is.EqualTo(0));
            Assert.That(filtered.WheelTicks, Is.EqualTo(0));
            Assert.That(filtered.KeyDowns, Is.EqualTo(5));
        }
    }
}
