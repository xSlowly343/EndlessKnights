using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using Game.Platform.Windows.Window;
using AOT;
using Game.Core.Input;
using Debug = UnityEngine.Debug;

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
        private const int MouseFlagsOffset = HeaderSize;
        private const int MouseButtonFlagsOffset = HeaderSize + 4;
        private const int MouseButtonDataOffset = HeaderSize + 6;
        private const int MouseLastXOffset = HeaderSize + 12;
        private const int MouseLastYOffset = HeaderSize + 16;
        private const int PointerSpeedRefreshMilliseconds = 5000;

        private static readonly RawInputNativeMethods.WindowProcedure Procedure = HandleMessage;
        private static readonly InputCounters Counters = new InputCounters();
        private static readonly KeyboardEventFilter Keyboard = new KeyboardEventFilter();
        private static readonly MouseMotion Mouse = new MouseMotion();
        private static readonly FlickDetector Flicks = new FlickDetector();
        private static readonly CircleDetector Circles = new CircleDetector();
        private static readonly JigglerFilter Jiggler = new JigglerFilter();
        private static readonly RhythmMeter Rhythm = new RhythmMeter();
        private static readonly WheelAccumulator Wheel = new WheelAccumulator();
        private static readonly OwnWindowArea OwnWindow = new OwnWindowArea();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static int _pointerSpeedCheckedAt = int.MinValue;
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

        public InputSnapshot Drain() => Counters.Drain(Clock.ElapsedMilliseconds);

        public void SetOwnWindowBounds(ScreenRect bounds) => OwnWindow.Set(bounds);

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
                },
                new RawInputNativeMethods.RawInputDevice
                {
                    UsagePage = RawInputNativeMethods.UsagePageGeneric,
                    Usage = RawInputNativeMethods.UsageMouse,
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

            if (InjectedInputFilter.IsInjected(Marshal.ReadIntPtr(_buffer, DeviceOffset)))
                return;

            var type = (uint)Marshal.ReadInt32(_buffer, 0);
            if (type == RawInputNativeMethods.RimTypeKeyboard)
                ReadKeyboard();
            else if (type == RawInputNativeMethods.RimTypeMouse)
                ReadMouse();
        }

        private static void ReadKeyboard()
        {
            var flags = (ushort)Marshal.ReadInt16(_buffer, KeyboardFlagsOffset);
            var virtualKey = (ushort)Marshal.ReadInt16(_buffer, KeyboardVirtualKeyOffset);
            var keyClass = Keyboard.Process(virtualKey, flags);
            if (keyClass == KeyClass.None)
                return;
            Counters.AddKey(keyClass);
            RecordRhythmicEvent();
        }

        private static void ReadMouse()
        {
            RefreshPointerSpeed();
            var flags = (ushort)Marshal.ReadInt16(_buffer, MouseFlagsOffset);
            var lastX = Marshal.ReadInt32(_buffer, MouseLastXOffset);
            var lastY = Marshal.ReadInt32(_buffer, MouseLastYOffset);
            var virtualDesktop = (flags & MouseMotion.VirtualDesktop) != 0;
            var width = RawInputNativeMethods.GetSystemMetrics(virtualDesktop ? RawInputNativeMethods.SmCxVirtualScreen : RawInputNativeMethods.SmCxScreen);
            var height = RawInputNativeMethods.GetSystemMetrics(virtualDesktop ? RawInputNativeMethods.SmCyVirtualScreen : RawInputNativeMethods.SmCyScreen);
            var now = Clock.ElapsedMilliseconds;
            var (dx, dy) = Mouse.DeltaPx(flags, lastX, lastY, width, height);
            if (dx != 0 || dy != 0)
                TrackMovement(dx, dy, now);

            var buttons = MouseButtons.Decode(
                (ushort)Marshal.ReadInt16(_buffer, MouseButtonFlagsOffset),
                Marshal.ReadInt16(_buffer, MouseButtonDataOffset));
            if (buttons.AnyClick && !IsCursorOverOwnWindow())
            {
                Counters.AddClicks(buttons);
                RecordRhythmicEvent();
            }
            if (buttons.WheelDelta != 0)
            {
                Counters.AddWheelTicks(Wheel.AddDelta(buttons.WheelDelta));
                Counters.MarkInput(now);
            }
        }

        private static void TrackMovement(double dx, double dy, long now)
        {
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var accepted = Jiggler.Add(dx, dy, now);
            if (accepted > 0)
            {
                Counters.AddMouseDistance(accepted);
                Counters.MarkInput(now);
            }
            if (Flicks.Add(distance, now))
                Counters.AddFlick();
            Counters.AddCircleTurns(Circles.Add(dx, dy, now));
        }

        private static void RecordRhythmicEvent()
        {
            var now = Clock.ElapsedMilliseconds;
            Counters.MarkInput(now);
            Rhythm.Record(now);
            Counters.SetRhythm(Rhythm.CoefficientOfVariation());
        }

        private static bool IsCursorOverOwnWindow() =>
            RawInputNativeMethods.GetCursorPos(out var point) && OwnWindow.Contains(point.X, point.Y);

        private static void RefreshPointerSpeed()
        {
            var now = Environment.TickCount;
            if (now - _pointerSpeedCheckedAt < PointerSpeedRefreshMilliseconds)
                return;
            _pointerSpeedCheckedAt = now;
            Mouse.SetPointerSpeed(RawInputNativeMethods.SystemParametersInfo(RawInputNativeMethods.SpiGetMouseSpeed, 0, out var speed, 0)
                ? speed
                : MouseMotion.DefaultPointerSpeed);
        }
    }
}
