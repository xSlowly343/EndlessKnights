using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class FullscreenDetectorTests
    {
        [TestCase(UserNotificationState.RunningFullscreenDirect3D)]
        [TestCase(UserNotificationState.PresentationMode)]
        public void HidesForFullscreenStates(UserNotificationState state)
        {
            Assert.That(FullscreenDetector.ShouldHideStrip(state), Is.True);
        }

        [TestCase(UserNotificationState.Busy)]
        [TestCase(UserNotificationState.NotPresent)]
        [TestCase(UserNotificationState.AcceptsNotifications)]
        [TestCase(UserNotificationState.QuietTime)]
        [TestCase(UserNotificationState.App)]
        public void StaysVisibleOtherwise(UserNotificationState state)
        {
            Assert.That(FullscreenDetector.ShouldHideStrip(state), Is.False);
        }

        [Test]
        public void AWindowCoversTheMonitorOnlyWhenItFillsItWhole()
        {
            var monitor = new ScreenRect(0, 0, 1920, 1080);

            Assert.That(FullscreenDetector.Covers(new ScreenRect(0, 0, 1920, 1080), monitor), Is.True, "borderless on the whole screen");
            Assert.That(FullscreenDetector.Covers(new ScreenRect(-8, -8, 1928, 1088), monitor), Is.True);
            Assert.That(FullscreenDetector.Covers(new ScreenRect(-8, -8, 1928, 1040), monitor), Is.False, "maximised above the taskbar");
            Assert.That(FullscreenDetector.Covers(new ScreenRect(100, 100, 900, 700), monitor), Is.False);
            Assert.That(FullscreenDetector.Covers(new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 0, 0)), Is.False);
        }

        [TestCase("Progman", true)]
        [TestCase("WorkerW", true)]
        [TestCase("Shell_TrayWnd", true)]
        [TestCase("UnityWndClass", false)]
        [TestCase("", false)]
        public void TheDesktopAndTheTaskbarAreNotFullscreenApps(string className, bool shell)
        {
            Assert.That(FullscreenDetector.IsShellClass(className), Is.EqualTo(shell));
        }

        [Test]
        public void TheStripHidesByHandByExclusiveFullscreenOrByACoveringWindowWhenAllowed()
        {
            var visibility = new StripVisibility();
            Assert.That(visibility.Hidden, Is.False);

            visibility.Covered = true;
            Assert.That(visibility.Hidden, Is.False, "auto-hide is off");
            visibility.AutoHide = true;
            Assert.That(visibility.Hidden, Is.True);
            visibility.Covered = false;
            Assert.That(visibility.Hidden, Is.False, "back after leaving the fullscreen window");

            visibility.Exclusive = true;
            Assert.That(visibility.Hidden, Is.True);
            visibility.Exclusive = false;
            visibility.ByHand = true;
            Assert.That(visibility.Hidden, Is.True);
        }
    }
}
