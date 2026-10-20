using Game.Platform.Windows.Window;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class FullscreenDetectorTests
    {
        [TestCase(UserNotificationState.Busy)]
        [TestCase(UserNotificationState.RunningFullscreenDirect3D)]
        [TestCase(UserNotificationState.PresentationMode)]
        public void HidesForFullscreenStates(UserNotificationState state)
        {
            Assert.That(FullscreenDetector.ShouldHideStrip(state), Is.True);
        }

        [TestCase(UserNotificationState.NotPresent)]
        [TestCase(UserNotificationState.AcceptsNotifications)]
        [TestCase(UserNotificationState.QuietTime)]
        [TestCase(UserNotificationState.App)]
        public void StaysVisibleOtherwise(UserNotificationState state)
        {
            Assert.That(FullscreenDetector.ShouldHideStrip(state), Is.False);
        }
    }
}
