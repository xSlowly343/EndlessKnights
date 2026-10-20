namespace Game.Platform.Windows.Window
{
    public static class MonitorDpi
    {
        public static uint ForRect(ScreenRect rect)
        {
            var nativeRect = new NativeMethods.Rect { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
            var monitor = NativeMethods.MonitorFromRect(ref nativeRect, NativeMethods.MonitorDefaultToNearest);
            return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var dpiX, out _) == 0
                ? dpiX
                : StripSize.BaseDpi;
        }
    }
}
