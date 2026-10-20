using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Game.Platform.Windows.Window
{
    internal static class NativeMethods
    {
        public const int GwlStyle = -16;
        public const int GwlExStyle = -20;
        public const long WsExToolWindow = 0x00000080L;
        public const long WsExAppWindow = 0x00040000L;
        public const long WsExNoActivate = 0x08000000L;
        public static readonly IntPtr HwndTopmost = new IntPtr(-1);
        public const long WsPopup = 0x80000000L;
        public const long WsVisible = 0x10000000L;
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpFrameChanged = 0x0020;
        public const uint SwpShowWindow = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;

            public static Margins WholeWindow => new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        }

        public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        public static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
    }
}
