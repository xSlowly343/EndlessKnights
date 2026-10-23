using System;
using System.Runtime.InteropServices;
using Game.Platform.Windows.Tray;
using Game.Platform.Windows.Window;

namespace Game.Platform.Windows.Instance
{
    public static class SingleInstance
    {
        private const string MutexName = "Local\\RiftWarden.SingleInstance";
        private const int ErrorAlreadyExists = 183;

        private static IntPtr _mutex;

        public static bool TryClaim()
        {
            _mutex = CreateMutex(IntPtr.Zero, false, MutexName);
            return _mutex != IntPtr.Zero && Marshal.GetLastWin32Error() != ErrorAlreadyExists;
        }

        public static void ActivateRunningInstance()
        {
            var trayWindow = FindWindow(TrayIcon.WindowClassName, null);
            if (trayWindow == IntPtr.Zero)
                return;
            GetWindowThreadProcessId(trayWindow, out var processId);
            WindowController.FindWindowOfProcess(processId)?.ShowAndActivate();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateMutex(IntPtr attributes, bool initialOwner, string name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string? windowName);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
