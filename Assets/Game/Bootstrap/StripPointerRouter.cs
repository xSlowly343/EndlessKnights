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
        private StripButton _menuButton = null!;
        private PanelView _panels = null!;
        private bool _panelCaptured;

        public static void Attach(GameObject host, PointerInput pointer, WindowController window, Camera camera, FocusModeButton focusButton, TaskbarDockDriver dock, StripButton menuButton, PanelView panels)
        {
            var router = host.AddComponent<StripPointerRouter>();
            router._pointer = pointer;
            router._window = window;
            router._camera = camera;
            router._focusButton = focusButton;
            router._dock = dock;
            router._menuButton = menuButton;
            router._panels = panels;
        }

        private void Update()
        {
            if (_pointer.Pressed)
                HandlePress();
            else if (_pointer.Released)
                HandleRelease();
            else if (_pointer.Held)
                HandleHold();
        }

        private void HandleHold()
        {
            if (!_panelCaptured)
            {
                _dock.ContinueDrag();
                return;
            }
            if (TryGetWorldPoint(out var worldPoint))
                _panels.Drag(worldPoint);
        }

        private void HandleRelease()
        {
            if (!_panelCaptured)
            {
                _dock.EndDrag();
                return;
            }
            _panelCaptured = false;
            if (TryGetWorldPoint(out var worldPoint))
                _panels.Release(worldPoint);
            else
                _panels.CancelPress();
        }

        private bool TryGetWorldPoint(out Vector2 worldPoint)
        {
            worldPoint = default;
            return _window.TryGetBounds(out var bounds)
                && StripHitTest.TryGetWorldPoint(_camera, bounds.Left, bounds.Top, bounds.Width, bounds.Height, _pointer.X, _pointer.Y, out worldPoint);
        }

        private void HandlePress()
        {
            if (!TryGetWorldPoint(out var worldPoint))
                return;

            if (_panels.TryClick(worldPoint))
            {
                _panelCaptured = true;
                return;
            }
            if (_menuButton.Contains(worldPoint))
                _menuButton.Click();
            else if (_focusButton.Contains(worldPoint))
                _focusButton.Click();
            else if (StripHitTest.IsSpriteAt(worldPoint))
                _dock.BeginDrag();
        }
    }
}
