namespace Game.Platform.Windows.Window
{
    public static class FullscreenDetector
    {
        public static bool TryGetState(out UserNotificationState state)
        {
            var found = NativeMethods.SHQueryUserNotificationState(out var raw) == 0;
            state = (UserNotificationState)raw;
            return found;
        }

        public static bool ShouldHideStrip(UserNotificationState state) =>
            state == UserNotificationState.RunningFullscreenDirect3D
            || state == UserNotificationState.PresentationMode;
    }
}
