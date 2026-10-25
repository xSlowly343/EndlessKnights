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
        private InputDebugOverlay _inputDebug = null!;
        private PanelModeDriver? _panelMode;
        private readonly SleepTracker _sleep = new SleepTracker();
        private TrayMenuState _state = TrayMenuState.Default;

        public event Action<TrayMenuState>? StateChanged;

        public TrayMenuState State => _state;

        public static TrayDriver Attach(GameObject host, TrayIcon tray, WindowController window, ClickThroughDriver clickThrough, FramePacingDriver framePacing, TaskbarDockDriver dock, RawInputService? rawInput, InputDebugOverlay inputDebug, PanelModeDriver? panelMode = null)
        {
            var driver = host.AddComponent<TrayDriver>();
            driver._tray = tray;
            driver._window = window;
            driver._clickThrough = clickThrough;
            driver._framePacing = framePacing;
            driver._dock = dock;
            driver._rawInput = rawInput;
            driver._inputDebug = inputDebug;
            driver._panelMode = panelMode;
            tray.ShowState(driver._state);
            clickThrough.Changed += driver.SyncClickThrough;
            inputDebug.SetVisible(driver._state.ShowInputDebug);
            return driver;
        }

        public void UseState(TrayMenuState state)
        {
            _state = state;
            ApplyState();
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
            if (command == TrayCommand.TogglePanel)
            {
                _panelMode?.Toggle();
                return;
            }

            _state = _state.Apply(command);
            ApplyState();
            StateChanged?.Invoke(_state);
        }

        private void ApplyState()
        {
            _tray.ShowState(_state);
            GamePause.Set(_state.Paused);
            _rawInput?.SetPaused(_state.Paused);
            _clickThrough.Enabled = _state.ClickThrough;
            _window.SetAlwaysOnTop(_state.AlwaysOnTop);
            _dock.AlwaysOnTop = _state.AlwaysOnTop;
            _framePacing.SetFrameRate(_state.FrameRate);
            _inputDebug.SetVisible(_state.ShowInputDebug);
        }

        private void SyncClickThrough(bool enabled)
        {
            if (_state.ClickThrough == enabled)
                return;
            _state = _state.Apply(TrayCommand.ToggleClickThrough);
            _tray.ShowState(_state);
            StateChanged?.Invoke(_state);
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
