using System.Linq;
using Game.Platform.Windows.Tray;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class TrayMenuTests
    {
        [Test]
        public void DefaultsAreUnpausedClickThroughOnTopAndSixtyFps()
        {
            var state = TrayMenuState.Default;

            Assert.That(state.Paused, Is.False);
            Assert.That(state.ClickThrough, Is.True);
            Assert.That(state.AlwaysOnTop, Is.True);
            Assert.That(state.FrameRate, Is.EqualTo(60));
            Assert.That(state.ShowInputDebug, Is.True);
        }

        [TestCase(TrayCommand.TogglePause)]
        [TestCase(TrayCommand.ToggleClickThrough)]
        [TestCase(TrayCommand.ToggleAlwaysOnTop)]
        [TestCase(TrayCommand.ToggleInputDebug)]
        public void EachToggleFlipsOnlyItsOwnOption(TrayCommand command)
        {
            var before = TrayMenuState.Default;

            var after = before.Apply(command);

            Assert.That(after.Paused, Is.EqualTo(command == TrayCommand.TogglePause ? !before.Paused : before.Paused));
            Assert.That(after.ClickThrough, Is.EqualTo(command == TrayCommand.ToggleClickThrough ? !before.ClickThrough : before.ClickThrough));
            Assert.That(after.AlwaysOnTop, Is.EqualTo(command == TrayCommand.ToggleAlwaysOnTop ? !before.AlwaysOnTop : before.AlwaysOnTop));
            Assert.That(after.ShowInputDebug, Is.EqualTo(command == TrayCommand.ToggleInputDebug ? !before.ShowInputDebug : before.ShowInputDebug));
            Assert.That(after.FrameRate, Is.EqualTo(before.FrameRate));
        }

        [TestCase(TrayCommand.FrameRate30, 30)]
        [TestCase(TrayCommand.FrameRate60, 60)]
        [TestCase(TrayCommand.FrameRate144, 144)]
        public void FrameRateCommandsSetTheLimit(TrayCommand command, int frameRate)
        {
            var after = TrayMenuState.Default.Apply(command);

            Assert.That(after.FrameRate, Is.EqualTo(frameRate));
            Assert.That(after.ClickThrough, Is.True);
            Assert.That(after.ShowInputDebug, Is.True);
        }

        [Test]
        public void ExitDoesNotChangeState()
        {
            var after = TrayMenuState.Default.Apply(TrayCommand.Exit);

            Assert.That(after.Paused, Is.False);
            Assert.That(after.ClickThrough, Is.True);
            Assert.That(after.AlwaysOnTop, Is.True);
            Assert.That(after.FrameRate, Is.EqualTo(60));
        }

        [Test]
        public void InputDebugToggleHidesAndShowsAgain()
        {
            var hidden = TrayMenuState.Default.Apply(TrayCommand.ToggleInputDebug);
            var shown = hidden.Apply(TrayCommand.ToggleInputDebug);

            Assert.That(hidden.ShowInputDebug, Is.False);
            Assert.That(shown.ShowInputDebug, Is.True);
        }

        [Test]
        public void MenuListsAllCommandsInOrderWithChecks()
        {
            var state = new TrayMenuState(paused: true, clickThrough: false, alwaysOnTop: true, frameRate: 144, showInputDebug: false);

            var items = TrayMenu.Items(state);
            var commands = items.Where(item => !item.IsSeparator).ToList();

            Assert.That(commands.Select(item => item.Command), Is.EqualTo(new[]
            {
                TrayCommand.TogglePause, TrayCommand.ToggleClickThrough, TrayCommand.ToggleAlwaysOnTop, TrayCommand.ToggleInputDebug,
                TrayCommand.FrameRate30, TrayCommand.FrameRate60, TrayCommand.FrameRate144, TrayCommand.TogglePanel, TrayCommand.Exit
            }));
            Assert.That(commands.Select(item => item.IsChecked), Is.EqualTo(new[] { true, false, true, false, false, false, true, false, false }));
            Assert.That(items.Count(item => item.IsSeparator), Is.EqualTo(2));
        }
    }
}
