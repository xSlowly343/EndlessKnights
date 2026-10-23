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
        private bool _passingClicks;

        public bool Enabled { get; set; } = true;

        public static ClickThroughDriver Attach(GameObject host, WindowController window, Camera camera, PointerInput pointer)
        {
            var driver = host.AddComponent<ClickThroughDriver>();
            driver._pointer = pointer;
            driver._window = window;
            driver._camera = camera;
            return driver;
        }

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

            var overSprite = StripHitTest.IsClickableAt(_camera, bounds.Left, bounds.Top, bounds.Width, bounds.Height, x, y);
            var buttonHeld = _pointer.Held;

            _passingClicks = ClickThroughPolicy.ShouldPassClicks(overSprite, buttonHeld, _passingClicks);
            _window.SetClickThrough(_passingClicks);
        }
    }
}
