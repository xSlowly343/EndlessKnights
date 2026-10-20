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
        private IntPtr _lastOtherForeground;
        private bool _clickThrough;

        private WindowController(IntPtr window)
        {
            _window = window;
        }

        public static WindowController? FindOwnWindow() => FindWindowOfProcess((uint)Process.GetCurrentProcess().Id);

        public static WindowController? FindWindowOfProcess(uint processId)
        {
            _searchedProcessId = processId;
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

        public void PlaceAt(ScreenRect rect)
        {
            NativeMethods.SetWindowPos(_window, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        }

        public void KeepOnTop()
        {
            SetAlwaysOnTop(true);
        }

        public void SetAlwaysOnTop(bool onTop)
        {
            NativeMethods.SetWindowPos(_window, onTop ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost, 0, 0, 0, 0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        }

        public bool IsForeground => NativeMethods.GetForegroundWindow() == _window;

        public void ShowInTaskbar()
        {
            UpdateExStyle(NativeMethods.WsExAppWindow, NativeMethods.WsExToolWindow);
        }

        public void SetNoActivate(bool enabled)
        {
            if (enabled)
                UpdateExStyle(NativeMethods.WsExNoActivate, 0L);
            else
                UpdateExStyle(0L, NativeMethods.WsExNoActivate);
        }

        public void RememberOtherForeground()
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero && foreground != _window)
                _lastOtherForeground = foreground;
        }

        public void ReturnFocusToLastOtherWindow()
        {
            if (_lastOtherForeground != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(_lastOtherForeground);
        }

        public void ShowAndActivate()
        {
            NativeMethods.ShowWindow(_window, NativeMethods.SwShowNoActivate);
            NativeMethods.SetForegroundWindow(_window);
        }

        public void SetHidden(bool hidden)
        {
            NativeMethods.ShowWindow(_window, hidden ? NativeMethods.SwHide : NativeMethods.SwShowNoActivate);
        }

        public bool TryGetBounds(out ScreenRect bounds)
        {
            var found = NativeMethods.GetWindowRect(_window, out var rect);
            bounds = new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return found;
        }

        public void SetClickThrough(bool enabled)
        {
            if (enabled == _clickThrough)
                return;
            _clickThrough = enabled;
            const long clickThroughStyles = NativeMethods.WsExTransparent | NativeMethods.WsExLayered;
            if (enabled)
            {
                UpdateExStyle(clickThroughStyles, 0L);
                NativeMethods.SetLayeredWindowAttributes(_window, 0, byte.MaxValue, NativeMethods.LwaAlpha);
            }
            else
            {
                UpdateExStyle(0L, clickThroughStyles);
            }
        }

        private void UpdateExStyle(long add, long remove)
        {
            var exStyle = NativeMethods.GetWindowLongPtr(_window, NativeMethods.GwlExStyle).ToInt64();
            exStyle = (exStyle | add) & ~remove;
            NativeMethods.SetWindowLongPtr(_window, NativeMethods.GwlExStyle, new IntPtr(exStyle));
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
