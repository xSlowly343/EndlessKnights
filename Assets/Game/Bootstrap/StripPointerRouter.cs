using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class StripPointerRouter : MonoBehaviour
    {
        private PointerInput _pointer = null!;
        private WindowController _window = null!;
        private Camera _camera = null!;
        private FocusModeButton _focusButton = null!;
        private TaskbarDockDriver _dock = null!;

        public static void Attach(GameObject host, PointerInput pointer, WindowController window, Camera camera, FocusModeButton focusButton, TaskbarDockDriver dock)
        {
            var router = host.AddComponent<StripPointerRouter>();
            router._pointer = pointer;
            router._window = window;
            router._camera = camera;
            router._focusButton = focusButton;
            router._dock = dock;
        }

        private void Update()
        {
            if (_pointer.Pressed)
                HandlePress();
            else if (_pointer.Released)
                _dock.EndDrag();
            else if (_pointer.Held)
                _dock.ContinueDrag();
        }

        private void HandlePress()
        {
            if (!_window.TryGetBounds(out var bounds))
                return;
            if (!StripHitTest.TryGetWorldPoint(_camera, bounds.Left, bounds.Top, bounds.Width, bounds.Height, _pointer.X, _pointer.Y, out var worldPoint))
                return;

            if (_focusButton.Contains(worldPoint))
                _focusButton.Click();
            else if (StripHitTest.IsSpriteAt(worldPoint))
                _dock.BeginDrag();
        }
    }
}
