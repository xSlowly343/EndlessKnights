using System;
using System.Diagnostics;
using System.Text;
using AOT;

namespace Game.Platform.Windows.Window
{
    public sealed class WindowController
    {
        private const string UnityWindowClass = "UnityWndClass";

        private static uint _searchedProcessId;
        private static IntPtr _foundWindow;

        private readonly IntPtr _window;

        private WindowController(IntPtr window)
        {
            _window = window;
        }

        public static WindowController? FindOwnWindow()
        {
            _searchedProcessId = (uint)Process.GetCurrentProcess().Id;
            _foundWindow = IntPtr.Zero;
            NativeMethods.EnumWindows(MatchOwnWindow, IntPtr.Zero);
            return _foundWindow == IntPtr.Zero ? null : new WindowController(_foundWindow);
        }

        public void MakeBorderless(int width, int height)
        {
            NativeMethods.SetWindowLongPtr(_window, NativeMethods.GwlStyle, new IntPtr(NativeMethods.WsPopup | NativeMethods.WsVisible));
            NativeMethods.SetWindowPos(_window, IntPtr.Zero, 0, 0, width, height,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow);
        }

        public void KeepOnTop()
        {
            NativeMethods.SetWindowPos(_window, NativeMethods.HwndTopmost, 0, 0, 0, 0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        }

        public void MakeBackgroundTransparent()
        {
            var margins = NativeMethods.Margins.WholeWindow;
            NativeMethods.DwmExtendFrameIntoClientArea(_window, ref margins);
        }

        [MonoPInvokeCallback(typeof(NativeMethods.EnumWindowsProc))]
        private static bool MatchOwnWindow(IntPtr window, IntPtr parameter)
        {
            NativeMethods.GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId != _searchedProcessId || !IsUnityWindow(window))
                return true;
            _foundWindow = window;
            return false;
        }

        private static bool IsUnityWindow(IntPtr window)
        {
            var className = new StringBuilder(64);
            NativeMethods.GetClassName(window, className, className.Capacity);
            return className.ToString() == UnityWindowClass;
        }
    }
}
