using System;
using Game.Platform.Windows.Input;
using Game.Platform.Windows.Power;
using Game.Platform.Windows.Tray;
using Game.Presentation.Strip;
using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class TrayDriver : MonoBehaviour
    {
        private TrayIcon _tray = null!;
        private WindowController _window = null!;
        private ClickThroughDriver _clickThrough = null!;
        private FramePacingDriver _framePacing = null!;
        private TaskbarDockDriver _dock = null!;
        private RawInputService? _rawInput;
        private readonly SleepTracker _sleep = new SleepTracker();
        private TrayMenuState _state = TrayMenuState.Default;

        public static void Attach(GameObject host, TrayIcon tray, WindowController window, ClickThroughDriver clickThrough, FramePacingDriver framePacing, TaskbarDockDriver dock, RawInputService? rawInput)
        {
            var driver = host.AddComponent<TrayDriver>();
            driver._tray = tray;
            driver._window = window;
            driver._clickThrough = clickThrough;
            driver._framePacing = framePacing;
            driver._dock = dock;
            driver._rawInput = rawInput;
            tray.ShowState(driver._state);
        }

        private void Update()
        {
            while (_tray.TryTakeCommand(out var command))
                Execute(command);
            while (_tray.TryTakeSystemEvent(out var systemEvent))
                Handle(systemEvent);
        }

        private void Execute(TrayCommand command)
        {
            if (command == TrayCommand.Exit)
            {
                Application.Quit();
                return;
            }

            _state = _state.Apply(command);
            _tray.ShowState(_state);
            GamePause.Set(_state.Paused);
            _rawInput?.SetPaused(_state.Paused);
            _clickThrough.Enabled = _state.ClickThrough;
            _window.SetAlwaysOnTop(_state.AlwaysOnTop);
            _framePacing.SetFrameRate(_state.FrameRate);
        }

        private void Handle(SystemEvent systemEvent)
        {
            switch (systemEvent)
            {
                case SystemEvent.Suspend:
                    _sleep.Suspended(DateTime.UtcNow);
                    Debug.Log("System is going to sleep");
                    break;
                case SystemEvent.Resume:
                    var asleep = _sleep.Resumed(DateTime.UtcNow);
                    Debug.Log($"System resumed after {asleep.TotalSeconds:F0} s asleep");
                    _dock.ForceRedock();
                    break;
                case SystemEvent.SessionLocked:
                    RenderPause.Set(RenderPauseReason.SessionLocked, true);
                    Debug.Log("Session locked, rendering paused");
                    break;
                case SystemEvent.SessionUnlocked:
                    RenderPause.Set(RenderPauseReason.SessionLocked, false);
                    _dock.ForceRedock();
                    Debug.Log("Session unlocked, rendering resumed");
                    break;
            }
        }

        private void OnDestroy()
        {
            _tray.Dispose();
        }

        private void OnApplicationQuit()
        {
            _tray.Dispose();
        }
    }
}
