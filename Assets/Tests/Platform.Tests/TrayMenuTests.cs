using System.Linq;
using Game.Platform.Windows.Tray;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class TrayMenuTests
    {
        [Test]
        public void DefaultsAreUnpausedClickThroughAndOnTop()
        {
            var state = TrayMenuState.Default;

            Assert.That(state.Paused, Is.False);
            Assert.That(state.ClickThrough, Is.True);
            Assert.That(state.AlwaysOnTop, Is.True);
        }

        [TestCase(TrayCommand.TogglePause)]
        [TestCase(TrayCommand.ToggleClickThrough)]
        [TestCase(TrayCommand.ToggleAlwaysOnTop)]
        public void EachToggleFlipsOnlyItsOwnOption(TrayCommand command)
        {
            var before = TrayMenuState.Default;

            var after = before.Apply(command);

            Assert.That(after.Paused, Is.EqualTo(command == TrayCommand.TogglePause ? !before.Paused : before.Paused));
            Assert.That(after.ClickThrough, Is.EqualTo(command == TrayCommand.ToggleClickThrough ? !before.ClickThrough : before.ClickThrough));
            Assert.That(after.AlwaysOnTop, Is.EqualTo(command == TrayCommand.ToggleAlwaysOnTop ? !before.AlwaysOnTop : before.AlwaysOnTop));
        }

        [Test]
        public void ExitDoesNotChangeState()
        {
            var after = TrayMenuState.Default.Apply(TrayCommand.Exit);

            Assert.That(after.Paused, Is.False);
            Assert.That(after.ClickThrough, Is.True);
            Assert.That(after.AlwaysOnTop, Is.True);
        }

        [Test]
        public void MenuListsAllCommandsInOrderWithChecks()
        {
            var state = new TrayMenuState(paused: true, clickThrough: false, alwaysOnTop: true);

            var items = TrayMenu.Items(state);

            Assert.That(items.Where(item => !item.IsSeparator).Select(item => item.Command), Is.EqualTo(new[]
            {
                TrayCommand.TogglePause, TrayCommand.ToggleClickThrough, TrayCommand.ToggleAlwaysOnTop, TrayCommand.Exit
            }));
            Assert.That(items[0].IsChecked, Is.True);
            Assert.That(items[1].IsChecked, Is.False);
            Assert.That(items[2].IsChecked, Is.True);
            Assert.That(items.Count(item => item.IsSeparator), Is.EqualTo(1));
        }
    }
}
