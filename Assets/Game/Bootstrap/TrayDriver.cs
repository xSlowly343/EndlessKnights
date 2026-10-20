using Game.Platform.Windows.Tray;
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
        private TrayMenuState _state = TrayMenuState.Default;

        public static void Attach(GameObject host, TrayIcon tray, WindowController window, ClickThroughDriver clickThrough, FramePacingDriver framePacing)
        {
            var driver = host.AddComponent<TrayDriver>();
            driver._tray = tray;
            driver._window = window;
            driver._clickThrough = clickThrough;
            driver._framePacing = framePacing;
            tray.ShowState(driver._state);
        }

        private void Update()
        {
            while (_tray.TryTakeCommand(out var command))
                Execute(command);
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
            _clickThrough.Enabled = _state.ClickThrough;
            _window.SetAlwaysOnTop(_state.AlwaysOnTop);
            _framePacing.SetFrameRate(_state.FrameRate);
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
