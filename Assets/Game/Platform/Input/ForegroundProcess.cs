using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Game.Platform.Windows.Input
{
    public static class ForegroundProcess
    {
        private const uint QueryLimitedInformation = 0x1000;
        private const int PathCapacity = 1024;

        private static readonly StringBuilder PathBuffer = new StringBuilder(PathCapacity);

        public static string? ExecutablePath()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return null;
            GetWindowThreadProcessId(window, out var processId);
            var process = OpenProcess(QueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
                return null;
            try
            {
                PathBuffer.Clear();
                var size = (uint)PathCapacity;
                return QueryFullProcessImageName(process, 0, PathBuffer, ref size) ? PathBuffer.ToString() : null;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
