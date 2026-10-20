using System;
using System.Runtime.InteropServices;

namespace Game.Platform.Windows.Window
{
    public static class Taskbar
    {
        public static bool TryGetPosition(out TaskbarEdge edge, out ScreenRect bounds)
        {
            var data = new NativeMethods.AppBarData { Size = (uint)Marshal.SizeOf<NativeMethods.AppBarData>() };
            var found = NativeMethods.SHAppBarMessage(NativeMethods.AbmGetTaskbarPos, ref data) != UIntPtr.Zero;
            edge = (TaskbarEdge)data.Edge;
            bounds = new ScreenRect(data.Bounds.Left, data.Bounds.Top, data.Bounds.Right, data.Bounds.Bottom);
            return found;
        }
    }
}
