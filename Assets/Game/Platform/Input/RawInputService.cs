using System;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Game.Core.Input;
using UnityEngine;

namespace Game.Platform.Windows.Input
{
    public sealed class RawInputService : IInputSource, IDisposable
    {
        private const string WindowClassName = "EndlessKnightsRawInput";
        private const int BufferSize = 1024;
        private const int HeaderSize = 24;
        private const int DeviceOffset = 8;
        private const int KeyboardFlagsOffset = HeaderSize + 2;
        private const int KeyboardVirtualKeyOffset = HeaderSize + 6;

        private static readonly RawInputNativeMethods.WindowProcedure Procedure = HandleMessage;
        private static readonly InputCounters Counters = new InputCounters();
        private static readonly KeyboardEventFilter Keyboard = new KeyboardEventFilter();
        private static IntPtr _buffer;

        private readonly Thread _thread;
        private IntPtr _window;
        private volatile bool _started;

        private RawInputService()
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "RawInput" };
        }

        public static RawInputService Start()
        {
            var service = new RawInputService();
            service._thread.Start();
            return service;
        }

        public bool IsRunning => _started && _thread.IsAlive;

        public InputSnapshot Drain() => Counters.Drain();

        public void Dispose()
        {
            if (_window != IntPtr.Zero)
                RawInputNativeMethods.PostMessage(_window, RawInputNativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        private void Run()
        {
            if (_buffer == IntPtr.Zero)
                _buffer = Marshal.AllocHGlobal(BufferSize);

            _window = CreateMessageWindow();
            if (_window == IntPtr.Zero)
            {
                Debug.LogWarning($"RawInput: could not create message window, error {Marshal.GetLastWin32Error()}");
                return;
            }

            if (!RegisterDevices(_window))
            {
                Debug.LogWarning($"RawInput: RegisterRawInputDevices failed, error {Marshal.GetLastWin32Error()}");
                RawInputNativeMethods.DestroyWindow(_window);
                return;
            }

            _started = true;
            while (RawInputNativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                RawInputNativeMethods.DispatchMessage(ref message);
        }

        private static IntPtr CreateMessageWindow()
        {
            var instance = RawInputNativeMethods.GetModuleHandle(null);
            var windowClass = new RawInputNativeMethods.WindowClass
            {
                Size = (uint)Marshal.SizeOf<RawInputNativeMethods.WindowClass>(),
                Procedure = Procedure,
                Instance = instance,
                ClassName = WindowClassName
            };
            RawInputNativeMethods.RegisterClassEx(ref windowClass);
            return RawInputNativeMethods.CreateWindowEx(0, WindowClassName, WindowClassName, 0, 0, 0, 0, 0,
                RawInputNativeMethods.HwndMessage, IntPtr.Zero, instance, IntPtr.Zero);
        }

        private static bool RegisterDevices(IntPtr window)
        {
            var devices = new[]
            {
                new RawInputNativeMethods.RawInputDevice
                {
                    UsagePage = RawInputNativeMethods.UsagePageGeneric,
                    Usage = RawInputNativeMethods.UsageKeyboard,
                    Flags = RawInputNativeMethods.RideVInputSink,
                    Target = window
                }
            };
            return RawInputNativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputNativeMethods.RawInputDevice>());
        }

        [MonoPInvokeCallback(typeof(RawInputNativeMethods.WindowProcedure))]
        private static IntPtr HandleMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == RawInputNativeMethods.WmInput)
            {
                ReadRawInput(lParam);
                return RawInputNativeMethods.DefWindowProc(window, message, wParam, lParam);
            }

            if (message == RawInputNativeMethods.WmClose)
            {
                RawInputNativeMethods.DestroyWindow(window);
                return IntPtr.Zero;
            }

            if (message == RawInputNativeMethods.WmDestroy)
            {
                RawInputNativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return RawInputNativeMethods.DefWindowProc(window, message, wParam, lParam);
        }

        private static void ReadRawInput(IntPtr rawInput)
        {
            var size = (uint)BufferSize;
            if (RawInputNativeMethods.GetRawInputData(rawInput, RawInputNativeMethods.RidInput, _buffer, ref size, HeaderSize) == uint.MaxValue)
                return;

            var type = (uint)Marshal.ReadInt32(_buffer, 0);
            if (type != RawInputNativeMethods.RimTypeKeyboard)
                return;
            if (InjectedInputFilter.IsInjected(Marshal.ReadIntPtr(_buffer, DeviceOffset)))
                return;

            var flags = (ushort)Marshal.ReadInt16(_buffer, KeyboardFlagsOffset);
            var virtualKey = (ushort)Marshal.ReadInt16(_buffer, KeyboardVirtualKeyOffset);
            Counters.AddKey(Keyboard.Process(virtualKey, flags));
        }
    }
}
