using System;
using System.Runtime.InteropServices;

namespace Game.Platform.Windows.Tray
{
    internal static class TrayNativeMethods
    {
        public const uint NimAdd = 0x00000000;
        public const uint NimDelete = 0x00000002;
        public const uint NifMessage = 0x00000001;
        public const uint NifIcon = 0x00000002;
        public const uint NifTip = 0x00000004;
        public const uint WmApp = 0x8000;
        public const uint WmCommand = 0x0111;
        public const uint WmNull = 0x0000;
        public const uint WmRButtonUp = 0x0205;
        public const uint WmContextMenu = 0x007B;
        public const uint MfString = 0x00000000;
        public const uint MfChecked = 0x00000008;
        public const uint MfSeparator = 0x00000800;
        public const uint TpmRightButton = 0x0002;
        public const uint TpmBottomAlign = 0x0020;
        public const long WsPopup = 0x80000000L;

        public delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WindowClass
        {
            public uint Size;
            public uint Style;
            public WindowProcedure Procedure;
            public int ClassExtra;
            public int WindowExtra;
            public IntPtr Instance;
            public IntPtr Icon;
            public IntPtr Cursor;
            public IntPtr Background;
            public string? MenuName;
            public string ClassName;
            public IntPtr SmallIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NotifyIconData
        {
            public uint Size;
            public IntPtr Window;
            public uint Id;
            public uint Flags;
            public uint CallbackMessage;
            public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Tip;
            public uint State;
            public uint StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Info;
            public uint TimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string InfoTitle;
            public uint InfoFlags;
            public Guid Item;
            public IntPtr BalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassEx(ref WindowClass windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern uint GetModuleFileName(IntPtr module, [Out] char[] fileName, uint size);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr ExtractIcon(IntPtr instance, string exePath, uint index);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

        public const uint NotifyForThisSession = 0;

        [DllImport("wtsapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WTSRegisterSessionNotification(IntPtr window, uint flags);

        [DllImport("wtsapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WTSUnRegisterSessionNotification(IntPtr window);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
