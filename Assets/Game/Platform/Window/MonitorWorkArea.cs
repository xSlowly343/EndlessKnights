using System.Runtime.InteropServices;

namespace Game.Platform.Windows.Window
{
    public static class MonitorWorkArea
    {
        public static bool TryGet(ScreenRect near, out ScreenRect workArea)
        {
            var nativeRect = new NativeMethods.Rect { Left = near.Left, Top = near.Top, Right = near.Right, Bottom = near.Bottom };
            var monitor = NativeMethods.MonitorFromRect(ref nativeRect, NativeMethods.MonitorDefaultToNearest);
            var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            var found = NativeMethods.GetMonitorInfo(monitor, ref info);
            workArea = new ScreenRect(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
            return found;
        }
    }
}
