using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Game.Platform.Windows.Power;
using UnityEngine;

namespace Game.Platform.Windows.Tray
{
    public sealed class TrayIcon : IDisposable
    {
        public const string WindowClassName = "RiftWardenTray";
        private const uint IconId = 1;
        private const uint CallbackMessage = TrayNativeMethods.WmApp + 1;

        private static readonly TrayNativeMethods.WindowProcedure Procedure = HandleMessage;
        private static readonly Queue<TrayCommand> PendingCommands = new Queue<TrayCommand>();
        private static readonly Queue<SystemEvent> PendingSystemEvents = new Queue<SystemEvent>();
        private static TrayMenuState _menuState = TrayMenuState.Default;
        private static bool _classRegistered;

        private readonly IntPtr _window;
        private bool _disposed;

        private TrayIcon(IntPtr window)
        {
            _window = window;
        }

        public static TrayIcon? Create(string tooltip)
        {
            var instance = TrayNativeMethods.GetModuleHandle(null);
            if (!_classRegistered)
            {
                var windowClass = new TrayNativeMethods.WindowClass
                {
                    Size = (uint)Marshal.SizeOf<TrayNativeMethods.WindowClass>(),
                    Procedure = Procedure,
                    Instance = instance,
                    ClassName = WindowClassName
                };
                _classRegistered = TrayNativeMethods.RegisterClassEx(ref windowClass) != 0;
                if (!_classRegistered)
                    Debug.LogWarning($"Tray: RegisterClassEx failed, error {Marshal.GetLastWin32Error()}");
            }

            var window = TrayNativeMethods.CreateWindowEx(0, WindowClassName, tooltip, unchecked((uint)TrayNativeMethods.WsPopup),
                0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (window == IntPtr.Zero)
            {
                Debug.LogWarning($"Tray: CreateWindowEx failed, error {Marshal.GetLastWin32Error()}");
                return null;
            }

            var data = NotifyData(window);
            data.Flags = TrayNativeMethods.NifMessage | TrayNativeMethods.NifIcon | TrayNativeMethods.NifTip;
            data.CallbackMessage = CallbackMessage;
            data.Icon = TrayNativeMethods.ExtractIcon(instance, ExecutablePath(), 0);
            data.Tip = tooltip;
            if (!TrayNativeMethods.Shell_NotifyIcon(TrayNativeMethods.NimAdd, ref data))
            {
                Debug.LogWarning("Tray: Shell_NotifyIcon(NIM_ADD) failed");
                TrayNativeMethods.DestroyWindow(window);
                return null;
            }

            TrayNativeMethods.WTSRegisterSessionNotification(window, TrayNativeMethods.NotifyForThisSession);
            return new TrayIcon(window);
        }

        public void ShowState(TrayMenuState state)
        {
            _menuState = state;
        }

        public bool TryTakeCommand(out TrayCommand command)
        {
            if (PendingCommands.Count == 0)
            {
                command = default;
                return false;
            }
            command = PendingCommands.Dequeue();
            return true;
        }

        public bool TryTakeSystemEvent(out SystemEvent systemEvent)
        {
            if (PendingSystemEvents.Count == 0)
            {
                systemEvent = default;
                return false;
            }
            systemEvent = PendingSystemEvents.Dequeue();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            var data = NotifyData(_window);
            TrayNativeMethods.Shell_NotifyIcon(TrayNativeMethods.NimDelete, ref data);
            TrayNativeMethods.WTSUnRegisterSessionNotification(_window);
            TrayNativeMethods.DestroyWindow(_window);
        }

        private static string ExecutablePath()
        {
            var path = new char[1024];
            var length = TrayNativeMethods.GetModuleFileName(IntPtr.Zero, path, (uint)path.Length);
            return new string(path, 0, (int)System.Math.Min(length, (uint)path.Length));
        }

        private static TrayNativeMethods.NotifyIconData NotifyData(IntPtr window) => new TrayNativeMethods.NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<TrayNativeMethods.NotifyIconData>(),
            Window = window,
            Id = IconId,
            Tip = string.Empty,
            Info = string.Empty,
            InfoTitle = string.Empty
        };

        [MonoPInvokeCallback(typeof(TrayNativeMethods.WindowProcedure))]
        private static IntPtr HandleMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == CallbackMessage)
            {
                var mouseMessage = (uint)lParam.ToInt64() & 0xFFFF;
                if (mouseMessage == TrayNativeMethods.WmRButtonUp || mouseMessage == TrayNativeMethods.WmContextMenu)
                    ShowMenu(window);
                return IntPtr.Zero;
            }

            if (message == TrayNativeMethods.WmCommand)
            {
                var id = (int)(wParam.ToInt64() & 0xFFFF);
                if (Enum.IsDefined(typeof(TrayCommand), id))
                    PendingCommands.Enqueue((TrayCommand)id);
                return IntPtr.Zero;
            }

            if (SystemEventDecoder.TryDecode(message, wParam.ToInt64(), out var systemEvent))
                PendingSystemEvents.Enqueue(systemEvent);

            return TrayNativeMethods.DefWindowProc(window, message, wParam, lParam);
        }

        private static void ShowMenu(IntPtr window)
        {
            var menu = TrayNativeMethods.CreatePopupMenu();
            foreach (var item in TrayMenu.Items(_menuState))
            {
                if (item.IsSeparator)
                {
                    TrayNativeMethods.AppendMenu(menu, TrayNativeMethods.MfSeparator, UIntPtr.Zero, null);
                    continue;
                }
                var flags = TrayNativeMethods.MfString | (item.IsChecked ? TrayNativeMethods.MfChecked : 0u);
                TrayNativeMethods.AppendMenu(menu, flags, new UIntPtr((uint)item.Command), item.Text);
            }

            TrayNativeMethods.GetCursorPos(out var cursor);
            TrayNativeMethods.SetForegroundWindow(window);
            TrayNativeMethods.TrackPopupMenu(menu, TrayNativeMethods.TpmRightButton | TrayNativeMethods.TpmBottomAlign, cursor.X, cursor.Y, 0, window, IntPtr.Zero);
            TrayNativeMethods.PostMessage(window, TrayNativeMethods.WmNull, IntPtr.Zero, IntPtr.Zero);
            TrayNativeMethods.DestroyMenu(menu);
        }
    }
}
