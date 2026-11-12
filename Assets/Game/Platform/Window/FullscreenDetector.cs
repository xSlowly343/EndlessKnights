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

        public static bool Covers(ScreenRect window, ScreenRect monitor) =>
            monitor.Width > 0 && monitor.Height > 0
            && window.Left <= monitor.Left && window.Top <= monitor.Top
            && window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

        public static bool IsShellClass(string className) =>
            className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd";
    }
}
