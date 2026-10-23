using System;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class ClickThroughDriver : MonoBehaviour
    {
        private WindowController _window = null!;
        private Camera _camera = null!;
        private PointerInput _pointer = null!;
        private PanelView _panels = null!;
        private bool _passingClicks;
        private bool _enabled = true;

        public event Action<bool>? Changed;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                Changed?.Invoke(value);
            }
        }

        public static ClickThroughDriver Attach(GameObject host, WindowController window, Camera camera, PointerInput pointer, PanelView panels, FocusModeButton toggleButton)
        {
            var driver = host.AddComponent<ClickThroughDriver>();
            driver._panels = panels;
            driver._pointer = pointer;
            driver._window = window;
            driver._camera = camera;
            toggleButton.Clicked += driver.Toggle;
            driver.Changed += toggleButton.ShowPassingClicks;
            toggleButton.ShowPassingClicks(driver.Enabled);
            return driver;
        }

        public void Toggle() => Enabled = !Enabled;

        private void Update()
        {
            if (!Enabled)
            {
                _passingClicks = false;
                _window.SetClickThrough(false);
                return;
            }

            if (!_window.TryGetBounds(out var bounds) || !ScreenCursor.TryGetPosition(out var x, out var y))
                return;

            var catches = StripHitTest.TryGetWorldPoint(_camera, bounds.Left, bounds.Top, bounds.Width, bounds.Height, x, y, out var worldPoint)
                && CatchesAt(worldPoint);
            _passingClicks = ClickThroughPolicy.ShouldPassClicks(catches, _pointer.Held, _passingClicks);
            _window.SetClickThrough(_passingClicks);
        }

        private bool CatchesAt(Vector2 worldPoint)
        {
            if (_panels.IsOpen)
                return StripHitTest.IsClickableAt(worldPoint);
            return PointerState.IsControlDown() && StripHitTest.IsSpriteAt(worldPoint);
        }
    }
}
